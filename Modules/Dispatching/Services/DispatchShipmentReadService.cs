using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed class DispatchShipmentReadService : IDispatchShipmentReadService
{
    private readonly InventoryDbContext _dbContext;

    public DispatchShipmentReadService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedQueryResult<DispatchCustomerShipmentListItem>> GetCustomerShipmentsAsync(
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
            .Select(trip => new DispatchCustomerShipmentListItem(
                trip.Id,
                trip.ContainerNumber,
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

        return new PagedQueryResult<DispatchCustomerShipmentListItem>(items, total);
    }

    public async Task<DispatchCustomerShipmentDetail> GetCustomerShipmentDetailAsync(
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
        var atwState = await _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Where(doc => doc.TripId == trip.Id && doc.IsActive && doc.Type == TripDocumentType.Atw)
            .Select(doc => (TripDocumentState?)doc.State)
            .FirstOrDefaultAsync(cancellationToken) ?? TripDocumentState.Missing;
        var waybillGenerated = await _dbContext.GeneratedWaybills
            .AsNoTracking()
            .AnyAsync(waybill => waybill.TripId == trip.Id && waybill.IsActive, cancellationToken);

        return new DispatchCustomerShipmentDetail(
            trip.Id,
            trip.ContainerNumber,
            trip.Status,
            pickupLocation,
            dropoffLocation,
            pickupTime,
            dropoffTime,
            deliveredTime,
            podState,
            atwState,
            waybillGenerated,
            trip.Stops
                .OrderBy(stop => stop.StopType)
                .Select(stop => new DispatchCustomerShipmentStop(
                    stop.StopType,
                    stop.LocationText,
                    stop.ScheduledAt,
                    stop.ActualAt))
                .ToList());
    }

    public async Task<IReadOnlyCollection<DispatchCustomerShipmentTimelineEntry>> GetCustomerShipmentTimelineAsync(
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
            .Select(history => new DispatchCustomerShipmentTimelineEntry(
                history.FromStatus,
                history.ToStatus,
                history.EventAt))
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task<IReadOnlyCollection<DispatchCustomerShipmentDocument>> GetCustomerShipmentDocumentsAsync(
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

        var visibleTypes = new[] { TripDocumentType.Atw, TripDocumentType.Pod };
        var docs = await _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Where(doc => doc.TripId == tripId && doc.IsActive && visibleTypes.Contains(doc.Type))
            .OrderByDescending(doc => doc.UploadedAt)
            .Select(doc => new DispatchCustomerShipmentDocument(
                doc.Type,
                doc.State,
                doc.StorageKey,
                doc.UploadedAt))
            .ToListAsync(cancellationToken);

        return docs;
    }
}
