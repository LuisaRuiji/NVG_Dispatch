using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Security;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Modules.ShipmentRequests.Services;

namespace NVGInventory.Modules.Dispatching.Controllers;

[ApiController]
[Route("api/dispatch/planning")]
[Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager}")]
public sealed class DispatchPlanningController : ControllerBase
{
    private readonly DispatchPlanningService _planningService;
    private readonly PlanningDecisionSupportService _decisionSupportService;
    private readonly ShipmentRequestService _shipmentRequestService;

    public DispatchPlanningController(
        DispatchPlanningService planningService,
        PlanningDecisionSupportService decisionSupportService,
        ShipmentRequestService shipmentRequestService)
    {
        _planningService = planningService;
        _decisionSupportService = decisionSupportService;
        _shipmentRequestService = shipmentRequestService;
    }

    [HttpGet("board")]
    public async Task<ActionResult<PlanningBoardSnapshot>> GetBoard(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? day = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) return BadRequest("Page must be at least 1.");
        if (pageSize is < 1 or > 100) return BadRequest("Page size must be between 1 and 100.");

        return Ok(await _planningService.GetBoardAsync(page, pageSize, search, day, cancellationToken));
    }

    [HttpGet("resources")]
    public async Task<ActionResult<PlanningResourceSnapshot>> GetResources(
        [FromQuery] DateTime? pickupAt,
        [FromQuery] DateTime? dropoffAt,
        [FromQuery] Guid? excludeTripId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _planningService.GetResourcesAsync(
                pickupAt,
                dropoffAt,
                excludeTripId,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("bookings/{requestId:guid}/start")]
    public async Task<ActionResult<PlanningStartResponse>> StartPlanning(
        Guid requestId,
        PlanningStartRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _shipmentRequestService.StartPlanningAsync(
            requestId,
            BuildActor(),
            request?.ScheduledPickupTime,
            cancellationToken);
        return Ok(new PlanningStartResponse(result.Request.Id, result.TripId, result.Request.Status, result.Created));
    }

    [HttpGet("trips/{tripId:guid}/decision-support")]
    public async Task<ActionResult<DispatcherPlanningDecisionSupportResponse>> GetDecisionSupport(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return Ok(ToDispatcherResponse(await _decisionSupportService.GetDecisionSupportAsync(tripId, cancellationToken)));
    }

    [HttpPost("trips/{tripId:guid}/validate")]
    public async Task<ActionResult<DispatcherPlanningDecisionSupportResponse>> ValidateForDispatch(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return Ok(ToDispatcherResponse(await _decisionSupportService.ValidateForDispatchAsync(tripId, cancellationToken)));
    }

    [HttpPost("trips/{tripId:guid}/ready")]
    public async Task<ActionResult<MarkTripReadyResult>> MarkReady(
        Guid tripId,
        PlanningMarkReadyRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _decisionSupportService.MarkReadyAsync(
            tripId,
            new MarkTripReadyCommand(request.RowVersion, request.RecommendationToken, request.SelectedRank, request.OverrideReason),
            BuildActor(),
            cancellationToken));
    }

    private DispatchActorContext BuildActor() => new(
        User.GetUserId(),
        User.IsInRole(RoleNames.Manager),
        User.IsInRole(RoleNames.Dispatcher),
        User.IsInRole(RoleNames.Driver),
        User.IsInRole(RoleNames.HeadOfFinance),
        User.IsInRole(RoleNames.Ceo),
        User.IsInRole(RoleNames.Admin));

    // The decision-support service retains the complete CSP-TOPSIS diagnostic model for
    // audit, development, and thesis analysis. Dispatchers receive only the operational
    // information needed to make and validate an assignment.
    private static DispatcherPlanningDecisionSupportResponse ToDispatcherResponse(PlanningDecisionSupportResponse response) => new(
        response.TripId,
        response.CanMarkReady,
        response.ResourcesEvaluated,
        response.BookingChecks.Select(check => new DispatcherPlanningCheck(check.State, check.Message)).ToArray(),
        response.Recommendations.Count,
        response.ExcludedResources.Select(resource => new DispatcherExcludedResource(
            resource.ResourceType,
            resource.ResourceLabel,
            resource.Checks
                .Where(check => check.State != PlanningCheckState.Passed)
                .Select(check => check.Message)
                .ToArray())).ToArray(),
        response.Recommendations.Select(recommendation => new DispatcherAssignmentSuggestion(
            recommendation.Rank,
            recommendation.Rank == 1,
            recommendation.DriverUserId,
            recommendation.DriverName,
            recommendation.TruckAssetId,
            recommendation.TruckCode,
            recommendation.TrailerAssetId,
            recommendation.TrailerCode,
            recommendation.Reasons,
            recommendation.Warnings)).ToArray(),
        response.RecommendationToken);
}

public sealed record DispatcherPlanningCheck(PlanningCheckState State, string Message);

public sealed record DispatcherExcludedResource(
    string ResourceType,
    string ResourceLabel,
    IReadOnlyCollection<string> Reasons);

public sealed record DispatcherAssignmentSuggestion(
    int SelectionRank,
    bool IsRecommended,
    Guid DriverUserId,
    string DriverName,
    Guid TruckAssetId,
    string TruckCode,
    Guid? TrailerAssetId,
    string? TrailerCode,
    IReadOnlyCollection<string> Reasons,
    IReadOnlyCollection<string> Warnings);

public sealed record DispatcherPlanningDecisionSupportResponse(
    Guid TripId,
    bool CanMarkReady,
    bool ResourcesEvaluated,
    IReadOnlyCollection<DispatcherPlanningCheck> BookingChecks,
    int AvailableAssignmentCount,
    IReadOnlyCollection<DispatcherExcludedResource> ExcludedResources,
    IReadOnlyCollection<DispatcherAssignmentSuggestion> Recommendations,
    string RecommendationToken);
