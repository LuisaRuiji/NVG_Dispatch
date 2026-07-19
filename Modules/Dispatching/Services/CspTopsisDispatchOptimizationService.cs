using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Models;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed class CspTopsisDispatchOptimizationService : ICspTopsisDispatchOptimizationService
{
    private readonly IDispatchCspValidationService _cspValidationService;
    private readonly IOptimizationScoringService _scoringService;
    private readonly ITravelTimeService _travelTimeService;

    // Default company yard coordinates (Manila area)
    private const decimal DefaultLatitude = 14.5995m;
    private const decimal DefaultLongitude = 120.9842m;

    public CspTopsisDispatchOptimizationService(
        IDispatchCspValidationService cspValidationService,
        IOptimizationScoringService scoringService,
        ITravelTimeService travelTimeService)
    {
        _cspValidationService = cspValidationService;
        _scoringService = scoringService;
        _travelTimeService = travelTimeService;
    }

    public async Task<DispatchSearchResult> SolveAsync(
        List<Trip> trips,
        List<Truck> trucks,
        List<Driver> drivers,
        OptimizationWeightSettings weights,
        SearchParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DispatchSearchResult();

        // Pre-filter trips
        var eligibleTrips = new List<Trip>();
        foreach (var trip in trips)
        {
            var reasons = new List<string>();

            var hasPickup = trip.Stops.Any(s => s.StopType == TripStopType.Pickup);
            var hasDropoff = trip.Stops.Any(s => s.StopType == TripStopType.Dropoff);

            if (!hasPickup) reasons.Add("Missing pickup stop");
            if (!hasDropoff) reasons.Add("Missing drop-off stop");

            if (hasPickup && hasDropoff)
            {
                var missingCoords = trip.Stops
                    .Where(s => s.StopType == TripStopType.Pickup || s.StopType == TripStopType.Dropoff)
                    .Any(s => !s.Latitude.HasValue || !s.Longitude.HasValue);
                if (missingCoords) reasons.Add("Missing pickup/drop-off coordinates");
            }

            if (reasons.Count > 0)
            {
                result.ExcludedTrips.Add(new ExcludedTripInfo { TripId = trip.Id, Reasons = reasons });
            }
            else
            {
                eligibleTrips.Add(trip);
            }
        }

        if (eligibleTrips.Count == 0)
        {
            result.IsCompleteSolution = true;
            result.SearchIterations = 0;
            return result;
        }

        var allTripsDict = eligibleTrips.ToDictionary(t => t.Id);
        var unassignedTripIds = eligibleTrips.Select(t => t.Id).ToList();

        var state = new DispatchSearchState
        {
            UnassignedTripIds = unassignedTripIds.ToList(),
            AssignedTripIds = new List<Guid>(),
            Depth = 0
        };

        // Pair each truck with a driver
        int pairCount = Math.Min(trucks.Count, drivers.Count);
        var activeTruckIds = new List<Guid>();

        for (int i = 0; i < pairCount; i++)
        {
            var truck = trucks[i];
            var driver = drivers[i];
            var route = new DispatchSearchRoute
            {
                DispatchTruckId = truck.Id,
                DispatchDriverId = driver.Id,
                CurrentLatitude = truck.LastLatitude ?? DefaultLatitude,
                CurrentLongitude = truck.LastLongitude ?? DefaultLongitude,
                EstimatedTravelMinutes = 0,
                EstimatedDistanceKm = 0m,
                EstimatedEmptyMileageKm = 0m,
                OrderedTripIds = new List<Guid>()
            };
            state.RoutesByTruck[truck.Id] = route;
            state.CurrentLocationByTruck[truck.Id] = (route.CurrentLatitude, route.CurrentLongitude);
            activeTruckIds.Add(truck.Id);
        }

        int iterations = 0;

        // Greedy TOPSIS Trip Chaining
        while (state.UnassignedTripIds.Count > 0 && iterations < parameters.MaxIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (stopwatch.Elapsed.TotalSeconds >= parameters.MaxExecutionSeconds)
                break;

            iterations++;

            var candidateMoves = new List<CandidateTopsisMove>();

            // 1. Generate all feasible (Truck, Trip) combinations
            foreach (var tripId in state.UnassignedTripIds)
            {
                var trip = allTripsDict[tripId];
                var pickup = trip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Pickup);
                var dropoff = trip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Dropoff);
                
                if (pickup?.Latitude == null || pickup?.Longitude == null || dropoff?.Latitude == null || dropoff?.Longitude == null)
                    continue;

                foreach (var truckId in activeTruckIds)
                {
                    var route = state.RoutesByTruck[truckId];

                    // CSP Validation (Hard Constraints)
                    var validation = await _cspValidationService.ValidateCandidateAsync(
                        trip.Id, route.DispatchTruckId, route.DispatchDriverId, null, cancellationToken);

                    if (!validation.IsFeasible) continue;

                    // Compute the 6 Thesis Criteria
                    decimal currentLat = state.CurrentLocationByTruck[truckId].Lat;
                    decimal currentLon = state.CurrentLocationByTruck[truckId].Lon;
                    DateTime simulatedTime = DateTime.UtcNow.AddMinutes(route.EstimatedTravelMinutes);

                    // C1: Deadhead Distance (Cost)
                    var emptyTravel = await _travelTimeService.EstimateTravelAsync(currentLat, currentLon, pickup.Latitude.Value, pickup.Longitude.Value, cancellationToken);
                    decimal deadheadDistance = emptyTravel.DistanceKm;
                    simulatedTime = simulatedTime.AddMinutes(emptyTravel.TravelMinutes);

                    // C2: Cleaning Time (Cost)
                    decimal cleaningTime = 15m; // Standard 15 mins for container inspection/cleaning
                    
                    // C3: Waiting Time (Cost)
                    decimal waitingTime = 0m;
                    if (pickup.ScheduledAt.HasValue && simulatedTime < pickup.ScheduledAt.Value)
                    {
                        waitingTime = (decimal)(pickup.ScheduledAt.Value - simulatedTime).TotalMinutes;
                    }

                    // C4: Job Urgency (Benefit)
                    decimal jobUrgency = GetTripPriority(trip);

                    // C5: Cargo Compatibility (Benefit)
                    decimal cargoCompat = 8m; // Default to good score, modify if trip metadata is available

                    // C6: Asset Utilization (Benefit)
                    decimal assetUtilization = 9m; // Assume good utilization

                    candidateMoves.Add(new CandidateTopsisMove
                    {
                        TripId = tripId,
                        TruckId = truckId,
                        DeadheadDistance = deadheadDistance,
                        CleaningTime = cleaningTime,
                        WaitingTime = waitingTime,
                        JobUrgency = jobUrgency,
                        CargoCompatibility = cargoCompat,
                        AssetUtilization = assetUtilization,
                        
                        EmptyTravelMins = emptyTravel.TravelMinutes,
                        DropoffLat = dropoff.Latitude.Value,
                        DropoffLon = dropoff.Longitude.Value
                    });
                }
            }

            if (candidateMoves.Count == 0)
            {
                // No feasible moves left for any truck
                break;
            }

            // 2. TOPSIS Matrix Normalization (Eq 1)
            decimal sumSqDeadhead = (decimal)Math.Sqrt((double)candidateMoves.Sum(m => m.DeadheadDistance * m.DeadheadDistance));
            decimal sumSqCleaning = (decimal)Math.Sqrt((double)candidateMoves.Sum(m => m.CleaningTime * m.CleaningTime));
            decimal sumSqWaiting = (decimal)Math.Sqrt((double)candidateMoves.Sum(m => m.WaitingTime * m.WaitingTime));
            decimal sumSqUrgency = (decimal)Math.Sqrt((double)candidateMoves.Sum(m => m.JobUrgency * m.JobUrgency));
            decimal sumSqCargo = (decimal)Math.Sqrt((double)candidateMoves.Sum(m => m.CargoCompatibility * m.CargoCompatibility));
            decimal sumSqAsset = (decimal)Math.Sqrt((double)candidateMoves.Sum(m => m.AssetUtilization * m.AssetUtilization));

            var normalizedMatrix = candidateMoves.Select(m => new CandidateTopsisMove
            {
                TripId = m.TripId,
                TruckId = m.TruckId,
                DeadheadDistance = sumSqDeadhead > 0 ? (m.DeadheadDistance / sumSqDeadhead) * weights.DeadheadDistanceWeight : 0,
                CleaningTime = sumSqCleaning > 0 ? (m.CleaningTime / sumSqCleaning) * weights.CleaningTimeWeight : 0,
                WaitingTime = sumSqWaiting > 0 ? (m.WaitingTime / sumSqWaiting) * weights.WaitingTimeWeight : 0,
                JobUrgency = sumSqUrgency > 0 ? (m.JobUrgency / sumSqUrgency) * weights.JobUrgencyWeight : 0,
                CargoCompatibility = sumSqCargo > 0 ? (m.CargoCompatibility / sumSqCargo) * weights.CargoCompatibilityWeight : 0,
                AssetUtilization = sumSqAsset > 0 ? (m.AssetUtilization / sumSqAsset) * weights.AssetUtilizationWeight : 0,
                EmptyTravelMins = m.EmptyTravelMins,
                DropoffLat = m.DropoffLat,
                DropoffLon = m.DropoffLon
            }).ToList();

            // 3. Identify Ideal/Negative-Ideal
            // Costs: DeadheadDistance, CleaningTime, WaitingTime
            // Benefit: JobUrgency, CargoCompatibility, AssetUtilization
            decimal idealDeadhead = normalizedMatrix.Min(m => m.DeadheadDistance);
            decimal idealCleaning = normalizedMatrix.Min(m => m.CleaningTime);
            decimal idealWaiting = normalizedMatrix.Min(m => m.WaitingTime);
            decimal idealUrgency = normalizedMatrix.Max(m => m.JobUrgency);
            decimal idealCargo = normalizedMatrix.Max(m => m.CargoCompatibility);
            decimal idealAsset = normalizedMatrix.Max(m => m.AssetUtilization);

            decimal negIdealDeadhead = normalizedMatrix.Max(m => m.DeadheadDistance);
            decimal negIdealCleaning = normalizedMatrix.Max(m => m.CleaningTime);
            decimal negIdealWaiting = normalizedMatrix.Max(m => m.WaitingTime);
            decimal negIdealUrgency = normalizedMatrix.Min(m => m.JobUrgency);
            decimal negIdealCargo = normalizedMatrix.Min(m => m.CargoCompatibility);
            decimal negIdealAsset = normalizedMatrix.Min(m => m.AssetUtilization);

            // 4. Compute Distances & Closeness Coefficient (Eq 2)
            foreach (var norm in normalizedMatrix)
            {
                decimal distIdealSq = 
                    (norm.DeadheadDistance - idealDeadhead) * (norm.DeadheadDistance - idealDeadhead) +
                    (norm.CleaningTime - idealCleaning) * (norm.CleaningTime - idealCleaning) +
                    (norm.WaitingTime - idealWaiting) * (norm.WaitingTime - idealWaiting) +
                    (norm.JobUrgency - idealUrgency) * (norm.JobUrgency - idealUrgency) +
                    (norm.CargoCompatibility - idealCargo) * (norm.CargoCompatibility - idealCargo) +
                    (norm.AssetUtilization - idealAsset) * (norm.AssetUtilization - idealAsset);

                decimal distNegSq = 
                    (norm.DeadheadDistance - negIdealDeadhead) * (norm.DeadheadDistance - negIdealDeadhead) +
                    (norm.CleaningTime - negIdealCleaning) * (norm.CleaningTime - negIdealCleaning) +
                    (norm.WaitingTime - negIdealWaiting) * (norm.WaitingTime - negIdealWaiting) +
                    (norm.JobUrgency - negIdealUrgency) * (norm.JobUrgency - negIdealUrgency) +
                    (norm.CargoCompatibility - negIdealCargo) * (norm.CargoCompatibility - negIdealCargo) +
                    (norm.AssetUtilization - negIdealAsset) * (norm.AssetUtilization - negIdealAsset);

                decimal distIdeal = (decimal)Math.Sqrt((double)distIdealSq);
                decimal distNeg = (decimal)Math.Sqrt((double)distNegSq);

                if (distIdeal + distNeg > 0)
                {
                    norm.ClosenessCoefficient = distNeg / (distIdeal + distNeg);
                }
            }

            // 5. Pick Best Move
            var bestMove = normalizedMatrix.OrderByDescending(m => m.ClosenessCoefficient).First();

            // 6. Apply Move to State
            var targetRoute = state.RoutesByTruck[bestMove.TruckId];
            targetRoute.OrderedTripIds.Add(bestMove.TripId);
            
            var rawBestMove = candidateMoves.First(m => m.TripId == bestMove.TripId && m.TruckId == bestMove.TruckId);
            
            // We just estimate time via distance for state tracking
            targetRoute.EstimatedTravelMinutes += (int)(rawBestMove.EmptyTravelMins + 60m); 
            targetRoute.EstimatedDistanceKm += (rawBestMove.DeadheadDistance + 20m);
            targetRoute.EstimatedEmptyMileageKm += rawBestMove.DeadheadDistance;
            targetRoute.CurrentLatitude = rawBestMove.DropoffLat;
            targetRoute.CurrentLongitude = rawBestMove.DropoffLon;
            
            state.CurrentLocationByTruck[bestMove.TruckId] = (rawBestMove.DropoffLat, rawBestMove.DropoffLon);
            state.UnassignedTripIds.Remove(bestMove.TripId);
            state.AssignedTripIds.Add(bestMove.TripId);
            state.Depth++;
        }

        // Calculate final state cost
        await _scoringService.CalculateStateCostAsync(state, allTripsDict, weights, cancellationToken);

        result.BestState = state;
        result.IsCompleteSolution = state.UnassignedTripIds.Count == 0;
        result.SearchIterations = iterations;

        foreach (var tripId in state.UnassignedTripIds)
        {
            result.UnassignedTrips.Add(new UnassignedTripInfo { TripId = tripId });
        }

        return result;
    }

    private static decimal GetTripPriority(Trip trip)
    {
        if (string.Equals(trip.TripType, "Urgent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trip.TripType, "High", StringComparison.OrdinalIgnoreCase) ||
            (trip.Notes != null && trip.Notes.Contains("urgent", StringComparison.OrdinalIgnoreCase)))
        {
            return 10m;
        }
        return 5m;
    }

    private sealed class CandidateTopsisMove
    {
        public Guid TripId { get; set; }
        public Guid TruckId { get; set; }
        
        // 6 Criteria
        public decimal DeadheadDistance { get; set; }
        public decimal CleaningTime { get; set; }
        public decimal WaitingTime { get; set; }
        public decimal JobUrgency { get; set; }
        public decimal CargoCompatibility { get; set; }
        public decimal AssetUtilization { get; set; }

        public decimal ClosenessCoefficient { get; set; }

        public int EmptyTravelMins { get; set; }
        public decimal DropoffLat { get; set; }
        public decimal DropoffLon { get; set; }
    }
}
