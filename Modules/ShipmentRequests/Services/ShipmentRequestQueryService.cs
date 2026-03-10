using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Modules.ShipmentRequests.Entities;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed record ShipmentRequestListItem(
    Guid Id,
    ShipmentRequestStatus Status,
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    int DocumentsCount,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    Guid? ConvertedTripId);

public sealed record ShipmentRequestDetail(
    Guid Id,
    ShipmentRequestStatus Status,
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    string? CargoDescription,
    decimal? CargoWeight,
    string? SpecialInstructions,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    Guid? ConvertedTripId,
    IReadOnlyCollection<ShipmentRequestDocument> Documents);

public sealed record DispatchShipmentRequestQueueItem(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    int DocumentsCount,
    DateTime CreatedAt);

public sealed record CustomerShipmentListItem(
    Guid TripId,
    string PickupLocation,
    string DropoffLocation,
    TripStatus Status,
    DateTime? PickupTime,
    DateTime? DeliveredTime,
    TripDocumentState PodState);

public sealed record CustomerShipmentDetail(
    Guid TripId,
    TripStatus Status,
    string PickupLocation,
    string DropoffLocation,
    DateTime? PickupTime,
    DateTime? DropoffTime,
    DateTime? DeliveredTime,
    TripDocumentState PodState,
    IReadOnlyCollection<CustomerShipmentStop> Stops);

public sealed record CustomerShipmentStop(
    TripStopType StopType,
    string LocationText,
    DateTime? ScheduledAt,
    DateTime? ActualAt);

public sealed record CustomerShipmentTimelineEntry(
    TripStatus FromStatus,
    TripStatus ToStatus,
    DateTime EventAt);

public sealed record CustomerShipmentDocument(
    TripDocumentType Type,
    TripDocumentState State,
    string StorageKey,
    DateTime UploadedAt);

public sealed class ShipmentRequestQueryService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IDispatchShipmentReadService _dispatchShipmentReadService;

    public ShipmentRequestQueryService(
        InventoryDbContext dbContext,
        IDispatchShipmentReadService dispatchShipmentReadService)
    {
        _dbContext = dbContext;
        _dispatchShipmentReadService = dispatchShipmentReadService;
    }

    public async Task<PagedQueryResult<ShipmentRequestListItem>> GetCustomerRequestsAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ShipmentRequests
            .AsNoTracking()
            .Where(request => request.CustomerId == customerId);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(request => request.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(request => new ShipmentRequestListItem(
                request.Id,
                request.Status,
                request.PickupLocation,
                request.DropoffLocation,
                request.RequestedPickupTime,
                _dbContext.ShipmentRequestDocuments.Count(doc => doc.RequestId == request.Id),
                request.CreatedAt,
                request.ApprovedAt,
                request.ConvertedTripId))
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<ShipmentRequestListItem>(items, total);
    }

    public async Task<ShipmentRequestDetail> GetCustomerRequestDetailAsync(
        Guid requestId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ShipmentRequests
            .AsNoTracking()
            .Include(r => r.Documents)
            .ThenInclude(doc => doc.UploadedByUser)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.CustomerId == customerId, cancellationToken);

        if (request is null)
        {
            throw new NotFoundException("Shipment request not found.");
        }

        return new ShipmentRequestDetail(
            request.Id,
            request.Status,
            request.PickupLocation,
            request.DropoffLocation,
            request.RequestedPickupTime,
            request.CargoDescription,
            request.CargoWeight,
            request.SpecialInstructions,
            request.CreatedAt,
            request.ApprovedAt,
            request.ConvertedTripId,
            request.Documents.OrderByDescending(d => d.UploadedAt).ToList());
    }

    public async Task<PagedQueryResult<DispatchShipmentRequestQueueItem>> GetDispatchQueueAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ShipmentRequests
            .AsNoTracking()
            .Include(request => request.Customer)
            .Where(request => request.Status == ShipmentRequestStatus.Submitted);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(request => request.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(request => new DispatchShipmentRequestQueueItem(
                request.Id,
                request.CustomerId,
                request.Customer != null ? request.Customer.Name : string.Empty,
                request.PickupLocation,
                request.DropoffLocation,
                request.RequestedPickupTime,
                _dbContext.ShipmentRequestDocuments.Count(doc => doc.RequestId == request.Id),
                request.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<DispatchShipmentRequestQueueItem>(items, total);
    }

    public async Task<PagedQueryResult<CustomerShipmentListItem>> GetCustomerShipmentsAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatchShipmentReadService.GetCustomerShipmentsAsync(
            customerId,
            page,
            pageSize,
            cancellationToken);

        return new PagedQueryResult<CustomerShipmentListItem>(
            result.Items.Select(MapListItem).ToList(),
            result.TotalCount);
    }

    public async Task<CustomerShipmentDetail> GetCustomerShipmentDetailAsync(
        Guid tripId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var detail = await _dispatchShipmentReadService.GetCustomerShipmentDetailAsync(
            tripId,
            customerId,
            cancellationToken);

        return MapDetail(detail);
    }

    public async Task<IReadOnlyCollection<CustomerShipmentTimelineEntry>> GetCustomerShipmentTimelineAsync(
        Guid tripId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _dispatchShipmentReadService.GetCustomerShipmentTimelineAsync(
            tripId,
            customerId,
            cancellationToken);
        return entries.Select(MapTimelineEntry).ToList();
    }

    public async Task<IReadOnlyCollection<CustomerShipmentDocument>> GetCustomerShipmentDocumentsAsync(
        Guid tripId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var docs = await _dispatchShipmentReadService.GetCustomerShipmentDocumentsAsync(
            tripId,
            customerId,
            cancellationToken);
        return docs.Select(MapDocument).ToList();
    }

    private static CustomerShipmentListItem MapListItem(DispatchCustomerShipmentListItem item)
    {
        return new CustomerShipmentListItem(
            item.TripId,
            item.PickupLocation,
            item.DropoffLocation,
            item.Status,
            item.PickupTime,
            item.DeliveredTime,
            item.PodState);
    }

    private static CustomerShipmentDetail MapDetail(DispatchCustomerShipmentDetail detail)
    {
        return new CustomerShipmentDetail(
            detail.TripId,
            detail.Status,
            detail.PickupLocation,
            detail.DropoffLocation,
            detail.PickupTime,
            detail.DropoffTime,
            detail.DeliveredTime,
            detail.PodState,
            detail.Stops.Select(stop => new CustomerShipmentStop(
                stop.StopType,
                stop.LocationText,
                stop.ScheduledAt,
                stop.ActualAt)).ToList());
    }

    private static CustomerShipmentTimelineEntry MapTimelineEntry(DispatchCustomerShipmentTimelineEntry entry)
    {
        return new CustomerShipmentTimelineEntry(
            entry.FromStatus,
            entry.ToStatus,
            entry.EventAt);
    }

    private static CustomerShipmentDocument MapDocument(DispatchCustomerShipmentDocument document)
    {
        return new CustomerShipmentDocument(
            document.Type,
            document.State,
            document.StorageKey,
            document.UploadedAt);
    }
}
