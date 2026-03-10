using NVGInventory.Modules.Dispatching.Services;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed record ShipmentRequestTripDraftData(
    Guid CustomerId,
    string PickupLocation,
    string DropoffLocation,
    DateTime PickupTime);

public interface IShipmentRequestTripDispatchGateway
{
    Task<Guid> CreateDraftTripAsync(
        ShipmentRequestTripDraftData tripDraft,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);
}
