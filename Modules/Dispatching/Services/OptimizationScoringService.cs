using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Models;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed class OptimizationScoringService : IOptimizationScoringService
{
    private readonly ITravelTimeService _travelTimeService;

    public OptimizationScoringService(ITravelTimeService travelTimeService)
    {
        _travelTimeService = travelTimeService;
    }

    public async Task CalculateStateCostAsync(
        DispatchSearchState state,
        Dictionary<Guid, Trip> allTrips,
        OptimizationWeightSettings weights,
        CancellationToken cancellationToken = default)
    {
        if (state.RoutesByTruck.Count == 0) return;

        // Note: For state scoring (overall plan evaluation), we aggregate the 6 criteria
        // across all routes in the plan to determine the total "Closeness Coefficient" of the plan.

        decimal totalDeadheadKm = 0m;
        decimal totalCleaningMins = 0m;
        decimal totalWaitingMins = 0m;
        decimal totalJobUrgency = 0m;
        decimal totalCargoCompatibility = 0m;
        decimal totalAssetUtilization = 0m;

        int activeTrucks = 0;

        foreach (var routeKvp in state.RoutesByTruck)
        {
            var route = routeKvp.Value;
            if (route.OrderedTripIds.Count == 0) continue;

            activeTrucks++;
            decimal routeDeadheadKm = 0m;
            decimal routeCleaningMins = 0m;
            decimal routeWaitingMins = 0m;
            decimal routeUrgency = 0m;
            decimal routeCargoCompat = 0m;

            var truckLoc = state.CurrentLocationByTruck[route.DispatchTruckId];
            decimal currentLat = truckLoc.Lat;
            decimal currentLon = truckLoc.Lon;
            DateTime simulatedTime = DateTime.UtcNow;

            for (int i = 0; i < route.OrderedTripIds.Count; i++)
            {
                var trip = allTrips[route.OrderedTripIds[i]];
                var pickup = trip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Pickup);
                var dropoff = trip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Dropoff);

                if (pickup?.Latitude == null || pickup?.Longitude == null || dropoff?.Latitude == null || dropoff?.Longitude == null)
                    continue;

                // 1. Deadhead Distance
                var emptyTravel = await _travelTimeService.EstimateTravelAsync(currentLat, currentLon, pickup.Latitude.Value, pickup.Longitude.Value, cancellationToken);
                routeDeadheadKm += emptyTravel.DistanceKm;
                
                simulatedTime = simulatedTime.AddMinutes(emptyTravel.TravelMinutes);

                // 2. Waiting Time
                if (pickup.ScheduledAt.HasValue && simulatedTime < pickup.ScheduledAt.Value)
                {
                    routeWaitingMins += (decimal)(pickup.ScheduledAt.Value - simulatedTime).TotalMinutes;
                    simulatedTime = pickup.ScheduledAt.Value;
                }

                // 3. Cleaning Time
                // Simulated: Assume 15 mins average cleaning time per container
                routeCleaningMins += 15m;
                simulatedTime = simulatedTime.AddMinutes(15);

                // 4. Job Urgency
                routeUrgency += GetTripPriority(trip);

                // 5. Cargo Compatibility
                // Simulated: Higher score if trip type matches previous or is standard
                routeCargoCompat += 8m; // Default good compatibility

                // Update location and time for loaded travel
                var loadedTravel = await _travelTimeService.EstimateTravelAsync(pickup.Latitude.Value, pickup.Longitude.Value, dropoff.Latitude.Value, dropoff.Longitude.Value, cancellationToken);
                simulatedTime = simulatedTime.AddMinutes(loadedTravel.TravelMinutes);
                
                // Dropoff time (simulated unloading)
                simulatedTime = simulatedTime.AddMinutes(30);

                currentLat = dropoff.Latitude.Value;
                currentLon = dropoff.Longitude.Value;
            }

            totalDeadheadKm += routeDeadheadKm;
            totalCleaningMins += routeCleaningMins;
            totalWaitingMins += routeWaitingMins;
            totalJobUrgency += routeUrgency;
            totalCargoCompatibility += routeCargoCompat;
        }

        // 6. Asset Utilization (Trucks used vs trips assigned)
        if (state.AssignedTripIds.Count > 0)
        {
            totalAssetUtilization = (decimal)state.AssignedTripIds.Count / (activeTrucks > 0 ? activeTrucks : 1) * 10m;
        }

        // To calculate a single TotalScore (Closeness Coefficient) for the state, we need Ideal/NegIdeal bounds.
        // For the sake of the aggregate score, we'll use a simplified weighted sum since TOPSIS is meant for comparing alternatives, not scoring a final state.
        
        decimal normalizedDeadhead = NormalizeCost(totalDeadheadKm, 0m, 500m);
        decimal normalizedCleaning = NormalizeCost(totalCleaningMins, 0m, 300m);
        decimal normalizedWaiting = NormalizeCost(totalWaitingMins, 0m, 600m);
        decimal normalizedUrgency = NormalizeBenefit(totalJobUrgency, 0m, 50m);
        decimal normalizedCargo = NormalizeBenefit(totalCargoCompatibility, 0m, 50m);
        decimal normalizedAsset = NormalizeBenefit(totalAssetUtilization, 0m, 100m);

        decimal rawScore = 
            (normalizedDeadhead * weights.DeadheadDistanceWeight) +
            (normalizedCleaning * weights.CleaningTimeWeight) +
            (normalizedWaiting * weights.WaitingTimeWeight) +
            (normalizedUrgency * weights.JobUrgencyWeight) +
            (normalizedCargo * weights.CargoCompatibilityWeight) +
            (normalizedAsset * weights.AssetUtilizationWeight);

        state.StateCost = Math.Round(rawScore * 100m, 2); // 0-100 scale

        // Set detailed breakdown for debugging
        state.EstimatedTotalEmptyMileageKm = totalDeadheadKm;
    }

    private decimal NormalizeCost(decimal value, decimal minExpected, decimal maxExpected)
    {
        if (value <= minExpected) return 1.0m;
        if (value >= maxExpected) return 0.0m;
        return 1.0m - ((value - minExpected) / (maxExpected - minExpected));
    }

    private decimal NormalizeBenefit(decimal value, decimal minExpected, decimal maxExpected)
    {
        if (value <= minExpected) return 0.0m;
        if (value >= maxExpected) return 1.0m;
        return (value - minExpected) / (maxExpected - minExpected);
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
}
