using System.Data;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.ShipmentRequests.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

[JsonConverter(typeof(JsonStringEnumConverter<PlanningCheckState>))]
public enum PlanningCheckState { Passed, Warning, Blocked }

public sealed record PlanningValidationCheck(string Code, PlanningCheckState State, string Message);

public sealed record PlanningExcludedResource(
    string ResourceType,
    Guid ResourceId,
    string ResourceLabel,
    IReadOnlyCollection<PlanningValidationCheck> Checks);

public sealed record PlanningCriteriaContribution(
    string Criterion,
    string Direction,
    decimal Weight,
    decimal RawValue,
    decimal WeightedValue,
    string Explanation);

public sealed record PlanningAssignmentRecommendation(
    int Rank,
    Guid DriverUserId,
    string DriverName,
    Guid TruckAssetId,
    string TruckCode,
    Guid? TrailerAssetId,
    string? TrailerCode,
    decimal Score,
    IReadOnlyCollection<PlanningCriteriaContribution> CriteriaContributions,
    IReadOnlyCollection<string> Reasons,
    IReadOnlyCollection<string> Warnings);

public sealed record PlanningDecisionSupportResponse(
    Guid TripId,
    bool CanMarkReady,
    bool ResourcesEvaluated,
    IReadOnlyCollection<PlanningValidationCheck> BookingChecks,
    int FeasibleCombinationCount,
    IReadOnlyCollection<PlanningExcludedResource> ExcludedResources,
    IReadOnlyCollection<PlanningAssignmentRecommendation> Recommendations,
    string RecommendationToken,
    DateTime GeneratedAt,
    DateTime ExpiresAt,
    string CriteriaWeightVersion,
    long AvailabilityVersion);

public sealed record MarkTripReadyCommand(
    string RowVersion,
    string RecommendationToken,
    int? SelectedRank,
    string? OverrideReason);

public sealed record MarkTripReadyResult(
    Guid TripId,
    TripStatus Status,
    string RowVersion,
    int? SelectedRank,
    bool WasManualOverride);

public sealed class PlanningDecisionSupportService
{
    public const string CriteriaWeightVersion = "planning-topsis-v1";
    private const decimal MaxPickupReachabilityKm = 450m;
    // Planning keeps the choice focused: one recommended assignment and up to two
    // viable alternatives. The full ranked diagnostic remains in the service snapshot.
    private const int RecommendationLimit = 3;
    private static readonly IReadOnlyDictionary<string, decimal> Weights = new Dictionary<string, decimal>
    {
        ["Pickup proximity"] = 0.40m,
        ["Driver availability"] = 0.25m,
        ["Equipment fit"] = 0.20m,
        ["Trailer fit"] = 0.15m
    };

    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly PlanningRecommendationSnapshotStore _snapshotStore;
    private readonly IPlanningAvailabilityNotifier _availabilityNotifier;

    public PlanningDecisionSupportService(
        InventoryDbContext dbContext,
        IAuditService auditService,
        PlanningRecommendationSnapshotStore snapshotStore,
        IPlanningAvailabilityNotifier availabilityNotifier)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _snapshotStore = snapshotStore;
        _availabilityNotifier = availabilityNotifier;
    }

    public async Task<PlanningDecisionSupportResponse> GetDecisionSupportAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await EvaluateAsync(tripId, cancellationToken);
        var generatedAt = DateTime.UtcNow;
        var expiresAt = generatedAt.AddMinutes(5);
        var recommendations = RankCandidates(evaluation.Candidates)
            .Take(RecommendationLimit)
            .ToList();
        var snapshot = _snapshotStore.Store(
            evaluation.Trip.Id,
            Convert.ToBase64String(evaluation.Trip.RowVersion),
            recommendations,
            generatedAt,
            expiresAt);

        return new PlanningDecisionSupportResponse(
            evaluation.Trip.Id,
            evaluation.BookingChecks.All(check => check.State != PlanningCheckState.Blocked) &&
            evaluation.CurrentAssignmentIsFeasible,
            evaluation.ResourcesEvaluated,
            evaluation.BookingChecks,
            evaluation.Candidates.Count,
            evaluation.ExcludedResources,
            recommendations,
            snapshot.Token,
            generatedAt,
            expiresAt,
            CriteriaWeightVersion,
            snapshot.AvailabilityVersion);
    }

    public Task<PlanningDecisionSupportResponse> ValidateForDispatchAsync(
        Guid tripId,
        CancellationToken cancellationToken = default) =>
        GetDecisionSupportAsync(tripId, cancellationToken);

    public async Task<MarkTripReadyResult> MarkReadyAsync(
        Guid tripId,
        MarkTripReadyCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsDispatcher && !actor.IsManager)
        {
            throw new ForbiddenDomainException("Only dispatchers or managers can mark a trip ready for dispatch.");
        }

        if (!_snapshotStore.TryGet(command.RecommendationToken, out var snapshot) || snapshot.TripId != tripId)
        {
            throw new ConcurrencyConflictException("Planning recommendations are stale. Refresh validation before marking this trip ready.");
        }

        var expectedRowVersion = DecodeRowVersion(command.RowVersion);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var trip = await _dbContext.DispatchTrips
            .Include(item => item.Stops)
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.Id == tripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        if (trip.Status != TripStatus.Draft)
        {
            throw new ConflictDomainException("Only Draft trips can be marked ready for dispatch.");
        }

        if (!trip.RowVersion.SequenceEqual(expectedRowVersion) || !string.Equals(snapshot.TripRowVersion, command.RowVersion, StringComparison.Ordinal))
        {
            throw new ConcurrencyConflictException("This trip was updated by someone else. Refresh the planning drawer before continuing.");
        }

        var evaluation = await EvaluateAsync(tripId, cancellationToken);
        var blockers = evaluation.BookingChecks
            .Where(check => check.State == PlanningCheckState.Blocked)
            .Select(check => check.Message)
            .ToList();
        var selected = evaluation.Candidates.FirstOrDefault(candidate =>
            candidate.DriverUserId == trip.DriverUserId &&
            candidate.TruckAssetId == trip.TruckAssetId &&
            candidate.TrailerAssetId == trip.TrailerAssetId);

        if (selected is null)
        {
            blockers.Add("The selected driver, truck, and trailer combination is no longer feasible.");
        }

        if (blockers.Count > 0)
        {
            throw new BusinessRuleViolationException(string.Join(" ", blockers));
        }

        var rankedSelection = snapshot.Recommendations.FirstOrDefault(recommendation =>
            recommendation.DriverUserId == trip.DriverUserId &&
            recommendation.TruckAssetId == trip.TruckAssetId &&
            recommendation.TrailerAssetId == trip.TrailerAssetId);
        var selectedRank = rankedSelection?.Rank;
        if (command.SelectedRank.HasValue && command.SelectedRank != selectedRank)
        {
            throw new ConcurrencyConflictException("The selected recommendation changed. Refresh planning before marking the trip ready.");
        }

        var isManualOverride = !selectedRank.HasValue;
        if (isManualOverride && string.IsNullOrWhiteSpace(command.OverrideReason))
        {
            throw new BusinessRuleViolationException("Provide an override reason when selecting a valid combination outside the ranked recommendations.");
        }

        var now = DateTime.UtcNow;
        var previousStatus = trip.Status;
        trip.Status = TripStatus.ReadyForDispatch;
        trip.UpdatedAt = now;
        _dbContext.DispatchTripStatusHistories.Add(new TripStatusHistory
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            EventType = TripHistoryEventType.StatusChange,
            FromStatus = previousStatus,
            ToStatus = trip.Status,
            ActorUserId = actor.UserId,
            Remarks = isManualOverride ? command.OverrideReason?.Trim() : $"Planning recommendation rank {selectedRank} selected.",
            EventAt = now,
            RecordedAt = now
        });
        _auditService.AddEntry(
            actor.UserId,
            "DISPATCH_TRIP_MARKED_READY",
            EntityTypes.DispatchTrip,
            trip.Id,
            new { Status = previousStatus },
            new
            {
                Status = trip.Status,
                SelectedRank = selectedRank,
                WasManualOverride = isManualOverride,
                OverrideReason = isManualOverride ? command.OverrideReason?.Trim() : null,
                CriteriaWeightVersion
            },
            tripId: trip.Id);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("This trip or one of its resources changed. Refresh planning and try again.");
        }

        await _availabilityNotifier.InvalidateAsync("trip-ready-for-dispatch", trip.Id, cancellationToken);
        return new MarkTripReadyResult(
            trip.Id,
            trip.Status,
            Convert.ToBase64String(trip.RowVersion),
            selectedRank,
            isManualOverride);
    }

    private async Task<PlanningEvaluation> EvaluateAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Include(item => item.Stops)
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.Id == tripId, cancellationToken);
        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        var booking = await _dbContext.ShipmentRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ConvertedTripId == trip.Id, cancellationToken);
        var now = DateTime.UtcNow;
        var bookingChecks = BuildBookingChecks(trip, booking, now);
        var pickup = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Pickup);
        var dropoff = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Dropoff);

        // ATW verification and a container number are dispatch-handoff gates, not
        // planning gates.  A dispatcher must be able to find a feasible resource
        // combination while resolving either of those requirements.
        var resourcesEvaluated = !HasResourceEvaluationBlocker(bookingChecks);
        if (!resourcesEvaluated)
        {
            return new PlanningEvaluation(trip, bookingChecks, [], [], false, false);
        }

        var drivers = await _dbContext.DispatchDrivers.AsNoTracking().Include(item => item.User).ToListAsync(cancellationToken);
        var trucks = await _dbContext.DispatchTrucks.AsNoTracking().Include(item => item.Asset).ToListAsync(cancellationToken);
        var trailers = await _dbContext.DispatchTrailers.AsNoTracking().Include(item => item.Asset).ToListAsync(cancellationToken);
        var maintenanceAssetIds = await _dbContext.Requests.AsNoTracking()
            .Where(item => item.RequestType == RequestType.MaintenanceIssue &&
                           item.Status != RequestStatus.Closed &&
                           item.Status != RequestStatus.Rejected &&
                           item.AssetId.HasValue)
            .Select(item => item.AssetId!.Value)
            .ToListAsync(cancellationToken);
        var windows = await LoadAssignmentWindowsAsync(trip.Id, cancellationToken);

        var driverChecks = drivers.ToDictionary(
            driver => driver.UserId,
            driver => BuildDriverChecks(driver, pickup, dropoff, windows));
        var truckChecks = trucks.ToDictionary(
            truck => truck.AssetId,
            truck => BuildTruckChecks(truck, trip, pickup, dropoff, windows, maintenanceAssetIds));
        var trailerChecks = trailers.ToDictionary(
            trailer => trailer.AssetId,
            trailer => BuildTrailerChecks(trailer, trip, pickup, dropoff, windows));

        var excluded = new List<PlanningExcludedResource>();
        excluded.AddRange(drivers.Where(driver => driverChecks[driver.UserId].Any(IsBlocked)).Select(driver =>
            new PlanningExcludedResource("Driver", driver.UserId, driver.User?.Username ?? "Unnamed driver", driverChecks[driver.UserId])));
        excluded.AddRange(trucks.Where(truck => truckChecks[truck.AssetId].Any(IsBlocked)).Select(truck =>
            new PlanningExcludedResource("Truck", truck.AssetId, truck.PlateNumber, truckChecks[truck.AssetId])));
        excluded.AddRange(trailers.Where(trailer => trailerChecks[trailer.AssetId].Any(IsBlocked)).Select(trailer =>
            new PlanningExcludedResource("Trailer", trailer.AssetId, trailer.TrailerCode, trailerChecks[trailer.AssetId])));
        excluded.AddRange(trucks
            .Where(truck => !truckChecks[truck.AssetId].Any(IsBlocked))
            .Select(truck => new { Truck = truck, Checks = BuildPairChecks(trip, truck) })
            .Where(item => item.Checks.Any(IsBlocked))
            .Select(item => new PlanningExcludedResource("Truck", item.Truck.AssetId, item.Truck.PlateNumber, item.Checks)));

        var candidates = new List<PlanningCandidate>();
        foreach (var driver in drivers.Where(item => !driverChecks[item.UserId].Any(IsBlocked)))
        {
            var driverWorkload = GetDailyDriverWorkload(windows, driver.UserId, pickup?.ScheduledAt);
            foreach (var truck in trucks.Where(item => !truckChecks[item.AssetId].Any(IsBlocked)))
            {
                var pairChecks = BuildPairChecks(trip, truck);
                if (pairChecks.Any(IsBlocked))
                {
                    continue;
                }

                candidates.Add(BuildCandidate(driver, truck, null, driverWorkload, driverChecks[driver.UserId], truckChecks[truck.AssetId], pairChecks));
                foreach (var trailer in trailers.Where(item => !trailerChecks[item.AssetId].Any(IsBlocked)))
                {
                    var combinationChecks = BuildTrailerCombinationChecks(trip, trailer);
                    if (combinationChecks.Any(IsBlocked))
                    {
                        continue;
                    }

                    candidates.Add(BuildCandidate(
                        driver,
                        truck,
                        trailer,
                        driverWorkload,
                        driverChecks[driver.UserId],
                        truckChecks[truck.AssetId],
                        pairChecks.Concat(trailerChecks[trailer.AssetId]).Concat(combinationChecks).ToList()));
                }
            }
        }

        return new PlanningEvaluation(
            trip,
            bookingChecks,
            excluded,
            candidates,
            candidates.Any(candidate => candidate.DriverUserId == trip.DriverUserId && candidate.TruckAssetId == trip.TruckAssetId && candidate.TrailerAssetId == trip.TrailerAssetId),
            true);
    }

    private static IReadOnlyCollection<PlanningValidationCheck> BuildBookingChecks(Trip trip, ShipmentRequest? booking, DateTime now)
    {
        var pickup = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Pickup);
        var dropoff = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Dropoff);
        var atwVerified = trip.Documents.Any(document => document.IsActive && document.Type == TripDocumentType.Atw && document.State == TripDocumentState.Verified);
        return
        [
            new("BOOKING_APPROVED", booking is not null && booking.ApprovedAt.HasValue && booking.Status != ShipmentRequestStatus.Rejected
                ? PlanningCheckState.Passed : PlanningCheckState.Blocked,
                booking is not null && booking.ApprovedAt.HasValue ? "The linked customer booking was approved." : "This trip must be linked to an approved customer booking."),
            new("SCHEDULE_VALID", pickup?.ScheduledAt is DateTime pickupAt && dropoff?.ScheduledAt is DateTime dropoffAt && pickupAt < dropoffAt
                ? PlanningCheckState.Passed : PlanningCheckState.Blocked,
                "Set a pickup time earlier than the dropoff time."),
            new("SCHEDULE_FUTURE", pickup?.ScheduledAt is DateTime scheduledPickup && scheduledPickup > now
                ? PlanningCheckState.Passed : PlanningCheckState.Blocked,
                "Schedule pickup in the future before dispatch handoff."),
            new("ROUTE_COMPLETE", !string.IsNullOrWhiteSpace(pickup?.LocationText) && !string.IsNullOrWhiteSpace(dropoff?.LocationText)
                ? PlanningCheckState.Passed : PlanningCheckState.Blocked,
                "Pickup and dropoff locations are required."),
            new("PICKUP_REACHABILITY", pickup?.Latitude.HasValue == true && pickup.Longitude.HasValue == true
                ? PlanningCheckState.Passed : PlanningCheckState.Warning,
                pickup?.Latitude.HasValue == true && pickup.Longitude.HasValue == true
                    ? "Pickup coordinates are available for reachability checks."
                    : "Pickup coordinates are missing; reachability can only be verified from the location text."),
            new("MANDATORY_DOCUMENTS", atwVerified ? PlanningCheckState.Passed : PlanningCheckState.Blocked,
                atwVerified ? "ATW is verified." : "Verify the ATW before the trip can be marked ready."),
            new("CONTAINER_NUMBER", !string.IsNullOrWhiteSpace(trip.ContainerNumber) ? PlanningCheckState.Passed : PlanningCheckState.Blocked,
                !string.IsNullOrWhiteSpace(trip.ContainerNumber) ? "Container number is set." : "Set the container number before dispatch handoff.")
        ];
    }

    private static List<PlanningValidationCheck> BuildDriverChecks(Driver driver, TripStop? pickup, TripStop? dropoff, IReadOnlyCollection<AssignmentWindow> windows)
    {
        var checks = new List<PlanningValidationCheck>
        {
            new("DRIVER_AVAILABLE", driver.User?.IsActive == true && IsOperational(driver.Status) ? PlanningCheckState.Passed : PlanningCheckState.Blocked,
                driver.User?.IsActive == true && IsOperational(driver.Status) ? "Driver is active and available." : "Driver is inactive or unavailable."),
            new("DRIVER_OVERLAP", HasOverlap(windows, driver.UserId, null, null, pickup, dropoff) ? PlanningCheckState.Blocked : PlanningCheckState.Passed,
                HasOverlap(windows, driver.UserId, null, null, pickup, dropoff) ? "Driver has an overlapping trip." : "Driver has no overlapping trip.")
        };
        return checks;
    }

    private static List<PlanningValidationCheck> BuildTruckChecks(Truck truck, Trip trip, TripStop? pickup, TripStop? dropoff, IReadOnlyCollection<AssignmentWindow> windows, IReadOnlyCollection<Guid> maintenanceAssetIds)
    {
        var active = truck.Asset?.Status == AssetStatus.Active && IsOperational(truck.Status);
        decimal? distance = pickup?.Latitude is decimal pickupLat && pickup.Longitude is decimal pickupLon && truck.LastLatitude.HasValue && truck.LastLongitude.HasValue
            ? HaversineKm(truck.LastLatitude.Value, truck.LastLongitude.Value, pickupLat, pickupLon)
            : null;
        return
        [
            new("TRUCK_OPERATIONAL", active ? PlanningCheckState.Passed : PlanningCheckState.Blocked,
                active ? "Truck is operational." : "Truck is inactive or unavailable."),
            new("TRUCK_MAINTENANCE", maintenanceAssetIds.Contains(truck.AssetId) ? PlanningCheckState.Blocked : PlanningCheckState.Passed,
                maintenanceAssetIds.Contains(truck.AssetId) ? "Truck has an open maintenance request." : "Truck has no open maintenance request."),
            new("TRUCK_OVERLAP", HasOverlap(windows, null, truck.AssetId, null, pickup, dropoff) ? PlanningCheckState.Blocked : PlanningCheckState.Passed,
                HasOverlap(windows, null, truck.AssetId, null, pickup, dropoff) ? "Truck has an overlapping trip." : "Truck has no overlapping trip."),
            new("PICKUP_UNREACHABLE", distance.HasValue && distance.Value > MaxPickupReachabilityKm ? PlanningCheckState.Blocked : distance.HasValue ? PlanningCheckState.Passed : PlanningCheckState.Warning,
                distance.HasValue ? $"Truck is {distance.Value:0.#} km from pickup." : "Truck location is unavailable; pickup reachability needs confirmation.")
        ];
    }

    private static List<PlanningValidationCheck> BuildTrailerChecks(Trailer trailer, Trip trip, TripStop? pickup, TripStop? dropoff, IReadOnlyCollection<AssignmentWindow> windows) =>
    [
        new("TRAILER_OPERATIONAL", trailer.Asset?.Status == AssetStatus.Active && IsOperational(trailer.Status) ? PlanningCheckState.Passed : PlanningCheckState.Blocked,
            trailer.Asset?.Status == AssetStatus.Active && IsOperational(trailer.Status) ? "Trailer is operational." : "Trailer is inactive or unavailable."),
        new("TRAILER_SCHEDULE_OVERLAP", HasOverlap(windows, null, null, trailer.AssetId, pickup, dropoff) ? PlanningCheckState.Blocked : PlanningCheckState.Passed,
            HasOverlap(windows, null, null, trailer.AssetId, pickup, dropoff) ? "Trailer has an overlapping trip." : "Trailer has no overlapping trip.")
    ];

    private static List<PlanningValidationCheck> BuildPairChecks(Trip trip, Truck truck)
    {
        var compatible = IsContainerCompatible(truck.ContainerCapability, trip.ContainerSize);
        return
        [
            new("EQUIPMENT_INCOMPATIBLE", compatible ? PlanningCheckState.Passed : PlanningCheckState.Blocked,
                compatible ? "Truck equipment matches the container requirement." : "Truck equipment does not match the container requirement."),
            new("TRUCK_CAPACITY", compatible ? PlanningCheckState.Passed : PlanningCheckState.Blocked,
                compatible ? "Truck capacity supports this container size." : "Truck capacity does not support this container size.")
        ];
    }

    private static List<PlanningValidationCheck> BuildTrailerCombinationChecks(Trip trip, Trailer trailer)
    {
        var compatible = IsContainerCompatible(trailer.ContainerType, trip.ContainerSize);
        return
        [
            new("TRAILER_EQUIPMENT_COMPATIBILITY", compatible ? PlanningCheckState.Passed : PlanningCheckState.Blocked,
                compatible ? "Trailer matches the container requirement." : "Trailer does not match the container requirement.")
        ];
    }

    private static PlanningCandidate BuildCandidate(
        Driver driver,
        Truck truck,
        Trailer? trailer,
        decimal driverWorkload,
        IEnumerable<PlanningValidationCheck> driverChecks,
        IEnumerable<PlanningValidationCheck> truckChecks,
        IEnumerable<PlanningValidationCheck> otherChecks)
    {
        var distance = truckChecks.FirstOrDefault(check => check.Code == "PICKUP_UNREACHABLE")?.Message;
        var distanceValue = ParseDistance(distance);
        var warnings = driverChecks.Concat(truckChecks).Concat(otherChecks)
            .Where(check => check.State == PlanningCheckState.Warning)
            .Select(check => check.Message)
            .Distinct()
            .ToList();
        return new PlanningCandidate(
            driver.UserId,
            driver.User?.Username ?? "Unnamed driver",
            truck.AssetId,
            truck.PlateNumber,
            trailer?.AssetId,
            trailer?.TrailerCode,
            distanceValue,
            driverWorkload,
            EquipmentFit(truck.ContainerCapability),
            trailer is null ? 0.75m : EquipmentFit(trailer.ContainerType),
            warnings);
    }

    private static IReadOnlyCollection<PlanningAssignmentRecommendation> RankCandidates(IReadOnlyCollection<PlanningCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var maxDistance = candidates.Max(candidate => candidate.PickupDistanceKm);
        var maxWorkload = Math.Max(1m, candidates.Max(candidate => candidate.DriverWorkload));
        var maxEquipment = Math.Max(1m, candidates.Max(candidate => candidate.EquipmentFit));
        var maxTrailer = Math.Max(1m, candidates.Max(candidate => candidate.TrailerFit));
        var rows = candidates.Select(candidate => new RankedCandidate(
            candidate,
            candidate.PickupDistanceKm / Math.Max(1m, maxDistance) * Weights["Pickup proximity"],
            candidate.DriverWorkload / maxWorkload * Weights["Driver availability"],
            candidate.EquipmentFit / maxEquipment * Weights["Equipment fit"],
            candidate.TrailerFit / maxTrailer * Weights["Trailer fit"]))
            .ToList();

        var ideal = new[] { rows.Min(row => row.Proximity), rows.Min(row => row.Availability), rows.Max(row => row.Equipment), rows.Max(row => row.Trailer) };
        var antiIdeal = new[] { rows.Max(row => row.Proximity), rows.Max(row => row.Availability), rows.Min(row => row.Equipment), rows.Min(row => row.Trailer) };
        return rows.Select(row =>
            {
                var values = new[] { row.Proximity, row.Availability, row.Equipment, row.Trailer };
                var toIdeal = Math.Sqrt(values.Select((value, index) => Math.Pow((double)(value - ideal[index]), 2)).Sum());
                var toAntiIdeal = Math.Sqrt(values.Select((value, index) => Math.Pow((double)(value - antiIdeal[index]), 2)).Sum());
                var score = toIdeal + toAntiIdeal == 0 ? 1m : (decimal)(toAntiIdeal / (toIdeal + toAntiIdeal));
                return new { Row = row, Score = decimal.Round(score, 4) };
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Row.Candidate.DriverName)
            .ThenBy(item => item.Row.Candidate.TruckCode)
            .Select((item, index) => new PlanningAssignmentRecommendation(
                index + 1,
                item.Row.Candidate.DriverUserId,
                item.Row.Candidate.DriverName,
                item.Row.Candidate.TruckAssetId,
                item.Row.Candidate.TruckCode,
                item.Row.Candidate.TrailerAssetId,
                item.Row.Candidate.TrailerCode,
                item.Score,
                [
                    new("Pickup proximity", "Cost", Weights["Pickup proximity"], item.Row.Candidate.PickupDistanceKm, item.Row.Proximity, $"{item.Row.Candidate.PickupDistanceKm:0.#} km to pickup."),
                    new("Driver availability", "Cost", Weights["Driver availability"], item.Row.Candidate.DriverWorkload, item.Row.Availability, "No overlapping driver work is scheduled."),
                    new("Equipment fit", "Benefit", Weights["Equipment fit"], item.Row.Candidate.EquipmentFit, item.Row.Equipment, "Truck capacity and container equipment match."),
                    new("Trailer fit", "Benefit", Weights["Trailer fit"], item.Row.Candidate.TrailerFit, item.Row.Trailer, item.Row.Candidate.TrailerCode is null ? "No trailer is required for this option." : "Trailer equipment matches.")
                ],
                [
                    $"Driver {item.Row.Candidate.DriverName} and truck {item.Row.Candidate.TruckCode} satisfy all hard constraints.",
                    item.Row.Candidate.TrailerCode is null ? "Uses no trailer assignment." : $"Includes trailer {item.Row.Candidate.TrailerCode}."
                ],
                item.Row.Candidate.Warnings))
            .ToList();
    }

    private async Task<List<AssignmentWindow>> LoadAssignmentWindowsAsync(Guid excludedTripId, CancellationToken cancellationToken)
    {
        var windows = await _dbContext.DispatchTrips.AsNoTracking()
            .Where(trip => trip.Id != excludedTripId && trip.Status != TripStatus.Cancelled && trip.Status != TripStatus.Closed)
            .Select(trip => new AssignmentWindow(
                trip.DriverUserId,
                trip.TruckAssetId,
                trip.TrailerAssetId,
                trip.Stops.Where(stop => stop.StopType == TripStopType.Pickup).Select(stop => stop.ScheduledAt).FirstOrDefault(),
                trip.Stops.Where(stop => stop.StopType == TripStopType.Dropoff).Select(stop => stop.ScheduledAt).FirstOrDefault()))
            .ToListAsync(cancellationToken);
        return windows.Where(window => window.Start.HasValue && window.End.HasValue).ToList();
    }

    private static bool HasOverlap(IReadOnlyCollection<AssignmentWindow> windows, Guid? driverId, Guid? truckId, Guid? trailerId, TripStop? pickup, TripStop? dropoff)
    {
        if (pickup?.ScheduledAt is not DateTime start || dropoff?.ScheduledAt is not DateTime end || start >= end)
        {
            return false;
        }
        return windows.Any(window => window.Start < end && window.End > start &&
            ((driverId.HasValue && window.DriverUserId == driverId) ||
             (truckId.HasValue && window.TruckAssetId == truckId) ||
             (trailerId.HasValue && window.TrailerAssetId == trailerId)));
    }

    private static decimal GetDailyDriverWorkload(IReadOnlyCollection<AssignmentWindow> windows, Guid driverUserId, DateTime? pickupAt) =>
        pickupAt is not DateTime date ? 0m : windows.Count(window =>
            window.DriverUserId == driverUserId && window.Start?.Date == date.Date);

    private static bool IsOperational(string status) =>
        string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Available", StringComparison.OrdinalIgnoreCase);

    private static bool IsContainerCompatible(string? capability, string? containerSize)
    {
        if (string.IsNullOrWhiteSpace(capability) || string.IsNullOrWhiteSpace(containerSize)) return true;
        var cap = capability.ToUpperInvariant();
        var size = containerSize.ToUpperInvariant();
        return cap.Contains("ANY") || cap.Contains("ALL") || cap.Contains("UNKNOWN") ||
               (RequiresTwentyFootContainer(size) && cap.Contains("20")) ||
               (RequiresFortyFootContainer(size) && cap.Contains("40"));
    }

    // Shipment requests persist ContainerSize as enum text (for example, FortyFt),
    // while fleet capabilities are operator-entered text (for example, 40ft capable).
    // Support both forms so planning evaluates the same operational requirement.
    private static bool RequiresTwentyFootContainer(string size) =>
        size.Contains("20") || size.Contains("TWENTY");

    private static bool RequiresFortyFootContainer(string size) =>
        size.Contains("40") || size.Contains("FORTY");

    private static decimal EquipmentFit(string? capability) => string.IsNullOrWhiteSpace(capability) || capability.Contains("ANY", StringComparison.OrdinalIgnoreCase) ? 0.85m : 1m;

    private static decimal HaversineKm(decimal latitudeA, decimal longitudeA, decimal latitudeB, decimal longitudeB)
    {
        const double radiusKm = 6371d;
        var latDelta = DegreesToRadians((double)(latitudeB - latitudeA));
        var lonDelta = DegreesToRadians((double)(longitudeB - longitudeA));
        var a = Math.Sin(latDelta / 2) * Math.Sin(latDelta / 2) +
                Math.Cos(DegreesToRadians((double)latitudeA)) * Math.Cos(DegreesToRadians((double)latitudeB)) *
                Math.Sin(lonDelta / 2) * Math.Sin(lonDelta / 2);
        return decimal.Round((decimal)(radiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a))), 2);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
    private static bool IsBlocked(PlanningValidationCheck check) => check.State == PlanningCheckState.Blocked;
    private static decimal ParseDistance(string? message)
    {
        var values = message?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return values.Length > 2 && decimal.TryParse(values[2], out var distance) ? distance : 250m;
    }

    private static byte[] DecodeRowVersion(string value)
    {
        try { return Convert.FromBase64String(value); }
        catch (FormatException) { throw new BusinessRuleViolationException("A valid trip version is required. Refresh planning and try again."); }
    }

    private sealed record AssignmentWindow(Guid? DriverUserId, Guid? TruckAssetId, Guid? TrailerAssetId, DateTime? Start, DateTime? End);
    private sealed record PlanningCandidate(Guid DriverUserId, string DriverName, Guid TruckAssetId, string TruckCode, Guid? TrailerAssetId, string? TrailerCode, decimal PickupDistanceKm, decimal DriverWorkload, decimal EquipmentFit, decimal TrailerFit, IReadOnlyCollection<string> Warnings);
    private sealed record RankedCandidate(PlanningCandidate Candidate, decimal Proximity, decimal Availability, decimal Equipment, decimal Trailer);
    private static bool HasResourceEvaluationBlocker(IEnumerable<PlanningValidationCheck> checks) =>
        checks.Any(check => check.State == PlanningCheckState.Blocked && check.Code is
            "BOOKING_APPROVED" or "SCHEDULE_VALID" or "SCHEDULE_FUTURE" or "ROUTE_COMPLETE");

    private sealed record PlanningEvaluation(Trip Trip, IReadOnlyCollection<PlanningValidationCheck> BookingChecks, IReadOnlyCollection<PlanningExcludedResource> ExcludedResources, IReadOnlyCollection<PlanningCandidate> Candidates, bool CurrentAssignmentIsFeasible, bool ResourcesEvaluated);
}
