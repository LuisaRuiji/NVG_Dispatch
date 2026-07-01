using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Hubs;
using NVGInventory.Hubs.Events;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Services;

namespace NVGInventory.Modules.Dispatching.Services;

public interface IPostDeliveryRecommendationService
{
    Task GenerateRecommendationsAsync(Guid completedTripId, CancellationToken ct = default);
}

public sealed class PostDeliveryRecommendationService : IPostDeliveryRecommendationService
{
    private static readonly char[] Punctuation =
    [
        ',', '.', ';', ':', '-', '_', '/', '\\', '(', ')', '[', ']', '{', '}', '\'', '"'
    ];

    private static readonly HashSet<string> LocationKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "davao",
        "tagum",
        "panabo",
        "bunawan",
        "calinan",
        "tomas",
        "dict",
        "ktc",
        "kudos",
        "tadeco",
        "dole",
        "manila",
        "harbor"
    };

    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<PostDeliveryRecommendationService> _logger;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly IVaiaCacheService? _cacheService;

    public PostDeliveryRecommendationService(
        InventoryDbContext dbContext,
        ILogger<PostDeliveryRecommendationService> logger,
        IServiceScopeFactory? serviceScopeFactory = null,
        IVaiaCacheService? cacheService = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _cacheService = cacheService;
    }

    public async Task GenerateRecommendationsAsync(Guid completedTripId, CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var hasActiveRecommendations = await _dbContext.DispatchRecommendations
                .AsNoTracking()
                .AnyAsync(recommendation =>
                    recommendation.CompletedTripId == completedTripId &&
                    recommendation.ExpiresAt > now &&
                    !recommendation.WasAccepted &&
                    !recommendation.WasIgnored,
                    ct);
            if (hasActiveRecommendations)
            {
                return;
            }

            var completedTrip = await _dbContext.DispatchTrips
                .Include(trip => trip.Stops)
                .AsNoTracking()
                .FirstOrDefaultAsync(trip => trip.Id == completedTripId, ct);
            if (completedTrip is null || completedTrip.Status != TripStatus.Delivered)
            {
                return;
            }

            if (!completedTrip.DriverUserId.HasValue || !completedTrip.TruckAssetId.HasValue)
            {
                return;
            }

            var dropoffLocation = completedTrip.Stops
                .FirstOrDefault(stop => stop.StopType == TripStopType.Dropoff)
                ?.LocationText;
            if (string.IsNullOrWhiteSpace(dropoffLocation))
            {
                return;
            }

            var candidates = await LoadPendingUnassignedTripsAsync(ct);
            candidates = candidates
                .Where(trip => trip.Id != completedTripId)
                .ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            var todayStart = now.Date;
            var todayEnd = todayStart.AddDays(1);
            var driverTripsToday = await _dbContext.DispatchTripStatusHistories
                .AsNoTracking()
                .Where(history =>
                    history.ToStatus == TripStatus.Delivered &&
                    history.TripId != completedTripId &&
                    history.EventAt >= todayStart &&
                    history.EventAt < todayEnd &&
                    _dbContext.DispatchTrips.Any(trip =>
                        trip.Id == history.TripId &&
                        trip.DriverUserId == completedTrip.DriverUserId))
                .Select(history => history.TripId)
                .Distinct()
                .CountAsync(ct);
            var availabilityScore = ScoreAvailability(driverTripsToday);

            var truckCapability = await _dbContext.DispatchTrucks
                .AsNoTracking()
                .Where(truck => truck.AssetId == completedTrip.TruckAssetId)
                .Select(truck => truck.ContainerCapability)
                .FirstOrDefaultAsync(ct);

            var scored = candidates
                .Select(candidate =>
                {
                    var pickupLocation = candidate.Stops
                        .FirstOrDefault(stop => stop.StopType == TripStopType.Pickup)
                        ?.LocationText;
                    var proximityScore = ScoreProximity(dropoffLocation, pickupLocation);
                    var truckMatchScore = ScoreTruckMatch(truckCapability, candidate.ContainerSize);
                    var agingScore = ScoreAging(now - candidate.CreatedAt);
                    var totalScore = Math.Round(
                        0.40m * proximityScore +
                        0.30m * availabilityScore +
                        0.20m * truckMatchScore +
                        0.10m * agingScore,
                        4,
                        MidpointRounding.AwayFromZero);

                    return new RecommendationScore(
                        candidate.Id,
                        proximityScore,
                        availabilityScore,
                        truckMatchScore,
                        agingScore,
                        totalScore);
                })
                .OrderByDescending(score => score.TotalScore)
                .ThenBy(score => score.RecommendedTripId)
                .Take(3)
                .ToList();

            if (scored.Count == 0)
            {
                return;
            }

            var generatedAt = DateTime.UtcNow;
            var recommendations = scored
                .Select((score, index) => new DispatchRecommendation
                {
                    Id = Guid.NewGuid(),
                    CompletedTripId = completedTrip.Id,
                    DriverId = completedTrip.DriverUserId.Value,
                    TruckId = completedTrip.TruckAssetId.Value,
                    RecommendedTripId = score.RecommendedTripId,
                    ProximityScore = score.ProximityScore,
                    AvailabilityScore = score.AvailabilityScore,
                    TruckMatchScore = score.TruckMatchScore,
                    AgingScore = score.AgingScore,
                    TotalScore = score.TotalScore,
                    Rank = index + 1,
                    GeneratedAt = generatedAt,
                    ExpiresAt = generatedAt.AddMinutes(30)
                })
                .ToList();

            _dbContext.DispatchRecommendations.AddRange(recommendations);
            await _dbContext.SaveChangesAsync(ct);
            QueueRecommendationGeneratedBroadcast(completedTrip.Id, recommendations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Recommendation generation failed for trip {TripId}. Delivery status is not affected.",
                completedTripId);
        }
    }

    private async Task<List<Trip>> LoadPendingUnassignedTripsAsync(CancellationToken ct)
    {
        async Task<List<Trip>> LoadAsync()
        {
            return await _dbContext.DispatchTrips
                .Include(trip => trip.Stops)
                .AsNoTracking()
                .Where(trip => trip.Status == TripStatus.Draft && trip.DriverUserId == null)
                .ToListAsync(ct);
        }

        return _cacheService is null
            ? await LoadAsync()
            : await _cacheService.GetOrSetAsync(
                VaiaCacheKeys.PendingUnassignedTrips,
                LoadAsync,
                TimeSpan.FromMinutes(1),
                ct) ?? [];
    }

    private void QueueRecommendationGeneratedBroadcast(Guid completedTripId, int recommendationCount)
    {
        if (_serviceScopeFactory is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<PostDeliveryRecommendationService>>();
            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                var hubContext = scope.ServiceProvider
                    .GetRequiredService<IHubContext<VaiaDispatchHub, IVaiaDispatchClient>>();
                var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                var completedTrip = await dbContext.DispatchTrips
                    .Include(trip => trip.Driver)
                    .Include(trip => trip.TruckAsset)
                    .Include(trip => trip.StatusHistory)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(trip => trip.Id == completedTripId);
                if (completedTrip is null)
                {
                    return;
                }

                var deliveredAt = completedTrip.StatusHistory
                    .Where(history => history.ToStatus == TripStatus.Delivered)
                    .OrderByDescending(history => history.EventAt)
                    .Select(history => (DateTime?)history.EventAt)
                    .FirstOrDefault();
                var e = new RecommendationGeneratedEvent(
                    completedTrip.Id,
                    completedTrip.Driver?.Username ?? "Unassigned driver",
                    completedTrip.TruckAsset?.PlateNo ?? completedTrip.TruckAsset?.AssetCode ?? "Unassigned truck",
                    deliveredAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                    recommendationCount);

                await hubContext.Clients
                    .Group(VaiaDispatchHub.DispatchOpsGroup)
                    .RecommendationGenerated(e);
                await pushService.SendToRoleAsync(
                    RoleNames.Dispatcher,
                    "Post-Delivery Recommendations",
                    $"{e.DriverName} just delivered - {recommendationCount} nearby jobs available",
                    new Dictionary<string, string> { ["completedTripId"] = completedTrip.Id.ToString() });
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to broadcast recommendation generation for trip {TripId}.",
                    completedTripId);
            }
        });
    }

    private static decimal ScoreProximity(string? completedDropoff, string? candidatePickup)
    {
        var completed = NormalizeLocation(completedDropoff);
        var candidate = NormalizeLocation(candidatePickup);
        if (completed.Length == 0 || candidate.Length == 0)
        {
            return 0.2m;
        }

        if (string.Equals(completed.Normalized, candidate.Normalized, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0m;
        }

        var overlap = completed.Keywords.Intersect(candidate.Keywords, StringComparer.OrdinalIgnoreCase).ToList();
        if (overlap.Count == 0)
        {
            return 0.2m;
        }

        return overlap.Any(word => word is not ("davao" or "city" or "del" or "norte"))
            ? 1.0m
            : 0.6m;
    }

    private static decimal ScoreAvailability(int tripsToday)
    {
        return tripsToday switch
        {
            <= 0 => 1.0m,
            1 => 0.8m,
            2 => 0.5m,
            _ => 0.2m
        };
    }

    private static decimal ScoreTruckMatch(string? truckCapability, string? containerSize)
    {
        if (string.IsNullOrWhiteSpace(containerSize))
        {
            return 0.5m;
        }

        var capability = NormalizeComparable(truckCapability);
        var size = NormalizeComparable(containerSize);
        if (capability.Length == 0)
        {
            return 0.3m;
        }

        var requested = ResolveContainerClass(size);
        var capacity = ResolveContainerClass(capability);
        if (requested == ContainerClass.Unknown || capacity == ContainerClass.Unknown)
        {
            return 0.3m;
        }

        if (requested == capacity || capability.Contains(size, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0m;
        }

        return capacity > requested ? 0.7m : 0.3m;
    }

    private static decimal ScoreAging(TimeSpan age)
    {
        if (age.TotalHours < 1)
        {
            return 0.2m;
        }

        if (age.TotalHours < 4)
        {
            return 0.5m;
        }

        if (age.TotalHours < 8)
        {
            return 0.8m;
        }

        return 1.0m;
    }

    private static NormalizedLocation NormalizeLocation(string? value)
    {
        var normalized = NormalizeComparable(value);
        var words = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length > 1)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keywords = words
            .Where(word => LocationKeywords.Contains(word))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new NormalizedLocation(normalized, keywords);
    }

    private static string NormalizeComparable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLower(CultureInfo.InvariantCulture);
        foreach (var punctuation in Punctuation)
        {
            normalized = normalized.Replace(punctuation, ' ');
        }

        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static ContainerClass ResolveContainerClass(string value)
    {
        if (value.Contains("40hc", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("fortyhc", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("high cube", StringComparison.OrdinalIgnoreCase))
        {
            return ContainerClass.FortyHighCube;
        }

        if (value.Contains("40", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("forty", StringComparison.OrdinalIgnoreCase))
        {
            return ContainerClass.Forty;
        }

        if (value.Contains("20", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("twenty", StringComparison.OrdinalIgnoreCase))
        {
            return ContainerClass.Twenty;
        }

        return ContainerClass.Unknown;
    }

    private enum ContainerClass
    {
        Unknown = 0,
        Twenty = 1,
        Forty = 2,
        FortyHighCube = 3
    }

    private sealed record NormalizedLocation(string Normalized, HashSet<string> Keywords)
    {
        public int Length => Normalized.Length;
    }

    private sealed record RecommendationScore(
        Guid RecommendedTripId,
        decimal ProximityScore,
        decimal AvailabilityScore,
        decimal TruckMatchScore,
        decimal AgingScore,
        decimal TotalScore);
}
