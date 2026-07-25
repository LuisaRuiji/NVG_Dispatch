using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Enums;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed class DispatchCspValidationService : IDispatchCspValidationService
{
    private readonly InventoryDbContext _dbContext;

    public DispatchCspValidationService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CspValidationResult> ValidateCandidateAsync(
        Guid tripId,
        Guid truckId,
        Guid driverId,
        DateTime? selectedDate,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.DispatchTrips
            .Include(t => t.Stops)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip == null)
        {
            return new CspValidationResult(false, new[] { "Trip not found" });
        }

        var tripReasons = GetTripFailureReasons(trip, selectedDate);
        if (tripReasons.Count > 0)
        {
            return new CspValidationResult(false, tripReasons);
        }

        var truck = await _dbContext.DispatchTrucks
            .Include(t => t.Asset)
            .FirstOrDefaultAsync(t => t.AssetId == truckId, cancellationToken);

        if (truck == null)
        {
            return new CspValidationResult(false, new[] { "Truck not found" });
        }

        var driver = await _dbContext.DispatchDrivers
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == driverId, cancellationToken);

        if (driver == null)
        {
            return new CspValidationResult(false, new[] { "Driver not found" });
        }

        var reasons = new List<string>();

        // Truck constraints
        var truckReasons = await GetTruckFailureReasonsAsync(truck, trip, cancellationToken);
        reasons.AddRange(truckReasons);

        // Driver constraints
        var driverReasons = await GetDriverFailureReasonsAsync(driver, trip, cancellationToken);
        reasons.AddRange(driverReasons);

        return new CspValidationResult(reasons.Count == 0, reasons);
    }

    public async Task<CheckCspResponse> CheckAllCandidatesAsync(
        Guid tripId,
        DateTime? selectedDate,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.DispatchTrips
            .Include(t => t.Stops)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip == null)
        {
            return new CheckCspResponse(tripId, false, new[] { "Trip not found" }, Array.Empty<CandidateFeasibilityResult>());
        }

        var tripFailureReasons = GetTripFailureReasons(trip, selectedDate);
        var isTripEligible = tripFailureReasons.Count == 0;

        var trucks = await _dbContext.DispatchTrucks
            .Include(t => t.Asset)
            .ToListAsync(cancellationToken);

        var drivers = await _dbContext.DispatchDrivers
            .Include(d => d.User)
            .ToListAsync(cancellationToken);

        var candidates = new List<CandidateFeasibilityResult>();

        foreach (var truck in trucks)
        {
            foreach (var driver in drivers)
            {
                var failureReasons = new List<string>();

                if (!isTripEligible)
                {
                    failureReasons.AddRange(tripFailureReasons);
                }
                else
                {
                    var truckReasons = await GetTruckFailureReasonsAsync(truck, trip, cancellationToken);
                    failureReasons.AddRange(truckReasons);

                    var driverReasons = await GetDriverFailureReasonsAsync(driver, trip, cancellationToken);
                    failureReasons.AddRange(driverReasons);
                }

                candidates.Add(new CandidateFeasibilityResult(
                    truck.Id,
                    truck.PlateNumber,
                    driver.Id,
                    driver.User?.Username ?? "Unknown Driver",
                    failureReasons.Count == 0,
                    failureReasons));
            }
        }

        return new CheckCspResponse(tripId, isTripEligible, tripFailureReasons, candidates);
    }

    private List<string> GetTripFailureReasons(Trip trip, DateTime? selectedDate)
    {
        var reasons = new List<string>();

        // 1. Status checks
        if (trip.Status == TripStatus.Delivered)
        {
            reasons.Add("Trip already completed");
        }
        else if (trip.Status == TripStatus.Closed)
        {
            reasons.Add("Trip closed");
        }
        else if (trip.Status == TripStatus.Cancelled)
        {
            reasons.Add("Trip cancelled");
        }
        else if (trip.Status == TripStatus.Dispatched ||
                 trip.Status == TripStatus.EnroutePickup ||
                 trip.Status == TripStatus.AtPickup ||
                 trip.Status == TripStatus.Loaded ||
                 trip.Status == TripStatus.EnrouteDropoff ||
                 trip.Status == TripStatus.AtDropoff)
        {
            reasons.Add("Trip already dispatched");
        }

        // 2. Stop checks
        var hasPickup = trip.Stops.Any(s => s.StopType == TripStopType.Pickup);
        var hasDropoff = trip.Stops.Any(s => s.StopType == TripStopType.Dropoff);

        if (!hasPickup)
        {
            reasons.Add("Missing pickup stop");
        }

        if (!hasDropoff)
        {
            reasons.Add("Missing drop-off stop");
        }

        if (hasPickup && hasDropoff)
        {
            var missingCoords = trip.Stops
                .Where(s => s.StopType == TripStopType.Pickup || s.StopType == TripStopType.Dropoff)
                .Any(s => !s.Latitude.HasValue || !s.Longitude.HasValue);

            if (missingCoords)
            {
                reasons.Add("Missing pickup/drop-off coordinates");
            }
        }

        // 3. Date check
        if (selectedDate.HasValue)
        {
            var hasMatchingDate = trip.Stops.Any(s => s.ScheduledAt.HasValue && s.ScheduledAt.Value.Date == selectedDate.Value.Date);
            if (!hasMatchingDate)
            {
                reasons.Add("Trip not scheduled for selected date");
            }
        }

        return reasons;
    }

    private async Task<List<string>> GetTruckFailureReasonsAsync(Truck truck, Trip trip, CancellationToken cancellationToken)
    {
        var reasons = new List<string>();

        // 1. Status check
        if (!string.Equals(truck.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Truck unavailable");
        }

        // 2. Maintenance check
        if (truck.Asset != null && truck.Asset.Status == AssetStatus.Inactive)
        {
            reasons.Add("Truck under maintenance");
        }
        else
        {
            var hasActiveMaintenanceRequest = await _dbContext.Requests
                .AnyAsync(r => r.AssetId == truck.AssetId &&
                               r.RequestType == RequestType.MaintenanceIssue &&
                               r.Status != RequestStatus.Closed &&
                               r.Status != RequestStatus.Rejected, cancellationToken);

            if (hasActiveMaintenanceRequest)
            {
                reasons.Add("Truck under maintenance");
            }
        }

        // 3. Container capability check
        if (!string.Equals(truck.ContainerCapability, trip.ContainerSize, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Truck container capability mismatch");
        }

        // 4. AssetId check
        if (truck.AssetId == Guid.Empty)
        {
            reasons.Add("Truck has no asset ID");
        }

        // 5. Already assigned to another active trip
        var isAssignedToOtherActive = await _dbContext.DispatchTrips
            .AnyAsync(t => t.Id != trip.Id &&
                           t.TruckAssetId == truck.AssetId &&
                           t.Status != TripStatus.Draft &&
                           t.Status != TripStatus.Delivered &&
                           t.Status != TripStatus.Closed &&
                           t.Status != TripStatus.Cancelled, cancellationToken);

        if (isAssignedToOtherActive)
        {
            reasons.Add("Truck already assigned to active trip");
        }

        return reasons;
    }

    private async Task<List<string>> GetDriverFailureReasonsAsync(Driver driver, Trip trip, CancellationToken cancellationToken)
    {
        var reasons = new List<string>();

        // 1. Status check
        if (!string.Equals(driver.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Driver unavailable");
        }

        // 2. UserId check
        if (driver.UserId == Guid.Empty)
        {
            reasons.Add("Driver has no user ID");
        }

        // 3. Already assigned to another active trip
        var isAssignedToOtherActive = await _dbContext.DispatchTrips
            .AnyAsync(t => t.Id != trip.Id &&
                           t.DriverUserId == driver.UserId &&
                           t.Status != TripStatus.Draft &&
                           t.Status != TripStatus.Delivered &&
                           t.Status != TripStatus.Closed &&
                           t.Status != TripStatus.Cancelled, cancellationToken);

        if (isAssignedToOtherActive)
        {
            reasons.Add("Driver already assigned to active trip");
        }

        return reasons;
    }
}
