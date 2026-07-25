using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Queries;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record DispatchTripListItem(
    Guid Id,
    TripStatus Status,
    Guid CustomerId,
    string CustomerName,
    string? ContainerNumber,
    Guid? DriverUserId,
    string? DriverUsername,
    Guid? TruckAssetId,
    string? TruckAssetCode,
    bool PodPending,
    int UploadedDocumentCount,
    int RequiredDocumentCount,
    IReadOnlyCollection<DispatchTripDocumentChecklist> Documents,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? PickupLocation,
    string? DropoffLocation,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    decimal? DropoffLatitude,
    decimal? DropoffLongitude,
    DateTime? PickupScheduledAt,
    DateTime? DropoffScheduledAt,
    DateTime? PlannedStart,
    DateTime? PlannedEnd,
    int? PlannedDurationMinutes,
    bool LatePickup,
    bool LateDelivery,
    int? OnHoldMinutes,
    TripDocumentState PodState,
    bool CloseDocumentReady,
    int MissingRequiredDocumentCount,
    int RejectedRequiredDocumentCount,
    string? CloseDocumentBlockReason,
    byte[] RowVersion,
    TripStatus? HoldPreviousStatus,
    TripStatus? FailedAttemptFromStatus,
    DateTime? LastEventAt,
    DispatchTripLatestDriverLocation? LatestDriverLocation);

public sealed record DispatchTripDocumentChecklist(
    TripDocumentType Type,
    TripDocumentState State);

public sealed record DispatchTripFinancialDetail(
    decimal? Rate,
    decimal? Payroll,
    decimal? Allowance,
    decimal? FuelAmount,
    decimal? FuelPricePerLiter,
    string? OfficialReceiptNumber);

public sealed record DispatchTripLatestDriverLocation(
    double Latitude,
    double Longitude,
    double? AccuracyMeters,
    DateTime RecordedAt,
    bool IsStale);

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
    string? ContainerNumber,
    string? EirNumber,
    string? BookingNumber,
    string? ShippingLine,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<TripStop> Stops,
    IReadOnlyCollection<TripDocument> Documents,
    IReadOnlyCollection<TripStatusHistory> History,
    DispatchTripFinancialDetail? Financials,
    DispatchTripLatestDriverLocation? LatestDriverLocation,
    byte[] RowVersion);

public sealed record DispatchTripSummary(
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
    IReadOnlyCollection<DispatchTripDocumentChecklist> Documents,
    TripDocumentState PodState,
    bool CloseDocumentReady,
    int MissingRequiredDocumentCount,
    int RejectedRequiredDocumentCount,
    string? CloseDocumentBlockReason,
    Guid? CreatedByUserId,
    string? CreatedByUsername,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PickupScheduledAt,
    DateTime? DropoffScheduledAt,
    DateTime? PlannedStart,
    DateTime? PlannedEnd,
    int? PlannedDurationMinutes,
    bool LatePickup,
    bool LateDelivery,
    int? OnHoldMinutes,
    byte[] RowVersion);

public sealed record DispatchAssignmentDayTripItem(
    Guid TripId,
    string TripReference,
    TripStatus Status,
    Guid CustomerId,
    string CustomerName,
    Guid? DriverUserId,
    string? DriverUsername,
    Guid? TruckAssetId,
    string? TruckAssetCode,
    DateTime PlannedStart,
    DateTime PlannedEnd,
    int PlannedDurationMinutes,
    bool HasOverlap);

public sealed record DispatchAssignmentDayGroup(
    string GroupKey,
    string GroupLabel,
    bool HasOverlap,
    IReadOnlyCollection<DispatchAssignmentDayTripItem> Trips);

public sealed class DispatchTripQueryService
{
    private static readonly TripStatus[] OperationalFlow =
    [
        TripStatus.Dispatched,
        TripStatus.EnroutePickup,
        TripStatus.AtPickup,
        TripStatus.Loaded,
        TripStatus.EnrouteDropoff,
        TripStatus.AtDropoff,
        TripStatus.Delivered
    ];

    private readonly InventoryDbContext _dbContext;
    private readonly DispatchingOptions _options;
    private readonly DispatchTripQueryBuilder _builder;

    public DispatchTripQueryService(InventoryDbContext dbContext, IOptions<DispatchingOptions> options)
    {
        _dbContext = dbContext;
        _options = options?.Value ?? new DispatchingOptions();
        _builder = new DispatchTripQueryBuilder(_dbContext, _options);
    }

    public async Task<PagedQueryResult<DispatchTripListItem>> GetTripsAsync(
        TripStatus? status,
        Guid? driverUserId,
        Guid? truckAssetId,
        Guid? customerId,
        DateTime? pickupFrom,
        DateTime? pickupTo,
        DateTime? deliveredFrom,
        DateTime? deliveredTo,
        DispatchPodStatusFilter? podStatus,
        int page,
        int pageSize,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var requiredTypes = DispatchDocumentRules.GetRequiredDocumentTypes(_options).ToArray();
        var query = _builder.Base(actor);
        query = _builder.FilterStatus(query, status);
        query = _builder.FilterDriver(query, driverUserId);
        query = _builder.FilterTruck(query, truckAssetId);
        query = _builder.FilterCustomer(query, customerId);
        query = _builder.FilterPickupRange(query, pickupFrom, pickupTo);
        query = _builder.FilterDeliveredRange(query, deliveredFrom, deliveredTo);
        query = _builder.FilterPodStatus(query, podStatus);

        return await ExecuteTripListQueryAsync(query, requiredTypes, page, pageSize, cancellationToken);
    }

    public async Task<PagedQueryResult<DispatchTripListItem>> GetActiveTripsAsync(
        int page,
        int pageSize,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureMonitoringAccess(actor);
        var requiredTypes = DispatchDocumentRules.GetRequiredDocumentTypes(_options).ToArray();
        var query = _builder.Active(_builder.Base(actor));
        return await ExecuteTripListQueryAsync(query, requiredTypes, page, pageSize, cancellationToken);
    }

    public async Task<PagedQueryResult<DispatchTripListItem>> GetOnHoldTripsAsync(
        int page,
        int pageSize,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureMonitoringAccess(actor);
        var requiredTypes = DispatchDocumentRules.GetRequiredDocumentTypes(_options).ToArray();
        var query = _builder.OnHold(_builder.Base(actor));
        return await ExecuteTripListQueryAsync(query, requiredTypes, page, pageSize, cancellationToken);
    }

    public async Task<PagedQueryResult<DispatchTripListItem>> GetFailedAttemptTripsAsync(
        int page,
        int pageSize,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureMonitoringAccess(actor);
        var requiredTypes = DispatchDocumentRules.GetRequiredDocumentTypes(_options).ToArray();
        var query = _builder.FailedAttempts(_builder.Base(actor));
        return await ExecuteTripListQueryAsync(query, requiredTypes, page, pageSize, cancellationToken);
    }

    public async Task<PagedQueryResult<DispatchTripListItem>> GetPodPendingTripsAsync(
        int page,
        int pageSize,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureMonitoringAccess(actor);
        var requiredTypes = DispatchDocumentRules.GetRequiredDocumentTypes(_options).ToArray();
        var query = _builder.FilterPodStatus(_builder.Base(actor), DispatchPodStatusFilter.Pending);

        return await ExecuteTripListQueryAsync(query, requiredTypes, page, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyCollection<DispatchAssignmentDayGroup>> GetAssignmentDayViewAsync(
        DateOnly day,
        DispatchAssignmentGroupBy groupBy,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureMonitoringAccess(actor);

        var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var rows = await _builder.Base(actor)
            .Where(t => t.Status != TripStatus.Closed && t.Status != TripStatus.Cancelled)
            .Select(t => new
            {
                t.Id,
                t.Status,
                t.CustomerId,
                CustomerName = t.Customer != null ? t.Customer.Name : string.Empty,
                t.DriverUserId,
                DriverUsername = t.Driver != null ? t.Driver.Username : null,
                t.TruckAssetId,
                TruckAssetCode = t.TruckAsset != null ? t.TruckAsset.AssetCode : null,
                PlannedStart = _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Pickup)
                    .Select(s => s.ScheduledAt)
                    .FirstOrDefault(),
                PlannedEnd = _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Dropoff)
                    .Select(s => s.ScheduledAt)
                    .FirstOrDefault()
            })
            .Where(item =>
                item.PlannedStart.HasValue &&
                item.PlannedEnd.HasValue &&
                item.PlannedStart.Value < dayEnd &&
                item.PlannedEnd.Value > dayStart)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(item =>
            {
                var plannedDurationMinutes = (int)Math.Floor((item.PlannedEnd!.Value - item.PlannedStart!.Value).TotalMinutes);
                if (plannedDurationMinutes < 0)
                {
                    plannedDurationMinutes = 0;
                }

                return new DispatchAssignmentDayTripItem(
                    item.Id,
                    BuildTripReference(item.Id),
                    item.Status,
                    item.CustomerId,
                    item.CustomerName,
                    item.DriverUserId,
                    item.DriverUsername,
                    item.TruckAssetId,
                    item.TruckAssetCode,
                    item.PlannedStart.Value,
                    item.PlannedEnd.Value,
                    plannedDurationMinutes,
                    false);
            })
            .ToList();

        return BuildAssignmentDayGroups(items, groupBy);
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

        var latestLocation = await GetLatestDriverLocationAsync(trip.Id, cancellationToken);
        return MapTripDetail(trip, CanViewFinancials(actor), latestLocation);
    }

    public async Task<DispatchTripSummary> GetTripSummaryAsync(
        Guid tripId,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var requiredTypes = DispatchDocumentRules.GetRequiredDocumentTypes(_options).ToArray();
        var query = _builder.Base(actor).Where(t => t.Id == tripId);
        var listResult = await ExecuteTripListQueryAsync(query, requiredTypes, 1, 1, cancellationToken);
        var item = listResult.Items.FirstOrDefault();
        if (item is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        var createdBy = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(log =>
                log.EntityType == EntityTypes.DispatchTrip &&
                log.EntityId == tripId &&
                log.Action == AuditActions.DispatchTripCreated)
            .OrderBy(log => log.CreatedAt)
            .Select(log => new
            {
                log.ActorUserId,
                Username = log.Actor != null ? log.Actor.Username : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new DispatchTripSummary(
            item.Id,
            item.Status,
            item.CustomerId,
            item.CustomerName,
            item.DriverUserId,
            item.DriverUsername,
            item.TruckAssetId,
            item.TruckAssetCode,
            item.PodPending,
            item.UploadedDocumentCount,
            item.RequiredDocumentCount,
            item.Documents,
            item.PodState,
            item.CloseDocumentReady,
            item.MissingRequiredDocumentCount,
            item.RejectedRequiredDocumentCount,
            item.CloseDocumentBlockReason,
            createdBy?.ActorUserId,
            createdBy?.Username,
            item.CreatedAt,
            item.UpdatedAt,
            item.PickupScheduledAt,
            item.DropoffScheduledAt,
            item.PlannedStart,
            item.PlannedEnd,
            item.PlannedDurationMinutes,
            item.LatePickup,
            item.LateDelivery,
            item.OnHoldMinutes,
            item.RowVersion);
    }

    public async Task<PagedQueryResult<DispatchTripListItem>> GetMyTripsAsync(
        Guid driverUserId,
        bool includeClosed,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var requiredTypes = DispatchDocumentRules.GetRequiredDocumentTypes(_options).ToArray();
        var requiredDocumentCount = requiredTypes.Length;
        var query = _dbContext.DispatchTrips
            .AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Driver)
            .Include(t => t.TruckAsset)
            .Where(t => t.DriverUserId == driverUserId);

        if (!includeClosed)
        {
            query = query.Where(t => t.Status != TripStatus.Closed && t.Status != TripStatus.Cancelled);
        }

        if (from.HasValue)
        {
            query = query.Where(t =>
                (t.Stops.Where(s => s.StopType == TripStopType.Pickup)
                    .Select(s => s.ScheduledAt)
                    .FirstOrDefault() ?? t.CreatedAt) >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(t =>
                (t.Stops.Where(s => s.StopType == TripStopType.Pickup)
                    .Select(s => s.ScheduledAt)
                    .FirstOrDefault() ?? t.CreatedAt) <= to.Value);
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
                t.ContainerNumber,
                t.DriverUserId,
                t.Driver != null ? t.Driver.Username : null,
                t.TruckAssetId,
                t.TruckAsset != null ? t.TruckAsset.AssetCode : null,
                t.PodPending,
                _dbContext.DispatchTripDocuments.Count(d =>
                    d.TripId == t.Id &&
                    d.IsActive &&
                    requiredTypes.Contains(d.Type) &&
                    (d.State == TripDocumentState.Uploaded || d.State == TripDocumentState.Verified)),
                requiredDocumentCount,
                Array.Empty<DispatchTripDocumentChecklist>(),
                t.CreatedAt,
                t.UpdatedAt,
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Pickup)
                    .Select(s => s.LocationText)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Dropoff)
                    .Select(s => s.LocationText)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Pickup)
                    .Select(s => s.Latitude)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Pickup)
                    .Select(s => s.Longitude)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Dropoff)
                    .Select(s => s.Latitude)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Dropoff)
                    .Select(s => s.Longitude)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Pickup)
                    .Select(s => s.ScheduledAt)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Dropoff)
                    .Select(s => s.ScheduledAt)
                    .FirstOrDefault(),
                null,
                null,
                null,
                false,
                false,
                null,
                TripDocumentState.Missing,
                false,
                0,
                0,
                null,
                t.RowVersion,
                t.HoldPreviousStatus,
                _dbContext.DispatchTripStatusHistories
                    .Where(h => h.TripId == t.Id && h.ToStatus == TripStatus.FailedAttempt)
                    .OrderByDescending(h => h.EventAt)
                    .Select(h => (TripStatus?)h.FromStatus)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStatusHistories
                    .Where(h => h.TripId == t.Id)
                    .OrderByDescending(h => h.EventAt)
                    .Select(h => (DateTime?)h.EventAt)
                    .FirstOrDefault(),
                null))
            .ToListAsync(cancellationToken);

        var enriched = await AttachDocumentChecklistAsync(results, requiredTypes, cancellationToken);
        enriched = await AttachLatestDriverLocationsAsync(enriched, cancellationToken);
        enriched = AttachDocumentReadiness(enriched);
        enriched = AttachPlannedWindow(enriched);
        enriched = AttachOperationalIndicators(enriched);
        return new PagedQueryResult<DispatchTripListItem>(enriched, total);
    }

    public async Task<DispatchTripDetail> GetMyTripDetailAsync(
        Guid tripId,
        Guid driverUserId,
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
            .FirstOrDefaultAsync(t => t.Id == tripId && t.DriverUserId == driverUserId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        var latestLocation = await GetLatestDriverLocationAsync(trip.Id, cancellationToken);
        return MapTripDetail(trip, includeFinancials: false, latestLocation);
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
            .OrderBy(h => h.EventAt)
            .ThenBy(h => h.RecordedAt)
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
            .Where(d => d.TripId == tripId && d.IsActive)
            .OrderBy(d => d.Type)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedQueryResult<TripDocument>> GetTripDocumentVersionsPagedAsync(
        Guid tripId,
        TripDocumentType? type,
        TripDocumentState? state,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var tripQuery = _dbContext.DispatchTrips.AsNoTracking();
        if (actor.IsPrivileged)
        {
            tripQuery = tripQuery.Where(t => t.Id == tripId);
        }
        else if (actor.IsDriver)
        {
            tripQuery = tripQuery.Where(t => t.Id == tripId && t.DriverUserId == actor.UserId);
        }
        else
        {
            throw new NotFoundException("Trip not found.");
        }

        var tripExists = await tripQuery.AnyAsync(cancellationToken);
        if (!tripExists)
        {
            throw new NotFoundException("Trip not found.");
        }

        var query = _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Include(d => d.UploadedBy)
            .Include(d => d.VerifiedBy)
            .Include(d => d.RejectedBy)
            .Where(d => d.TripId == tripId);

        if (type.HasValue)
        {
            query = query.Where(d => d.Type == type.Value);
        }

        if (state.HasValue)
        {
            query = query.Where(d => d.State == state.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(d => d.UploadedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(d => d.UploadedAt <= to.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.UploadedAt)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<TripDocument>(items, total);
    }

    public async Task<string> GetTripDocumentLinkAsync(
        Guid tripId,
        Guid docId,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var tripQuery = _dbContext.DispatchTrips.AsNoTracking();
        if (actor.IsPrivileged)
        {
            tripQuery = tripQuery.Where(t => t.Id == tripId);
        }
        else if (actor.IsDriver)
        {
            tripQuery = tripQuery.Where(t => t.Id == tripId && t.DriverUserId == actor.UserId);
        }
        else
        {
            throw new NotFoundException("Trip not found.");
        }

        var tripExists = await tripQuery.AnyAsync(cancellationToken);
        if (!tripExists)
        {
            throw new NotFoundException("Trip not found.");
        }

        var storageKey = await _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Where(d => d.TripId == tripId && d.Id == docId)
            .Select(d => d.StorageKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (storageKey is null)
        {
            throw new NotFoundException("Document not found.");
        }

        return storageKey;
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

    private DispatchTripDetail MapTripDetail(
        Trip trip,
        bool includeFinancials,
        DispatchTripLatestDriverLocation? latestLocation)
    {
        var history = trip.StatusHistory
            .OrderBy(h => h.EventAt)
            .ThenBy(h => h.RecordedAt)
            .ToList();

        var documents = trip.Documents
            .Where(d => d.IsActive)
            .OrderBy(d => d.Type)
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
            trip.ContainerNumber,
            trip.EirNumber,
            trip.BookingNumber,
            trip.ShippingLine,
            trip.CreatedAt,
            trip.UpdatedAt,
            trip.Stops,
            documents,
            history,
            includeFinancials
                ? new DispatchTripFinancialDetail(
                    trip.Rate,
                    trip.Payroll,
                    trip.Allowance,
                    trip.FuelAmount,
                    trip.FuelPricePerLiter,
                    trip.OfficialReceiptNumber)
                : null,
            latestLocation,
            trip.RowVersion);
    }

    private static bool CanViewFinancials(DispatchActorContext actor)
    {
        return actor.IsManager || actor.IsFinance || actor.IsCeo;
    }

    private static List<DispatchTripListItem> AttachPlannedWindow(List<DispatchTripListItem> items)
    {
        return items
            .Select(item =>
            {
                int? plannedDurationMinutes = null;
                if (item.PickupScheduledAt.HasValue &&
                    item.DropoffScheduledAt.HasValue &&
                    item.DropoffScheduledAt.Value > item.PickupScheduledAt.Value)
                {
                    plannedDurationMinutes = (int)Math.Floor(
                        (item.DropoffScheduledAt.Value - item.PickupScheduledAt.Value).TotalMinutes);
                }

                return item with
                {
                    PlannedStart = item.PickupScheduledAt,
                    PlannedEnd = item.DropoffScheduledAt,
                    PlannedDurationMinutes = plannedDurationMinutes
                };
            })
            .ToList();
    }

    private static List<DispatchTripListItem> AttachOperationalIndicators(List<DispatchTripListItem> items)
    {
        var now = DateTime.UtcNow;
        return items
            .Select(item =>
            {
                var effectiveStatus = ResolveEffectiveStatus(item);
                var latePickup = item.PickupScheduledAt.HasValue
                    && now > item.PickupScheduledAt.Value
                    && IsBeforePickup(effectiveStatus);
                var lateDelivery = item.DropoffScheduledAt.HasValue
                    && now > item.DropoffScheduledAt.Value
                    && IsBeforeDelivered(effectiveStatus);
                int? onHoldMinutes = null;
                if (item.Status == TripStatus.OnHold && item.LastEventAt.HasValue)
                {
                    onHoldMinutes = (int)Math.Floor((now - item.LastEventAt.Value).TotalMinutes);
                    if (onHoldMinutes < 0)
                    {
                        onHoldMinutes = 0;
                    }
                }

                return item with
                {
                    LatePickup = latePickup,
                    LateDelivery = lateDelivery,
                    OnHoldMinutes = onHoldMinutes
                };
            })
            .ToList();
    }

    private static TripStatus ResolveEffectiveStatus(DispatchTripListItem item)
    {
        if (item.Status == TripStatus.OnHold && item.HoldPreviousStatus.HasValue)
        {
            return item.HoldPreviousStatus.Value;
        }

        if (item.Status == TripStatus.FailedAttempt && item.FailedAttemptFromStatus.HasValue)
        {
            return item.FailedAttemptFromStatus.Value;
        }

        return item.Status;
    }

    private static bool IsBeforePickup(TripStatus status)
    {
        if (status is TripStatus.Draft or TripStatus.OnHold or TripStatus.FailedAttempt)
        {
            return true;
        }

        var idx = Array.IndexOf(OperationalFlow, status);
        var pickupIdx = Array.IndexOf(OperationalFlow, TripStatus.AtPickup);
        return idx >= 0 && idx < pickupIdx;
    }

    private static bool IsBeforeDelivered(TripStatus status)
    {
        if (status is TripStatus.Cancelled or TripStatus.Closed)
        {
            return false;
        }

        if (status is TripStatus.Draft or TripStatus.OnHold or TripStatus.FailedAttempt)
        {
            return true;
        }

        var idx = Array.IndexOf(OperationalFlow, status);
        var deliveredIdx = Array.IndexOf(OperationalFlow, TripStatus.Delivered);
        return idx >= 0 && idx < deliveredIdx;
    }

    private static IReadOnlyCollection<DispatchAssignmentDayGroup> BuildAssignmentDayGroups(
        IReadOnlyCollection<DispatchAssignmentDayTripItem> items,
        DispatchAssignmentGroupBy groupBy)
    {
        if (items.Count == 0)
        {
            return Array.Empty<DispatchAssignmentDayGroup>();
        }

        string ResolveGroupKey(DispatchAssignmentDayTripItem item)
        {
            return groupBy == DispatchAssignmentGroupBy.Driver
                ? item.DriverUserId?.ToString() ?? "UNASSIGNED_DRIVER"
                : item.TruckAssetId?.ToString() ?? "UNASSIGNED_TRUCK";
        }

        string ResolveGroupLabel(DispatchAssignmentDayTripItem item)
        {
            return groupBy == DispatchAssignmentGroupBy.Driver
                ? item.DriverUsername ?? "Unassigned Driver"
                : item.TruckAssetCode ?? "Unassigned Truck";
        }

        var grouped = items
            .GroupBy(ResolveGroupKey)
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(item => item.PlannedStart)
                    .ThenBy(item => item.PlannedEnd)
                    .ToList();

                var overlapFlags = new bool[ordered.Count];
                for (var i = 0; i < ordered.Count; i++)
                {
                    for (var j = i + 1; j < ordered.Count; j++)
                    {
                        if (ordered[i].PlannedStart < ordered[j].PlannedEnd &&
                            ordered[i].PlannedEnd > ordered[j].PlannedStart)
                        {
                            overlapFlags[i] = true;
                            overlapFlags[j] = true;
                        }
                    }
                }

                var mappedTrips = ordered
                    .Select((item, index) => item with { HasOverlap = overlapFlags[index] })
                    .ToList();

                return new DispatchAssignmentDayGroup(
                    group.Key,
                    ResolveGroupLabel(ordered[0]),
                    mappedTrips.Any(item => item.HasOverlap),
                    mappedTrips);
            })
            .OrderBy(group => group.GroupLabel)
            .ToList();

        return grouped;
    }

    private static string BuildTripReference(Guid tripId)
    {
        return tripId.ToString("N").Substring(0, 8).ToUpperInvariant();
    }

    private static void EnsureMonitoringAccess(DispatchActorContext actor)
    {
        if (actor.IsManager || actor.IsDispatcher || actor.IsCeo)
        {
            return;
        }

        throw new ForbiddenDomainException("Monitoring access denied.");
    }

    private async Task<PagedQueryResult<DispatchTripListItem>> ExecuteTripListQueryAsync(
        IQueryable<Trip> query,
        TripDocumentType[] requiredTypes,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var requiredDocumentCount = requiredTypes.Length;
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
                t.ContainerNumber,
                t.DriverUserId,
                t.Driver != null ? t.Driver.Username : null,
                t.TruckAssetId,
                t.TruckAsset != null ? t.TruckAsset.AssetCode : null,
                t.PodPending,
                _dbContext.DispatchTripDocuments.Count(d =>
                    d.TripId == t.Id &&
                    d.IsActive &&
                    requiredTypes.Contains(d.Type) &&
                    (d.State == TripDocumentState.Uploaded || d.State == TripDocumentState.Verified)),
                requiredDocumentCount,
                Array.Empty<DispatchTripDocumentChecklist>(),
                t.CreatedAt,
                t.UpdatedAt,
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Pickup)
                    .Select(s => s.LocationText)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Dropoff)
                    .Select(s => s.LocationText)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Pickup)
                    .Select(s => s.Latitude)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Pickup)
                    .Select(s => s.Longitude)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Dropoff)
                    .Select(s => s.Latitude)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Dropoff)
                    .Select(s => s.Longitude)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Pickup)
                    .Select(s => s.ScheduledAt)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStops
                    .Where(s => s.TripId == t.Id && s.StopType == TripStopType.Dropoff)
                    .Select(s => s.ScheduledAt)
                    .FirstOrDefault(),
                null,
                null,
                null,
                false,
                false,
                null,
                TripDocumentState.Missing,
                false,
                0,
                0,
                null,
                t.RowVersion,
                t.HoldPreviousStatus,
                _dbContext.DispatchTripStatusHistories
                    .Where(h => h.TripId == t.Id && h.ToStatus == TripStatus.FailedAttempt)
                    .OrderByDescending(h => h.EventAt)
                    .Select(h => (TripStatus?)h.FromStatus)
                    .FirstOrDefault(),
                _dbContext.DispatchTripStatusHistories
                    .Where(h => h.TripId == t.Id)
                    .OrderByDescending(h => h.EventAt)
                    .Select(h => (DateTime?)h.EventAt)
                    .FirstOrDefault(),
                null))
            .ToListAsync(cancellationToken);

        var enriched = await AttachDocumentChecklistAsync(results, requiredTypes, cancellationToken);
        enriched = await AttachLatestDriverLocationsAsync(enriched, cancellationToken);
        enriched = AttachDocumentReadiness(enriched);
        enriched = AttachPlannedWindow(enriched);
        enriched = AttachOperationalIndicators(enriched);
        return new PagedQueryResult<DispatchTripListItem>(enriched, total);
    }

    private async Task<List<DispatchTripListItem>> AttachLatestDriverLocationsAsync(
        List<DispatchTripListItem> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var tripIds = items.Select(item => item.Id).ToArray();
        var pings = await _dbContext.DispatchTripLocationPings
            .AsNoTracking()
            .Where(ping => tripIds.Contains(ping.TripId))
            .OrderByDescending(ping => ping.RecordedAt)
            .ThenByDescending(ping => ping.CreatedAt)
            .Select(ping => new
            {
                ping.TripId,
                ping.Latitude,
                ping.Longitude,
                ping.AccuracyMeters,
                ping.RecordedAt
            })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var lookup = pings
            .GroupBy(ping => ping.TripId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var ping = group.First();
                    return new DispatchTripLatestDriverLocation(
                        ping.Latitude,
                        ping.Longitude,
                        ping.AccuracyMeters,
                        ping.RecordedAt,
                        now - ping.RecordedAt > TimeSpan.FromMinutes(5));
                });

        return items
            .Select(item => lookup.TryGetValue(item.Id, out var latest)
                ? item with { LatestDriverLocation = latest }
                : item)
            .ToList();
    }

    private async Task<DispatchTripLatestDriverLocation?> GetLatestDriverLocationAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var ping = await _dbContext.DispatchTripLocationPings
            .AsNoTracking()
            .Where(item => item.TripId == tripId)
            .OrderByDescending(item => item.RecordedAt)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                item.Latitude,
                item.Longitude,
                item.AccuracyMeters,
                item.RecordedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (ping is null)
        {
            return null;
        }

        return new DispatchTripLatestDriverLocation(
            ping.Latitude,
            ping.Longitude,
            ping.AccuracyMeters,
            ping.RecordedAt,
            DateTime.UtcNow - ping.RecordedAt > TimeSpan.FromMinutes(5));
    }

    private async Task<List<DispatchTripListItem>> AttachDocumentChecklistAsync(
        List<DispatchTripListItem> items,
        TripDocumentType[] requiredTypes,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var tripIds = items.Select(item => item.Id).ToArray();

        var docs = await _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .Where(d => tripIds.Contains(d.TripId) && d.IsActive && requiredTypes.Contains(d.Type))
            .Select(d => new { d.TripId, d.Type, d.State })
            .ToListAsync(cancellationToken);

        var lookup = docs
            .GroupBy(d => d.TripId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(item => item.Type, item => item.State));

        if (requiredTypes.Contains(TripDocumentType.Waybill))
        {
            var generatedWaybillTripIds = await _dbContext.GeneratedWaybills
                .AsNoTracking()
                .Where(waybill => tripIds.Contains(waybill.TripId) && waybill.IsActive)
                .Select(waybill => waybill.TripId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var tripId in generatedWaybillTripIds)
            {
                if (!lookup.TryGetValue(tripId, out var states))
                {
                    states = new Dictionary<TripDocumentType, TripDocumentState>();
                    lookup[tripId] = states;
                }

                states[TripDocumentType.Waybill] = TripDocumentState.Verified;
            }
        }

        return items
            .Select(item =>
            {
                lookup.TryGetValue(item.Id, out var states);
                var checklist = BuildChecklist(requiredTypes, states);
                return item with { Documents = checklist };
            })
            .ToList();
    }

    private static IReadOnlyCollection<DispatchTripDocumentChecklist> BuildChecklist(
        TripDocumentType[] requiredTypes,
        IReadOnlyDictionary<TripDocumentType, TripDocumentState>? states)
    {
        var list = new List<DispatchTripDocumentChecklist>(requiredTypes.Length);
        foreach (var type in requiredTypes)
        {
            var state = TripDocumentState.Missing;
            if (states != null && states.TryGetValue(type, out var resolved))
            {
                state = resolved;
            }

            list.Add(new DispatchTripDocumentChecklist(type, state));
        }

        return list;
    }

    private List<DispatchTripListItem> AttachDocumentReadiness(List<DispatchTripListItem> items)
    {
        return items.Select(item =>
        {
            var podState = item.Documents
                .FirstOrDefault(doc => doc.Type == TripDocumentType.Pod)?.State
                ?? TripDocumentState.Missing;
            var missingRequiredCount = item.Documents.Count(doc => doc.State == TripDocumentState.Missing);
            var rejectedRequiredCount = item.Documents.Count(doc => doc.State == TripDocumentState.Rejected);

            var closeDocumentReady = IsCloseDocumentReady(item.Documents);
            var closeDocumentBlockReason = closeDocumentReady ? null : GetCloseDocumentBlockReason();

            return item with
            {
                PodState = podState,
                CloseDocumentReady = closeDocumentReady,
                MissingRequiredDocumentCount = missingRequiredCount,
                RejectedRequiredDocumentCount = rejectedRequiredCount,
                CloseDocumentBlockReason = closeDocumentBlockReason
            };
        }).ToList();
    }

    private static bool IsCloseDocumentReady(IReadOnlyCollection<DispatchTripDocumentChecklist> documents)
    {
        return documents.Count > 0 && documents.All(doc => doc.State == TripDocumentState.Verified);
    }

    private static string GetCloseDocumentBlockReason()
    {
        return "ATW, EIR, Gate Pass, DR, Waybill, and POD must be complete before closing.";
    }
}
