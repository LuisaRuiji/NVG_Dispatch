using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record DispatchTripListItem(
    Guid Id,
    TripStatus Status,
    Guid CustomerId,
    string CustomerName,
    Guid? DriverUserId,
    string? DriverUsername,
    Guid? TruckAssetId,
    string? TruckAssetCode,
    bool PodPending,
    int UploadedDocumentCount,
    int RequiredDocumentCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PickupScheduledAt,
    DateTime? DropoffScheduledAt);

public sealed record DispatchTripDetail(
    Guid Id,
    TripStatus Status,
    Guid CustomerId,
    string CustomerName,
    Guid? DriverUserId,
    string? DriverUsername,
    Guid? TruckAssetId,
    string? TruckAssetCode,
    bool PodPending,
    TripStatus? HoldPreviousStatus,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<TripStop> Stops,
    IReadOnlyCollection<TripDocument> Documents,
    IReadOnlyCollection<TripStatusHistory> History);

public sealed class DispatchTripQueryService
{
    private const int RequiredDocumentCount = 2;
    private readonly InventoryDbContext _dbContext;

    public DispatchTripQueryService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedQueryResult<DispatchTripListItem>> GetTripsAsync(
        TripStatus? status,
        Guid? driverUserId,
        Guid? customerId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DispatchTrips
            .AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Driver)
            .Include(t => t.TruckAsset)
            .Include(t => t.Documents)
            .AsQueryable();

        if (!actor.IsPrivileged)
        {
            query = query.Where(t => t.DriverUserId == actor.UserId);
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (driverUserId.HasValue)
        {
            query = query.Where(t => t.DriverUserId == driverUserId.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(t => t.CustomerId == customerId.Value);
        }

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

        var total = await query.CountAsync(cancellationToken);

        var results = await query
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new DispatchTripListItem(
                t.Id,
                t.Status,
                t.CustomerId,
                t.Customer != null ? t.Customer.Name : string.Empty,
                t.DriverUserId,
                t.Driver != null ? t.Driver.Username : null,
                t.TruckAssetId,
                t.TruckAsset != null ? t.TruckAsset.AssetCode : null,
                t.PodPending,
                t.Documents.Count(d => d.State == TripDocumentState.Uploaded || d.State == TripDocumentState.Verified),
                RequiredDocumentCount,
                t.CreatedAt,
                t.UpdatedAt,
                t.Stops.Where(s => s.StopType == TripStopType.Pickup).Select(s => s.ScheduledAt).FirstOrDefault(),
                t.Stops.Where(s => s.StopType == TripStopType.Dropoff).Select(s => s.ScheduledAt).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<DispatchTripListItem>(results, total);
    }

    public async Task<DispatchTripDetail> GetTripDetailAsync(
        Guid tripId,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips
            .AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Driver)
            .Include(t => t.TruckAsset)
            .Include(t => t.Stops)
            .Include(t => t.Documents)
            .ThenInclude(d => d.UploadedBy)
            .Include(t => t.Documents)
            .ThenInclude(d => d.VerifiedBy)
            .Include(t => t.Documents)
            .ThenInclude(d => d.RejectedBy)
            .Include(t => t.StatusHistory)
            .ThenInclude(h => h.Actor)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        EnsureTripAccess(trip, actor);

        var history = trip.StatusHistory
            .OrderBy(h => h.CreatedAt)
            .ToList();

        return new DispatchTripDetail(
            trip.Id,
            trip.Status,
            trip.CustomerId,
            trip.Customer?.Name ?? string.Empty,
            trip.DriverUserId,
            trip.Driver?.Username,
            trip.TruckAssetId,
            trip.TruckAsset?.AssetCode,
            trip.PodPending,
            trip.HoldPreviousStatus,
            trip.Notes,
            trip.CreatedAt,
            trip.UpdatedAt,
            trip.Stops,
            trip.Documents,
            history);
    }

    public async Task<IReadOnlyCollection<TripStatusHistory>> GetTripHistoryAsync(
        Guid tripId,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        EnsureTripAccess(trip, actor);

        return await _dbContext.DispatchTripStatusHistories
            .AsNoTracking()
            .Include(h => h.Actor)
            .Where(h => h.TripId == tripId)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TripDocument>> GetTripDocumentsAsync(
        Guid tripId,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        EnsureTripAccess(trip, actor);

        return await _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Include(d => d.UploadedBy)
            .Include(d => d.VerifiedBy)
            .Include(d => d.RejectedBy)
            .Where(d => d.TripId == tripId)
            .OrderBy(d => d.Type)
            .ToListAsync(cancellationToken);
    }

    private static void EnsureTripAccess(Trip trip, DispatchActorContext actor)
    {
        if (actor.IsPrivileged)
        {
            return;
        }

        if (actor.IsDriver && trip.DriverUserId == actor.UserId)
        {
            return;
        }

        throw new ForbiddenDomainException("Trip access denied.");
    }
}
