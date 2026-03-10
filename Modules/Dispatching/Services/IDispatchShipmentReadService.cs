using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record DispatchCustomerShipmentListItem(
    Guid TripId,
    string PickupLocation,
    string DropoffLocation,
    TripStatus Status,
    DateTime? PickupTime,
    DateTime? DeliveredTime,
    TripDocumentState PodState);

public sealed record DispatchCustomerShipmentStop(
    TripStopType StopType,
    string LocationText,
    DateTime? ScheduledAt,
    DateTime? ActualAt);

public sealed record DispatchCustomerShipmentDetail(
    Guid TripId,
    TripStatus Status,
    string PickupLocation,
    string DropoffLocation,
    DateTime? PickupTime,
    DateTime? DropoffTime,
    DateTime? DeliveredTime,
    TripDocumentState PodState,
    IReadOnlyCollection<DispatchCustomerShipmentStop> Stops);

public sealed record DispatchCustomerShipmentTimelineEntry(
    TripStatus FromStatus,
    TripStatus ToStatus,
    DateTime EventAt);

public sealed record DispatchCustomerShipmentDocument(
    TripDocumentType Type,
    TripDocumentState State,
    string StorageKey,
    DateTime UploadedAt);

public interface IDispatchShipmentReadService
{
    Task<PagedQueryResult<DispatchCustomerShipmentListItem>> GetCustomerShipmentsAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<DispatchCustomerShipmentDetail> GetCustomerShipmentDetailAsync(
        Guid tripId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DispatchCustomerShipmentTimelineEntry>> GetCustomerShipmentTimelineAsync(
        Guid tripId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DispatchCustomerShipmentDocument>> GetCustomerShipmentDocumentsAsync(
        Guid tripId,
        Guid customerId,
        CancellationToken cancellationToken = default);
}
