using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Contracts;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Security;

namespace NVGInventory.Modules.Dispatching.Controllers;

[ApiController]
[Route("api/dispatch")]
[Authorize]
public sealed class DispatchRecommendationController : ControllerBase
{
    private readonly InventoryDbContext _dbContext;
    private readonly IPostDeliveryRecommendationService _recommendationService;
    private readonly IAuditService _auditService;

    public DispatchRecommendationController(
        InventoryDbContext dbContext,
        IPostDeliveryRecommendationService recommendationService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _recommendationService = recommendationService;
        _auditService = auditService;
    }

    [HttpPost("trips/{tripId:guid}/recommendations")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<DispatchRecommendationResponse>>> GenerateRecommendations(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        await _recommendationService.GenerateRecommendationsAsync(tripId, cancellationToken);
        var recommendations = await LoadPendingQuery(DateTime.UtcNow)
            .Where(recommendation => recommendation.CompletedTripId == tripId)
            .ToListAsync(cancellationToken);

        return Ok(recommendations.Select(MapRecommendation).ToList());
    }

    [HttpGet("recommendations/pending")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<IReadOnlyCollection<DispatchRecommendationResponse>>> GetPending(
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var recommendations = await LoadPendingQuery(now)
            .OrderByDescending(recommendation => recommendation.GeneratedAt)
            .ThenBy(recommendation => recommendation.Rank)
            .ToListAsync(cancellationToken);

        return Ok(recommendations.Select(MapRecommendation).ToList());
    }

    [HttpPost("recommendations/{id:guid}/accept")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<ActionResult<DispatchRecommendationResponse>> Accept(
        Guid id,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var recommendation = await _dbContext.DispatchRecommendations
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (recommendation is null)
        {
            return NotFound("Recommendation not found.");
        }

        if (recommendation.ExpiresAt < now)
        {
            return Conflict("Recommendation has expired.");
        }

        if (recommendation.WasAccepted || recommendation.WasIgnored)
        {
            return Conflict("Recommendation was already reviewed.");
        }

        var actorUserId = User.GetUserId();
        recommendation.WasAccepted = true;
        recommendation.ReviewedByUserId = actorUserId;
        recommendation.ReviewedAt = now;

        var siblingRecommendations = await _dbContext.DispatchRecommendations
            .Where(item =>
                item.CompletedTripId == recommendation.CompletedTripId &&
                item.Id != recommendation.Id &&
                !item.WasAccepted &&
                !item.WasIgnored)
            .ToListAsync(cancellationToken);
        foreach (var sibling in siblingRecommendations)
        {
            sibling.WasIgnored = true;
            sibling.ReviewedByUserId = actorUserId;
            sibling.ReviewedAt = now;
        }

        _auditService.AddEntry(
            actorUserId,
            AuditActions.RecommendationAccepted,
            EntityTypes.DispatchRecommendation,
            recommendation.Id,
            null,
            new
            {
                RecommendationId = recommendation.Id,
                recommendation.RecommendedTripId,
                recommendation.CompletedTripId
            },
            tripId: recommendation.RecommendedTripId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var loaded = await LoadRecommendationById(id, cancellationToken);
        return Ok(MapRecommendation(loaded!));
    }

    [HttpPost("recommendations/{id:guid}/ignore")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin}")]
    public async Task<IActionResult> Ignore(
        Guid id,
        CancellationToken cancellationToken)
    {
        var recommendation = await _dbContext.DispatchRecommendations
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (recommendation is null)
        {
            return NotFound("Recommendation not found.");
        }

        if (!recommendation.WasAccepted && !recommendation.WasIgnored)
        {
            var actorUserId = User.GetUserId();
            recommendation.WasIgnored = true;
            recommendation.ReviewedByUserId = actorUserId;
            recommendation.ReviewedAt = DateTime.UtcNow;

            _auditService.AddEntry(
                actorUserId,
                AuditActions.RecommendationIgnored,
                EntityTypes.DispatchRecommendation,
                recommendation.Id,
                null,
                new
                {
                    RecommendationId = recommendation.Id,
                    recommendation.RecommendedTripId,
                    recommendation.CompletedTripId
                },
                tripId: recommendation.RecommendedTripId);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok();
    }

    [HttpGet("recommendations/history")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Admin},{RoleNames.SuperAdmin},{RoleNames.Ceo}")]
    public async Task<ActionResult<PagedResult<DispatchRecommendationResponse>>> GetHistory(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDateRange(from, to, out var fromUtc, out var toUtc, out var dateError))
        {
            return BadRequest(dateError);
        }

        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        var query = LoadBaseQuery()
            .Where(recommendation => recommendation.WasAccepted || recommendation.WasIgnored);
        if (fromUtc.HasValue)
        {
            query = query.Where(recommendation => recommendation.GeneratedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(recommendation => recommendation.GeneratedAt <= toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(recommendation => recommendation.GeneratedAt)
            .ThenBy(recommendation => recommendation.Rank)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<DispatchRecommendationResponse>(
            items.Select(MapRecommendation).ToList(),
            totalCount,
            resolvedPage,
            resolvedPageSize));
    }

    private IQueryable<DispatchRecommendation> LoadPendingQuery(DateTime now)
    {
        return LoadBaseQuery()
            .Where(recommendation =>
                recommendation.ExpiresAt > now &&
                !recommendation.WasAccepted &&
                !recommendation.WasIgnored);
    }

    private IQueryable<DispatchRecommendation> LoadBaseQuery()
    {
        return _dbContext.DispatchRecommendations
            .Include(recommendation => recommendation.CompletedTrip)
                .ThenInclude(trip => trip.Driver)
            .Include(recommendation => recommendation.CompletedTrip)
                .ThenInclude(trip => trip.TruckAsset)
            .Include(recommendation => recommendation.CompletedTrip)
                .ThenInclude(trip => trip.Stops)
            .Include(recommendation => recommendation.CompletedTrip)
                .ThenInclude(trip => trip.StatusHistory)
            .Include(recommendation => recommendation.RecommendedTrip)
                .ThenInclude(trip => trip.Stops)
            .Include(recommendation => recommendation.ReviewedByUser)
            .AsSplitQuery();
    }

    private async Task<DispatchRecommendation?> LoadRecommendationById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await LoadBaseQuery()
            .FirstOrDefaultAsync(recommendation => recommendation.Id == id, cancellationToken);
    }

    private static DispatchRecommendationResponse MapRecommendation(DispatchRecommendation recommendation)
    {
        var completedTrip = recommendation.CompletedTrip;
        var recommendedTrip = recommendation.RecommendedTrip;
        var completedDropoff = completedTrip.Stops
            .FirstOrDefault(stop => stop.StopType == TripStopType.Dropoff)
            ?.LocationText;
        var recommendedPickup = recommendedTrip.Stops
            .FirstOrDefault(stop => stop.StopType == TripStopType.Pickup)
            ?.LocationText;
        var recommendedDropoff = recommendedTrip.Stops
            .FirstOrDefault(stop => stop.StopType == TripStopType.Dropoff)
            ?.LocationText;
        var deliveredAt = completedTrip.StatusHistory
            .Where(history => history.ToStatus == TripStatus.Delivered)
            .OrderByDescending(history => history.EventAt)
            .Select(history => (DateTime?)history.EventAt)
            .FirstOrDefault();
        var agingHours = Math.Max(0, (DateTime.UtcNow - recommendedTrip.CreatedAt).TotalHours);

        return new DispatchRecommendationResponse(
            recommendation.Id,
            recommendation.Rank,
            recommendation.TotalScore,
            new CompletedTripRecommendationResponse(
                completedTrip.Id,
                completedTrip.Driver?.Username ?? "Unassigned driver",
                completedTrip.TruckAsset?.AssetCode ?? completedTrip.TruckAsset?.PlateNo ?? "Unassigned truck",
                completedDropoff ?? string.Empty,
                deliveredAt),
            new RecommendedTripRecommendationResponse(
                recommendedTrip.Id,
                recommendedTrip.ContainerNumber,
                recommendedPickup ?? string.Empty,
                recommendedDropoff ?? string.Empty,
                recommendedTrip.ContainerSize,
                recommendedTrip.TripType,
                recommendedTrip.Stops
                    .Where(stop => stop.StopType == TripStopType.Pickup)
                    .Select(stop => stop.ScheduledAt)
                    .FirstOrDefault(),
                Math.Round(agingHours, 2)),
            recommendation.ExpiresAt,
            recommendation.GeneratedAt,
            recommendation.WasAccepted,
            recommendation.WasIgnored,
            recommendation.ReviewedByUser?.Username,
            recommendation.ReviewedAt);
    }

    private static bool TryResolveDateRange(
        string? from,
        string? to,
        out DateTime? fromUtc,
        out DateTime? toUtc,
        out string? error)
    {
        fromUtc = null;
        toUtc = null;

        if (!TryParseUtcDate(from, out fromUtc, out error))
        {
            return false;
        }

        if (!TryParseUtcDate(to, out toUtc, out error))
        {
            return false;
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            error = "'from' must be earlier than or equal to 'to'.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryResolvePaging(
        int? page,
        int? pageSize,
        out int resolvedPage,
        out int resolvedPageSize,
        out string? error)
    {
        resolvedPage = page.GetValueOrDefault(1);
        if (resolvedPage < 1)
        {
            error = "Page must be at least 1.";
            resolvedPageSize = 0;
            return false;
        }

        resolvedPageSize = pageSize.GetValueOrDefault(20);
        if (resolvedPageSize < 1 || resolvedPageSize > 100)
        {
            error = "Page size must be between 1 and 100.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseUtcDate(string? raw, out DateTime? parsed, out string? error)
    {
        parsed = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value))
        {
            error = "Invalid date value.";
            return false;
        }

        parsed = value.UtcDateTime;
        return true;
    }
}

public sealed record DispatchRecommendationResponse(
    Guid RecommendationId,
    int Rank,
    decimal TotalScore,
    CompletedTripRecommendationResponse CompletedTrip,
    RecommendedTripRecommendationResponse RecommendedTrip,
    DateTime ExpiresAt,
    DateTime GeneratedAt,
    bool WasAccepted,
    bool WasIgnored,
    string? ReviewedBy,
    DateTime? ReviewedAt);

public sealed record CompletedTripRecommendationResponse(
    Guid TripId,
    string DriverName,
    string TruckPlate,
    string DropoffLocation,
    DateTime? DeliveredAt);

public sealed record RecommendedTripRecommendationResponse(
    Guid TripId,
    string? ContainerNumber,
    string PickupLocation,
    string DropoffLocation,
    string? ContainerSize,
    string? TripType,
    DateTime? ScheduledPickupTime,
    double AgingHours);
