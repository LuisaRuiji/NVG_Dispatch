using System;
using System.Threading;
using System.Threading.Tasks;

namespace NVGInventory.Modules.Dispatching.Services;

public interface ITravelTimeService
{
    Task<(decimal DistanceKm, int TravelMinutes)> EstimateTravelAsync(
        decimal fromLat, decimal fromLon,
        decimal toLat, decimal toLon,
        CancellationToken cancellationToken = default);
}
