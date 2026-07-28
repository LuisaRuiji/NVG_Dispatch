using Microsoft.AspNetCore.SignalR;
using NVGInventory.Hubs;
using NVGInventory.Hubs.Events;

namespace NVGInventory.Modules.Dispatching.Services;

public interface IPlanningAvailabilityNotifier
{
    Task InvalidateAsync(string reason, Guid? tripId = null, CancellationToken cancellationToken = default);
}

public sealed class PlanningAvailabilityNotifier : IPlanningAvailabilityNotifier
{
    private readonly PlanningRecommendationSnapshotStore _snapshotStore;
    private readonly IHubContext<VaiaDispatchHub, IVaiaDispatchClient> _hubContext;

    public PlanningAvailabilityNotifier(
        PlanningRecommendationSnapshotStore snapshotStore,
        IHubContext<VaiaDispatchHub, IVaiaDispatchClient> hubContext)
    {
        _snapshotStore = snapshotStore;
        _hubContext = hubContext;
    }

    public async Task InvalidateAsync(string reason, Guid? tripId = null, CancellationToken cancellationToken = default)
    {
        var availabilityVersion = _snapshotStore.InvalidateAll();
        await _hubContext.Clients.Group(VaiaDispatchHub.DispatchOpsGroup)
            .PlanningInvalidated(new PlanningInvalidatedEvent(tripId, reason, availabilityVersion, DateTime.UtcNow));
    }
}
