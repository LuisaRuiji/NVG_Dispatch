using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Models;

namespace NVGInventory.Modules.Dispatching.Services;

public interface IOptimizationScoringService
{
    Task CalculateStateCostAsync(
        DispatchSearchState state,
        Dictionary<Guid, Trip> allTrips,
        OptimizationWeightSettings weights,
        CancellationToken cancellationToken = default);
}
