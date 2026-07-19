using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NVGInventory.Modules.Dispatching.Services;

public interface IDispatchCspValidationService
{
    Task<CspValidationResult> ValidateCandidateAsync(
        Guid tripId,
        Guid truckId,
        Guid driverId,
        DateTime? selectedDate,
        CancellationToken cancellationToken);

    Task<CheckCspResponse> CheckAllCandidatesAsync(
        Guid tripId,
        DateTime? selectedDate,
        CancellationToken cancellationToken);
}

public sealed record CspValidationResult(
    bool IsFeasible,
    IReadOnlyCollection<string> FailureReasons);

public sealed record CheckCspResponse(
    Guid TripId,
    bool IsTripEligible,
    IReadOnlyCollection<string> TripFailureReasons,
    IReadOnlyCollection<CandidateFeasibilityResult> Candidates);

public sealed record CandidateFeasibilityResult(
    Guid TruckId,
    string PlateNumber,
    Guid DriverId,
    string DriverName,
    bool IsFeasible,
    IReadOnlyCollection<string> FailureReasons);
