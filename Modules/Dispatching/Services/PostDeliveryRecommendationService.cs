using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Models;
using Microsoft.Extensions.Logging;

namespace NVGInventory.Modules.Dispatching.Services;

public interface IPostDeliveryRecommendationService
{
    Task<List<DispatchRecommendation>> GenerateRecommendationsAsync(Guid tripId, CancellationToken cancellationToken = default);
}

public sealed class PostDeliveryRecommendationService : IPostDeliveryRecommendationService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IDispatchCspValidationService _cspValidationService;
    private readonly ITravelTimeService _travelTimeService;
    private readonly IGeocodingService? _geocodingService;
    private readonly ILogger<PostDeliveryRecommendationService> _logger;

    public PostDeliveryRecommendationService(
        InventoryDbContext dbContext,
        IDispatchCspValidationService cspValidationService,
        ITravelTimeService travelTimeService,
        ILogger<PostDeliveryRecommendationService> logger,
        IGeocodingService? geocodingService = null)
    {
        _dbContext = dbContext;
        _cspValidationService = cspValidationService;
        _travelTimeService = travelTimeService;
        _logger = logger;
        _geocodingService = geocodingService;
    }

    public async Task<List<DispatchRecommendation>> GenerateRecommendationsAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        var result = new List<DispatchRecommendation>();

        _logger.LogWarning($"[REC-DEBUG] Triggered Post-Delivery Recommendation for Trip: {tripId}");
        
        var completedTrip = await _dbContext.DispatchTrips
            .Include(t => t.Stops)
            .Include(t => t.TruckAsset)
            .Include(t => t.Driver)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (completedTrip == null || completedTrip.TruckAsset == null || completedTrip.Driver == null)
        {
            _logger.LogWarning($"[REC-DEBUG] Aborting: completedTrip is null ({completedTrip == null}), TruckAsset is null ({completedTrip?.TruckAsset == null}), or Driver is null ({completedTrip?.Driver == null}).");
            return result;
        }

        var lastStop = completedTrip.Stops
            .LastOrDefault(s => s.StopType == TripStopType.Dropoff);

        decimal currentLat = 7.0707m;
        decimal currentLon = 125.6103m;

        if (lastStop != null)
        {
            if (lastStop.Latitude.HasValue && lastStop.Longitude.HasValue)
            {
                currentLat = lastStop.Latitude.Value;
                currentLon = lastStop.Longitude.Value;
            }
            else if (!string.IsNullOrWhiteSpace(lastStop.LocationText) && _geocodingService != null)
            {
                var g = await _geocodingService.GeocodeAddressAsync(lastStop.LocationText, cancellationToken);
                if (g != null)
                {
                    currentLat = (decimal)g.Latitude;
                    currentLon = (decimal)g.Longitude;
                }
            }
        }

        var unassignedTrips = await _dbContext.DispatchTrips
            .Include(t => t.Stops)
            .Where(t => t.Status == TripStatus.Draft)
            .ToListAsync(cancellationToken);

        var candidateMoves = new List<CandidateTopsisMove>();

        foreach (var candidateTrip in unassignedTrips)
        {
            _logger.LogWarning($"[REC-DEBUG] Evaluating candidate draft trip {candidateTrip.Id}");
            var pickup = candidateTrip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Pickup);
            var dropoff = candidateTrip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Dropoff);

            if (pickup == null || dropoff == null)
            {
                _logger.LogWarning($"[REC-DEBUG] Candidate {candidateTrip.Id} rejected: Missing pickup or dropoff stop.");
                continue;
            }

            decimal pickupLat = 7.0707m, pickupLon = 125.6103m;
            decimal dropoffLat = 7.0707m, dropoffLon = 125.6103m;

            if (pickup.Latitude.HasValue && pickup.Longitude.HasValue)
            {
                pickupLat = pickup.Latitude.Value;
                pickupLon = pickup.Longitude.Value;
            }
            else if (!string.IsNullOrWhiteSpace(pickup.LocationText) && _geocodingService != null)
            {
                var pG = await _geocodingService.GeocodeAddressAsync(pickup.LocationText, cancellationToken);
                if (pG != null) { pickupLat = (decimal)pG.Latitude; pickupLon = (decimal)pG.Longitude; }
            }

            if (dropoff.Latitude.HasValue && dropoff.Longitude.HasValue)
            {
                dropoffLat = dropoff.Latitude.Value;
                dropoffLon = dropoff.Longitude.Value;
            }
            else if (!string.IsNullOrWhiteSpace(dropoff.LocationText) && _geocodingService != null)
            {
                var dG = await _geocodingService.GeocodeAddressAsync(dropoff.LocationText, cancellationToken);
                if (dG != null) { dropoffLat = (decimal)dG.Latitude; dropoffLon = (decimal)dG.Longitude; }
            }

            // 1. CSP Hard Constraint Validation
            var validationResult = await _cspValidationService.ValidateCandidateAsync(
                candidateTrip.Id, completedTrip.TruckAsset.Id, completedTrip.Driver.Id, null, cancellationToken);

            if (!validationResult.IsFeasible) 
            {
                _logger.LogWarning($"[REC-DEBUG] Candidate {candidateTrip.Id} failed CSP Hard Constraints: {string.Join(", ", validationResult.FailureReasons)}");
                continue;
            }

            _logger.LogWarning($"[REC-DEBUG] Candidate {candidateTrip.Id} passed CSP constraints! Calculating TOPSIS...");

            // 2. Compute 6 Thesis Criteria
            DateTime simulatedTime = DateTime.UtcNow;

            // C1: Deadhead Distance
            var emptyTravel = await _travelTimeService.EstimateTravelAsync(currentLat, currentLon, pickupLat, pickupLon, cancellationToken);
            decimal deadheadDistance = emptyTravel.DistanceKm;
            simulatedTime = simulatedTime.AddMinutes(emptyTravel.TravelMinutes);

            // C2: Cleaning Time
            decimal cleaningTime = 15m; 
            simulatedTime = simulatedTime.AddMinutes((double)cleaningTime);

            // C3: Waiting Time
            decimal waitingTime = 0m;
            if (pickup.ScheduledAt.HasValue && simulatedTime < pickup.ScheduledAt.Value)
            {
                waitingTime = (decimal)(pickup.ScheduledAt.Value - simulatedTime).TotalMinutes;
            }

            // C4: Job Urgency
            decimal jobUrgency = GetTripPriority(candidateTrip);

            // C5: Cargo Compatibility
            decimal cargoCompat = 8m; 

            // C6: Asset Utilization
            decimal assetUtilization = 9m;

            candidateMoves.Add(new CandidateTopsisMove
            {
                Trip = candidateTrip,
                DeadheadDistance = deadheadDistance,
                CleaningTime = cleaningTime,
                WaitingTime = waitingTime,
                JobUrgency = jobUrgency,
                CargoCompatibility = cargoCompat,
                AssetUtilization = assetUtilization,
                EmptyTravelMins = emptyTravel.TravelMinutes
            });
        }

        if (candidateMoves.Count == 0) return result;

        // Fetch Weights
        var weights = await _dbContext.OptimizationWeightSettings
            .Where(w => w.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (weights == null)
        {
            weights = new OptimizationWeightSettings
            {
                DeadheadDistanceWeight = 0.25m,
                CleaningTimeWeight = 0.15m,
                WaitingTimeWeight = 0.15m,
                JobUrgencyWeight = 0.20m,
                CargoCompatibilityWeight = 0.15m,
                AssetUtilizationWeight = 0.10m
            };
        }

        // 3. Normalize & Weight (Eq 1)
        decimal sumSqDeadhead = (decimal)Math.Sqrt((double)candidateMoves.Sum(m => m.DeadheadDistance * m.DeadheadDistance));
        decimal sumSqCleaning = (decimal)Math.Sqrt((double)candidateMoves.Sum(m => m.CleaningTime * m.CleaningTime));
        decimal sumSqWaiting = (decimal)Math.Sqrt((double)candidateMoves.Sum(m => m.WaitingTime * m.WaitingTime));
        decimal sumSqUrgency = (decimal)Math.Sqrt((double)candidateMoves.Sum(m => m.JobUrgency * m.JobUrgency));
        decimal sumSqCargo = (decimal)Math.Sqrt((double)candidateMoves.Sum(m => m.CargoCompatibility * m.CargoCompatibility));
        decimal sumSqAsset = (decimal)Math.Sqrt((double)candidateMoves.Sum(m => m.AssetUtilization * m.AssetUtilization));

        var normalizedMatrix = candidateMoves.Select(m => new CandidateTopsisMove
        {
            Trip = m.Trip,
            DeadheadDistance = sumSqDeadhead > 0 ? (m.DeadheadDistance / sumSqDeadhead) * weights.DeadheadDistanceWeight : 0,
            CleaningTime = sumSqCleaning > 0 ? (m.CleaningTime / sumSqCleaning) * weights.CleaningTimeWeight : 0,
            WaitingTime = sumSqWaiting > 0 ? (m.WaitingTime / sumSqWaiting) * weights.WaitingTimeWeight : 0,
            JobUrgency = sumSqUrgency > 0 ? (m.JobUrgency / sumSqUrgency) * weights.JobUrgencyWeight : 0,
            CargoCompatibility = sumSqCargo > 0 ? (m.CargoCompatibility / sumSqCargo) * weights.CargoCompatibilityWeight : 0,
            AssetUtilization = sumSqAsset > 0 ? (m.AssetUtilization / sumSqAsset) * weights.AssetUtilizationWeight : 0,
            EmptyTravelMins = m.EmptyTravelMins,
            RawDeadheadDistance = m.DeadheadDistance
        }).ToList();

        // 4. Ideal/Negative Ideal
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

        // 5 & 6. Distances & Closeness Coefficient
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

        var topCandidates = normalizedMatrix
            .OrderByDescending(m => m.ClosenessCoefficient)
            .Take(3)
            .ToList();

        // Expire any previously pending recommendations for this driver or truck so the UI shows fresh results for the latest completed trip
        var existingPending = await _dbContext.DispatchRecommendations
            .Where(r => (r.TruckId == completedTrip.TruckAsset.Id || r.DriverId == completedTrip.Driver.Id) && !r.WasAccepted && !r.WasIgnored)
            .ToListAsync(cancellationToken);
        foreach (var old in existingPending)
        {
            old.ExpiresAt = DateTime.UtcNow; // mark as expired
        }

        foreach (var move in topCandidates)
        {
            var rec = new DispatchRecommendation
            {
                RecommendedTripId = move.Trip.Id,
                CompletedTripId = tripId,
                DriverId = completedTrip.Driver.Id,
                TruckId = completedTrip.TruckAsset.Id,
                TotalScore = move.ClosenessCoefficient,
                ProximityScore = move.ClosenessCoefficient,
                AvailabilityScore = 0m,
                TruckMatchScore = 0m,
                AgingScore = 0m,
                Rank = result.Count + 1,
                GeneratedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };
            _dbContext.DispatchRecommendations.Add(rec);
            result.Add(rec);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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
        public Trip Trip { get; set; } = null!;
        public decimal DeadheadDistance { get; set; }
        public decimal CleaningTime { get; set; }
        public decimal WaitingTime { get; set; }
        public decimal JobUrgency { get; set; }
        public decimal CargoCompatibility { get; set; }
        public decimal AssetUtilization { get; set; }
        public decimal ClosenessCoefficient { get; set; }
        public int EmptyTravelMins { get; set; }
        public decimal RawDeadheadDistance { get; set; }
    }
}
