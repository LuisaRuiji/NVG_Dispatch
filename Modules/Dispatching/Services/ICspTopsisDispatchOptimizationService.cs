using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Models;

namespace NVGInventory.Modules.Dispatching.Services;

public interface ICspTopsisDispatchOptimizationService
{
    Task<DispatchSearchResult> SolveAsync(
        List<Trip> trips,
        List<Truck> trucks,
        List<Driver> drivers,
        OptimizationWeightSettings weights,
        SearchParameters parameters,
        CancellationToken cancellationToken = default);
}

public sealed class SearchParameters
{
    public int MaxIterations { get; set; } = 1000;
    public int MaxCandidatesPerState { get; set; } = 10;
    public int MaxExecutionSeconds { get; set; } = 10;
}

public sealed class DispatchSearchResult
{
    public DispatchSearchState BestState { get; set; } = new();
    public bool IsCompleteSolution { get; set; }
    public int SearchIterations { get; set; }
    public List<ExcludedTripInfo> ExcludedTrips { get; set; } = new();
    public List<UnassignedTripInfo> UnassignedTrips { get; set; } = new();
}

public sealed class ExcludedTripInfo
{
    public Guid TripId { get; set; }
    public IReadOnlyCollection<string> Reasons { get; set; } = Array.Empty<string>();
}

public sealed class UnassignedTripInfo
{
    public Guid TripId { get; set; }
    public string Reason { get; set; } = "No feasible truck-driver pair found within search limits.";
}
