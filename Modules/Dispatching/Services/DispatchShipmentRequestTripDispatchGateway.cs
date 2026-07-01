using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.ShipmentRequests.Services;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed class DispatchShipmentRequestTripDispatchGateway : IShipmentRequestTripDispatchGateway
{
    private readonly ITripLifecycleService _tripLifecycleService;

    public DispatchShipmentRequestTripDispatchGateway(ITripLifecycleService tripLifecycleService)
    {
        _tripLifecycleService = tripLifecycleService;
    }

    public async Task<Guid> CreateDraftTripAsync(
        ShipmentRequestTripDraftData tripDraft,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var lifecycleCommand = new CreateDispatchTripCommand(
            tripDraft.CustomerId,
            null,
            null,
            "Dropoff time is a placeholder - update before dispatching.",
            new[]
            {
                new DispatchTripStopInput(
                    TripStopType.Pickup,
                    tripDraft.PickupLocation,
                    tripDraft.PickupTime),
                new DispatchTripStopInput(
                    TripStopType.Dropoff,
                    tripDraft.DropoffLocation,
                    tripDraft.PickupTime.AddHours(1))
            },
            null,
            tripDraft.ContainerNumber,
            null,
            tripDraft.BookingNumber,
            tripDraft.ShippingLine,
            tripDraft.ContainerSize,
            tripDraft.TripType);

        var trip = await _tripLifecycleService.CreateDraftAsync(lifecycleCommand, actor, cancellationToken);
        return trip.Id;
    }
}
