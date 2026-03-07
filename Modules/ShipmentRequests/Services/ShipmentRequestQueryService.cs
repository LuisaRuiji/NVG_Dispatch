using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
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
    IReadOnlyCollection<TripStop> Stops);

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

    public ShipmentRequestQueryService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
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
        var query = _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(trip => trip.CustomerId == customerId);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(trip => trip.UpdatedAt ?? trip.CreatedAt)
            .ThenByDescending(trip => trip.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(trip => new CustomerShipmentListItem(
                trip.Id,
                _dbContext.DispatchTripStops
                    .Where(stop => stop.TripId == trip.Id && stop.StopType == TripStopType.Pickup)
                    .Select(stop => stop.LocationText)
                    .FirstOrDefault() ?? string.Empty,
                _dbContext.DispatchTripStops
                    .Where(stop => stop.TripId == trip.Id && stop.StopType == TripStopType.Dropoff)
                    .Select(stop => stop.LocationText)
                    .FirstOrDefault() ?? string.Empty,
                trip.Status,
                _dbContext.DispatchTripStops
                    .Where(stop => stop.TripId == trip.Id && stop.StopType == TripStopType.Pickup)
                    .Select(stop => stop.ScheduledAt)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStatusHistories
                    .Where(history => history.TripId == trip.Id && history.ToStatus == TripStatus.Delivered)
                    .OrderByDescending(history => history.EventAt)
                    .Select(history => (DateTime?)history.EventAt)
                    .FirstOrDefault(),
                _dbContext.DispatchTripDocuments
                    .Where(doc => doc.TripId == trip.Id && doc.IsActive && doc.Type == TripDocumentType.Pod)
                    .Select(doc => (TripDocumentState?)doc.State)
                    .FirstOrDefault() ?? TripDocumentState.Missing))
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<CustomerShipmentListItem>(items, total);
    }

    public async Task<CustomerShipmentDetail> GetCustomerShipmentDetailAsync(
        Guid tripId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Include(t => t.Stops)
            .FirstOrDefaultAsync(t => t.Id == tripId && t.CustomerId == customerId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Shipment not found.");
        }

        var pickupLocation = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Pickup)?.LocationText
            ?? string.Empty;
        var dropoffLocation = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Dropoff)?.LocationText
            ?? string.Empty;
        var pickupTime = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Pickup)?.ScheduledAt;
        var dropoffTime = trip.Stops.FirstOrDefault(stop => stop.StopType == TripStopType.Dropoff)?.ScheduledAt;
        var deliveredTime = await _dbContext.DispatchTripStatusHistories
            .AsNoTracking()
            .Where(history => history.TripId == trip.Id && history.ToStatus == TripStatus.Delivered)
            .OrderByDescending(history => history.EventAt)
            .Select(history => (DateTime?)history.EventAt)
            .FirstOrDefaultAsync(cancellationToken);
        var podState = await _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Where(doc => doc.TripId == trip.Id && doc.IsActive && doc.Type == TripDocumentType.Pod)
            .Select(doc => (TripDocumentState?)doc.State)
            .FirstOrDefaultAsync(cancellationToken) ?? TripDocumentState.Missing;

        return new CustomerShipmentDetail(
            trip.Id,
            trip.Status,
            pickupLocation,
            dropoffLocation,
            pickupTime,
            dropoffTime,
            deliveredTime,
            podState,
            trip.Stops.OrderBy(stop => stop.StopType).ToList());
    }

    public async Task<IReadOnlyCollection<CustomerShipmentTimelineEntry>> GetCustomerShipmentTimelineAsync(
        Guid tripId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var tripExists = await _dbContext.DispatchTrips
            .AsNoTracking()
            .AnyAsync(trip => trip.Id == tripId && trip.CustomerId == customerId, cancellationToken);

        if (!tripExists)
        {
            throw new NotFoundException("Shipment not found.");
        }

        var items = await _dbContext.DispatchTripStatusHistories
            .AsNoTracking()
            .Where(history => history.TripId == tripId && history.EventType == TripHistoryEventType.StatusChange)
            .OrderBy(history => history.EventAt)
            .Select(history => new CustomerShipmentTimelineEntry(
                history.FromStatus,
                history.ToStatus,
                history.EventAt))
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task<IReadOnlyCollection<CustomerShipmentDocument>> GetCustomerShipmentDocumentsAsync(
        Guid tripId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var tripExists = await _dbContext.DispatchTrips
            .AsNoTracking()
            .AnyAsync(trip => trip.Id == tripId && trip.CustomerId == customerId, cancellationToken);

        if (!tripExists)
        {
            throw new NotFoundException("Shipment not found.");
        }

        var docs = await _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Where(doc => doc.TripId == tripId && doc.IsActive && doc.Type == TripDocumentType.Pod)
            .OrderByDescending(doc => doc.UploadedAt)
            .Select(doc => new CustomerShipmentDocument(
                doc.Type,
                doc.State,
                doc.StorageKey,
                doc.UploadedAt))
            .ToListAsync(cancellationToken);

        return docs;
    }
}
