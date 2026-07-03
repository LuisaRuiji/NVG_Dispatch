using NVGInventory.Modules.Dispatching.Services;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed record ShipmentRequestTripDraftData(
    Guid CustomerId,
    string PickupLocation,
    string DropoffLocation,
    DateTime PickupTime,
    string? ContainerNumber,
    string? BookingNumber,
    string? ShippingLine,
    string? ContainerSize,
    string? TripType,
    double? PickupLatitude = null,
    double? PickupLongitude = null,
    double? DropoffLatitude = null,
    double? DropoffLongitude = null);

public interface IShipmentRequestTripDispatchGateway
{
    Task<Guid> CreateDraftTripAsync(
        ShipmentRequestTripDraftData tripDraft,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);
}
