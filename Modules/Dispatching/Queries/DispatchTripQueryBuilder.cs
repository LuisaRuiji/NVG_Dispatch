using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;

namespace NVGInventory.Modules.Dispatching.Queries;

public sealed class DispatchTripQueryBuilder
{
    private readonly InventoryDbContext _dbContext;
    private readonly DispatchingOptions _options;

    public DispatchTripQueryBuilder(InventoryDbContext dbContext, DispatchingOptions options)
    {
        _dbContext = dbContext;
        _options = options;
    }

    public IQueryable<Trip> Base(DispatchActorContext actor)
    {
        var query = _dbContext.DispatchTrips
            .AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Driver)
            .Include(t => t.TruckAsset)
            .AsQueryable();

        if (!actor.IsPrivileged)
        {
            query = query.Where(t => t.DriverUserId == actor.UserId);
        }

        return query;
    }

    public IQueryable<Trip> FilterStatus(IQueryable<Trip> query, TripStatus? status)
    {
        return status.HasValue ? query.Where(t => t.Status == status.Value) : query;
    }

    public IQueryable<Trip> FilterDriver(IQueryable<Trip> query, Guid? driverUserId)
    {
        return driverUserId.HasValue ? query.Where(t => t.DriverUserId == driverUserId.Value) : query;
    }

    public IQueryable<Trip> FilterTruck(IQueryable<Trip> query, Guid? truckAssetId)
    {
        return truckAssetId.HasValue ? query.Where(t => t.TruckAssetId == truckAssetId.Value) : query;
    }

    public IQueryable<Trip> FilterCustomer(IQueryable<Trip> query, Guid? customerId)
    {
        return customerId.HasValue ? query.Where(t => t.CustomerId == customerId.Value) : query;
    }

    public IQueryable<Trip> FilterPickupRange(IQueryable<Trip> query, DateTime? from, DateTime? to)
    {
        if (from.HasValue)
        {
            query = query.Where(t =>
                t.Stops.Any(s =>
                    s.StopType == TripStopType.Pickup &&
                    s.ScheduledAt.HasValue &&
                    s.ScheduledAt.Value >= from.Value));
        }

        if (to.HasValue)
        {
            query = query.Where(t =>
                t.Stops.Any(s =>
                    s.StopType == TripStopType.Pickup &&
                    s.ScheduledAt.HasValue &&
                    s.ScheduledAt.Value <= to.Value));
        }

        return query;
    }

    public IQueryable<Trip> FilterDeliveredRange(IQueryable<Trip> query, DateTime? from, DateTime? to)
    {
        if (from.HasValue)
        {
            query = query.Where(t => _dbContext.DispatchTripStatusHistories.Any(h =>
                h.TripId == t.Id &&
                h.ToStatus == TripStatus.Delivered &&
                h.EventAt >= from.Value));
        }

        if (to.HasValue)
        {
            query = query.Where(t => _dbContext.DispatchTripStatusHistories.Any(h =>
                h.TripId == t.Id &&
                h.ToStatus == TripStatus.Delivered &&
                h.EventAt <= to.Value));
        }

        return query;
    }

    public IQueryable<Trip> FilterPodStatus(IQueryable<Trip> query, DispatchPodStatusFilter? podStatus)
    {
        if (!podStatus.HasValue)
        {
            return query;
        }

        if (podStatus.Value == DispatchPodStatusFilter.Verified)
        {
            return query.Where(t => _dbContext.DispatchTripDocuments.Any(d =>
                d.TripId == t.Id &&
                d.IsActive &&
                d.Type == TripDocumentType.Pod &&
                d.State == TripDocumentState.Verified));
        }

        var pendingQuery = query.Where(t => t.Status == TripStatus.Delivered);
        if (_options.DocVerificationEnabled)
        {
            return pendingQuery.Where(t => !_dbContext.DispatchTripDocuments.Any(d =>
                d.TripId == t.Id &&
                d.IsActive &&
                d.Type == TripDocumentType.Pod &&
                d.State == TripDocumentState.Verified));
        }

        return pendingQuery
            .Where(t => !t.PodPending)
            .Where(t => !_dbContext.DispatchTripDocuments.Any(d =>
                d.TripId == t.Id &&
                d.IsActive &&
                d.Type == TripDocumentType.Pod &&
                (d.State == TripDocumentState.Uploaded || d.State == TripDocumentState.Verified)));
    }

    public IQueryable<Trip> Active(IQueryable<Trip> query)
    {
        return query.Where(t =>
            t.Status != TripStatus.Draft &&
            t.Status != TripStatus.ReadyForDispatch &&
            t.Status != TripStatus.Closed &&
            t.Status != TripStatus.Cancelled);
    }

    public IQueryable<Trip> ReadyForDispatch(IQueryable<Trip> query) =>
        query.Where(t => t.Status == TripStatus.ReadyForDispatch);

    public IQueryable<Trip> OnHold(IQueryable<Trip> query)
    {
        return query.Where(t => t.Status == TripStatus.OnHold);
    }

    public IQueryable<Trip> FailedAttempts(IQueryable<Trip> query)
    {
        return query.Where(t => t.Status == TripStatus.FailedAttempt);
    }
}
