using NVGInventory.Modules.Dispatching.Services;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed record ShipmentRequestTripDraftData(
    Guid CustomerId,
    string PickupLocation,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    string DropoffLocation,
    decimal? DropoffLatitude,
    decimal? DropoffLongitude,
    DateTime? ScheduledPickupTime,
    string? ContainerNumber,
    string? BookingNumber,
    string? ShippingLine,
    string? ContainerSize,
    string? TripType);

public interface IShipmentRequestTripDispatchGateway
{
    Task<Guid> CreateDraftTripAsync(
        ShipmentRequestTripDraftData tripDraft,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);
}
