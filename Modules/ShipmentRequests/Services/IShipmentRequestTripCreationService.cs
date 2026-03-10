using NVGInventory.Modules.Dispatching.Services;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed record CreateTripFromShipmentRequestCommand(
    Guid ShipmentRequestId,
    DateTime? ScheduledPickupTimeOverride = null);

public interface IShipmentRequestTripCreationService
{
    Task<Guid> CreateDraftTripFromApprovedRequestAsync(
        CreateTripFromShipmentRequestCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);
}
