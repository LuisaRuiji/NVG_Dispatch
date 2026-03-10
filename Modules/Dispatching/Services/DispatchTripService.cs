using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record DispatchTripStopInput(
    TripStopType StopType,
    string LocationText,
    DateTime? ScheduledAt);

public sealed record CreateDispatchTripCommand(
    Guid CustomerId,
    Guid? DriverUserId,
    Guid? TruckAssetId,
    string? Notes,
    IReadOnlyCollection<DispatchTripStopInput>? Stops);

public sealed record UpdateDispatchTripCommand(
    Guid CustomerId,
    Guid? DriverUserId,
    Guid? TruckAssetId,
    string? Notes,
    IReadOnlyCollection<DispatchTripStopInput>? Stops,
    string? Remarks,
    byte[] RowVersion);

public sealed record DispatchTripCommand(
    Guid TripId,
    Guid DriverUserId,
    Guid? TruckAssetId,
    string? Remarks,
    byte[] RowVersion);

public sealed record ChangeDispatchTripStatusCommand(
    Guid TripId,
    TripStatus ToStatus,
    string? Remarks,
    bool? PodPendingOverride,
    DateTime EventAt,
    byte[] RowVersion);

public sealed record CorrectDispatchTripStatusCommand(
    Guid TripId,
    TripStatus ToStatus,
    DateTime EventAt,
    string Remarks,
    byte[] RowVersion);

public sealed class DispatchTripService : ITripLifecycleService
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

    private static readonly IReadOnlyDictionary<TripStatus, TripStatus[]> TransitionMap =
        new Dictionary<TripStatus, TripStatus[]>
        {
            { TripStatus.Draft, [TripStatus.Dispatched, TripStatus.Cancelled] },
            { TripStatus.Dispatched, [TripStatus.EnroutePickup, TripStatus.OnHold, TripStatus.Cancelled] },
            { TripStatus.EnroutePickup, [TripStatus.AtPickup, TripStatus.OnHold, TripStatus.FailedAttempt, TripStatus.Cancelled] },
            { TripStatus.AtPickup, [TripStatus.Loaded, TripStatus.OnHold, TripStatus.FailedAttempt, TripStatus.Cancelled] },
            { TripStatus.Loaded, [TripStatus.EnrouteDropoff, TripStatus.OnHold, TripStatus.Cancelled] },
            { TripStatus.EnrouteDropoff, [TripStatus.AtDropoff, TripStatus.OnHold, TripStatus.FailedAttempt, TripStatus.Cancelled] },
            { TripStatus.AtDropoff, [TripStatus.Delivered, TripStatus.OnHold, TripStatus.FailedAttempt, TripStatus.Cancelled] },
            { TripStatus.Delivered, [TripStatus.Closed, TripStatus.OnHold, TripStatus.Cancelled] },
            { TripStatus.OnHold, Array.Empty<TripStatus>() },
            { TripStatus.FailedAttempt, Array.Empty<TripStatus>() },
            { TripStatus.Cancelled, Array.Empty<TripStatus>() },
            { TripStatus.Closed, Array.Empty<TripStatus>() }
        };

    private readonly InventoryDbContext _dbContext;
    private readonly UserService _userService;
    private readonly IDispatchDocumentReadService _documentReadService;
    private readonly IAuditService _auditService;
    private readonly DispatchingOptions _options;

    public DispatchTripService(
        InventoryDbContext dbContext,
        UserService userService,
        IDispatchDocumentReadService documentReadService,
        IOptions<DispatchingOptions> options,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _userService = userService;
        _documentReadService = documentReadService;
        _auditService = auditService;
        _options = options.Value ?? new DispatchingOptions();
    }

    public async Task<Trip> CreateDraftAsync(
        CreateDispatchTripCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureDispatcherOrManager(actor);
        await _userService.EnsureActiveUserAsync(actor.UserId, cancellationToken);

        EnsureScheduledStops(command.Stops);

        var customer = await _dbContext.DispatchCustomers
            .FirstOrDefaultAsync(c => c.Id == command.CustomerId, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException("Customer not found.");
        }

        if (command.DriverUserId.HasValue)
        {
            await _userService.EnsureUserHasRoleAsync(command.DriverUserId.Value, RoleNames.Driver, cancellationToken);
        }

        if (command.TruckAssetId.HasValue)
        {
            var asset = await _dbContext.Assets
                .FirstOrDefaultAsync(a => a.Id == command.TruckAssetId.Value, cancellationToken);
            if (asset is null)
            {
                throw new NotFoundException("Truck asset not found.");
            }

            if (asset.Status != AssetStatus.Active)
            {
                throw new BusinessRuleViolationException("Truck asset is inactive.");
            }
        }

        var now = DateTime.UtcNow;
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            CustomerId = command.CustomerId,
            DriverUserId = command.DriverUserId,
            TruckAssetId = command.TruckAssetId,
            Status = TripStatus.Draft,
            PodPending = false,
            Notes = command.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.DispatchTrips.Add(trip);

        if (command.Stops is { Count: > 0 })
        {
            foreach (var stop in command.Stops)
            {
                _dbContext.DispatchTripStops.Add(new TripStop
                {
                    Id = Guid.NewGuid(),
                    TripId = trip.Id,
                    StopType = stop.StopType,
                    LocationText = stop.LocationText,
                    ScheduledAt = stop.ScheduledAt,
                    CreatedAt = now
                });
            }
        }

        _auditService.AddEntry(
            actor.UserId,
            AuditActions.DispatchTripCreated,
            EntityTypes.DispatchTrip,
            trip.Id,
            null,
            new { trip.Status },
            tripId: trip.Id);

        await SaveChangesAsync(cancellationToken);
        return trip;
    }

    public async Task<Trip> UpdateTripAsync(
        Guid tripId,
        UpdateDispatchTripCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureDispatcherOrManager(actor);

        var trip = await _dbContext.DispatchTrips
            .Include(t => t.Stops)
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        ApplyRowVersion(trip, command.RowVersion);

        if (trip.Status == TripStatus.Closed || trip.Status == TripStatus.Cancelled)
        {
            throw new ConflictDomainException("Closed or cancelled trips cannot be edited.");
        }

        var isDraft = trip.Status == TripStatus.Draft;

        if (!isDraft && !actor.IsManager && !actor.IsDispatcher)
        {
            throw new ConflictDomainException("Only managers or dispatchers can edit active trips.");
        }

        var originalCustomerId = trip.CustomerId;
        var originalDriverUserId = trip.DriverUserId;
        var originalTruckAssetId = trip.TruckAssetId;
        var originalNotes = trip.Notes;
        var originalPickup = trip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Pickup);
        var originalDropoff = trip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Dropoff);
        var updateRemarks = string.IsNullOrWhiteSpace(command.Remarks) ? null : command.Remarks.Trim();

        var customerChanged = command.CustomerId != trip.CustomerId;

        if (!isDraft && actor.IsDispatcher && customerChanged)
        {
            throw new ConflictDomainException("Dispatchers cannot change the customer after dispatch.");
        }

        if (customerChanged || isDraft)
        {
            var customer = await _dbContext.DispatchCustomers
                .FirstOrDefaultAsync(c => c.Id == command.CustomerId, cancellationToken);
            if (customer is null)
            {
                throw new NotFoundException("Customer not found.");
            }

            trip.CustomerId = command.CustomerId;
        }

        if (command.DriverUserId.HasValue)
        {
            await _userService.EnsureUserHasRoleAsync(command.DriverUserId.Value, RoleNames.Driver, cancellationToken);
        }

        if (command.TruckAssetId.HasValue)
        {
            var asset = await _dbContext.Assets
                .FirstOrDefaultAsync(a => a.Id == command.TruckAssetId.Value, cancellationToken);
            if (asset is null)
            {
                throw new NotFoundException("Truck asset not found.");
            }

            if (asset.Status != AssetStatus.Active)
            {
                throw new BusinessRuleViolationException("Truck asset is inactive.");
            }
        }

        var driverChanged = originalDriverUserId != command.DriverUserId;
        var truckChanged = originalTruckAssetId != command.TruckAssetId;
        var assignmentChanged = driverChanged || truckChanged;

        if (assignmentChanged)
        {
            if (trip.Status == TripStatus.Delivered)
            {
                throw new ConflictDomainException("Driver or truck reassignment is not allowed after delivery.");
            }

            var effectiveStatus = await ResolveReassignmentStatusAsync(trip, cancellationToken);
            var loadedOrLater = IsLoadedOrLater(effectiveStatus);

            if (actor.IsDispatcher && loadedOrLater)
            {
                throw new ConflictDomainException("Dispatchers can only reassign before loading.");
            }

            if (actor.IsManager && loadedOrLater)
            {
                RequireReassignmentRemarks(updateRemarks);
            }
        }

        var scheduleChanged = false;

        if (customerChanged)
        {
            scheduleChanged = true;
        }

        if (trip.DriverUserId != command.DriverUserId)
        {
            scheduleChanged = true;
        }

        if (trip.TruckAssetId != command.TruckAssetId)
        {
            scheduleChanged = true;
        }

        trip.DriverUserId = command.DriverUserId;
        trip.TruckAssetId = command.TruckAssetId;
        if (trip.Notes != command.Notes)
        {
            scheduleChanged = true;
        }
        trip.Notes = command.Notes;
        trip.UpdatedAt = DateTime.UtcNow;

        if (command.Stops is not null)
        {
            if (isDraft)
            {
                EnsureScheduledStops(command.Stops);
                _dbContext.DispatchTripStops.RemoveRange(trip.Stops);
                var now = DateTime.UtcNow;
                foreach (var stop in command.Stops)
                {
                    _dbContext.DispatchTripStops.Add(new TripStop
                    {
                        Id = Guid.NewGuid(),
                        TripId = trip.Id,
                        StopType = stop.StopType,
                        LocationText = stop.LocationText,
                        ScheduledAt = stop.ScheduledAt,
                        CreatedAt = now
                    });
                }
                scheduleChanged = true;
            }
            else
            {
                ApplyNonDraftScheduleUpdate(trip, command.Stops, actor, ref scheduleChanged);
            }
        }
        else if (isDraft)
        {
            EnsureScheduledStops(trip.Stops.Select(stop => new DispatchTripStopInput(
                stop.StopType,
                stop.LocationText,
                stop.ScheduledAt)).ToList());
        }

        if (!isDraft && assignmentChanged)
        {
            var (pickupAt, dropoffAt) = GetScheduledWindow(trip.Stops);
            await EnforceAssignmentConflictsAsync(
                trip.Id,
                trip.DriverUserId,
                trip.TruckAssetId,
                pickupAt,
                dropoffAt,
                actor,
                updateRemarks,
                cancellationToken);
        }

        var changeSummary = BuildScheduleChangeSummary(
            originalCustomerId,
            originalDriverUserId,
            originalTruckAssetId,
            originalNotes,
            originalPickup,
            originalDropoff,
            command,
            trip);

        if (!isDraft && scheduleChanged)
        {
            var historyRemarks = BuildHistoryRemarks(changeSummary, updateRemarks);
            var historyEventAt = DateTime.UtcNow;
            await EnsureValidEventAtAsync(trip, historyEventAt, cancellationToken);
            AddHistory(
                trip.Id,
                trip.Status,
                trip.Status,
                actor.UserId,
                historyRemarks ?? "Schedule updated.",
                historyEventAt,
                TripHistoryEventType.ScheduleUpdated);
        }

        if (scheduleChanged)
        {
            var before = new
            {
                trip.Status,
                CustomerId = originalCustomerId,
                DriverUserId = originalDriverUserId,
                TruckAssetId = originalTruckAssetId,
                Notes = originalNotes,
                Pickup = originalPickup is null
                    ? null
                    : new { originalPickup.LocationText, originalPickup.ScheduledAt },
                Dropoff = originalDropoff is null
                    ? null
                    : new { originalDropoff.LocationText, originalDropoff.ScheduledAt }
            };

            var afterPickup = ResolveStopInput(command, trip, TripStopType.Pickup);
            var afterDropoff = ResolveStopInput(command, trip, TripStopType.Dropoff);

            var after = new
            {
                trip.Status,
                trip.CustomerId,
                trip.DriverUserId,
                trip.TruckAssetId,
                trip.Notes,
                Pickup = afterPickup is null
                    ? null
                    : new { afterPickup.LocationText, afterPickup.ScheduledAt },
                Dropoff = afterDropoff is null
                    ? null
                    : new { afterDropoff.LocationText, afterDropoff.ScheduledAt },
                ChangeSummary = changeSummary,
                Remarks = updateRemarks
            };

            _auditService.AddEntry(
                actor.UserId,
                AuditActions.DispatchTripUpdated,
                EntityTypes.DispatchTrip,
                trip.Id,
                before,
                after,
                tripId: trip.Id);
        }

        await SaveChangesAsync(cancellationToken);
        return trip;
    }

    public async Task<Trip> DispatchAsync(
        DispatchTripCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureDispatcherOrManager(actor);

        var trip = await _dbContext.DispatchTrips
            .Include(t => t.Stops)
            .FirstOrDefaultAsync(t => t.Id == command.TripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        ApplyRowVersion(trip, command.RowVersion);

        if (trip.Status != TripStatus.Draft)
        {
            throw new ConflictDomainException("Only draft trips can be dispatched.");
        }

        if (command.DriverUserId == Guid.Empty)
        {
            throw new BusinessRuleViolationException("Dispatching requires an assigned driver.");
        }

        EnsureScheduledStops(trip.Stops.Select(stop => new DispatchTripStopInput(
            stop.StopType,
            stop.LocationText,
            stop.ScheduledAt)).ToList());

        await _userService.EnsureUserHasRoleAsync(command.DriverUserId, RoleNames.Driver, cancellationToken);

        if (command.TruckAssetId.HasValue)
        {
            var asset = await _dbContext.Assets
                .FirstOrDefaultAsync(a => a.Id == command.TruckAssetId.Value, cancellationToken);
            if (asset is null)
            {
                throw new NotFoundException("Truck asset not found.");
            }

            if (asset.Status != AssetStatus.Active)
            {
                throw new BusinessRuleViolationException("Truck asset is inactive.");
            }
        }

        var (pickupAt, dropoffAt) = GetScheduledWindow(trip.Stops);
        await EnforceAssignmentConflictsAsync(
            trip.Id,
            command.DriverUserId,
            command.TruckAssetId,
            pickupAt,
            dropoffAt,
            actor,
            command.Remarks,
            cancellationToken);

        var now = DateTime.UtcNow;
        await EnsureValidEventAtAsync(trip, now, cancellationToken);
        var fromStatus = trip.Status;
        trip.DriverUserId = command.DriverUserId;
        trip.TruckAssetId = command.TruckAssetId;
        await EnsureTransitionAllowedAsync(trip, TripStatus.Dispatched, actor, command.Remarks, allowDraftDispatch: true, cancellationToken);
        trip.Status = TripStatus.Dispatched;
        trip.UpdatedAt = now;

        AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, command.Remarks, now);

        _auditService.AddEntry(
            actor.UserId,
            AuditActions.DispatchTripDispatched,
            EntityTypes.DispatchTrip,
            trip.Id,
            null,
            new { trip.Status },
            tripId: trip.Id);

        await SaveChangesAsync(cancellationToken);
        return trip;
    }

    public async Task<Trip> ChangeStatusAsync(
        ChangeDispatchTripStatusCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips
            .FirstOrDefaultAsync(t => t.Id == command.TripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        ApplyRowVersion(trip, command.RowVersion);

        EnsureTripAccess(trip, actor);

        var fromStatus = trip.Status;
        var toStatus = command.ToStatus;
        if (fromStatus == toStatus)
        {
            throw new ConflictDomainException("Trip is already in that status.");
        }

        if (toStatus == TripStatus.Draft)
        {
            throw new ConflictDomainException("Trips cannot transition back to draft.");
        }

        if (fromStatus == TripStatus.Closed || fromStatus == TripStatus.Cancelled)
        {
            throw new ConflictDomainException("Closed or cancelled trips cannot transition.");
        }

        if (!actor.IsManager && !actor.IsDriver)
        {
            throw new ForbiddenDomainException("Only drivers or managers can change trip status.");
        }

        var remarks = string.IsNullOrWhiteSpace(command.Remarks) ? null : command.Remarks.Trim();
        await EnsureValidEventAtAsync(trip, command.EventAt, cancellationToken);
        await EnsureTransitionAllowedAsync(trip, toStatus, actor, remarks, allowDraftDispatch: false, cancellationToken);

        if (fromStatus == TripStatus.FailedAttempt)
        {
            EnsureManager(actor);
            var previous = await GetFailedAttemptResumeStatusAsync(trip.Id, cancellationToken);
            if (previous is null)
            {
                throw new ConflictDomainException("Failed attempt resume status is missing.");
            }

            if (toStatus == TripStatus.OnHold)
            {
                trip.HoldPreviousStatus = previous.Value;
                trip.Status = TripStatus.OnHold;
            }
            else if (toStatus == TripStatus.Cancelled)
            {
                trip.Status = TripStatus.Cancelled;
            }
            else if (toStatus == previous.Value)
            {
                trip.Status = previous.Value;
            }
            else
            {
                throw new ConflictDomainException("Failed attempt can only resume to the previous status, be put on hold, or be cancelled.");
            }

            trip.UpdatedAt = DateTime.UtcNow;
            AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks, command.EventAt);
            AddStatusAudit(actor.UserId, trip.Id, trip.Status);
            await SaveChangesAsync(cancellationToken);
            return trip;
        }

        if (toStatus == TripStatus.OnHold)
        {
            ApplyHold(trip, actor, remarks);
            AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks, command.EventAt);
            AddStatusAudit(actor.UserId, trip.Id, trip.Status);
            await SaveChangesAsync(cancellationToken);
            return trip;
        }

        if (fromStatus == TripStatus.OnHold)
        {
            EnsureManager(actor);
            if (trip.HoldPreviousStatus is null)
            {
                throw new ConflictDomainException("Hold resume status is missing.");
            }

            if (toStatus == TripStatus.Cancelled)
            {
                trip.Status = TripStatus.Cancelled;
                trip.HoldPreviousStatus = null;
            }
            else if (toStatus == trip.HoldPreviousStatus)
            {
                trip.Status = toStatus;
                trip.HoldPreviousStatus = null;
            }
            else
            {
                throw new ConflictDomainException("On-hold trips can only resume to the previous status.");
            }

            trip.UpdatedAt = DateTime.UtcNow;
            AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks, command.EventAt);
            AddStatusAudit(actor.UserId, trip.Id, trip.Status);
            await SaveChangesAsync(cancellationToken);
            return trip;
        }

        if (toStatus == TripStatus.FailedAttempt)
        {
            ApplyFailedAttempt(trip, actor, remarks);
            AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks, command.EventAt);
            AddStatusAudit(actor.UserId, trip.Id, trip.Status);
            await SaveChangesAsync(cancellationToken);
            return trip;
        }

        if (toStatus == TripStatus.Cancelled)
        {
            EnsureManager(actor);
            trip.Status = TripStatus.Cancelled;
            trip.UpdatedAt = DateTime.UtcNow;
            AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks, command.EventAt);
            AddStatusAudit(actor.UserId, trip.Id, trip.Status);
            await SaveChangesAsync(cancellationToken);
            return trip;
        }

        if (toStatus == TripStatus.Closed)
        {
            EnsureManager(actor);
            if (fromStatus != TripStatus.Delivered)
            {
                throw new ConflictDomainException("Trip must be delivered before closing.");
            }

            if (_options.DocVerificationEnabled)
            {
                var hasVerifiedPod = await _documentReadService.HasVerifiedPodAsync(trip.Id, cancellationToken);
                if (!hasVerifiedPod)
                {
                    throw new ConflictDomainException("Closing requires a verified POD.");
                }
            }
            else
            {
                var hasUploadedOrVerifiedPod = await _documentReadService.HasUploadedOrVerifiedPodAsync(trip.Id, cancellationToken);
                if (!hasUploadedOrVerifiedPod && !trip.PodPending)
                {
                    throw new ConflictDomainException("Closing requires POD upload or POD pending.");
                }
            }

            trip.Status = TripStatus.Closed;
            trip.UpdatedAt = DateTime.UtcNow;
            AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks, command.EventAt);
            AddStatusAudit(actor.UserId, trip.Id, trip.Status);
            await SaveChangesAsync(cancellationToken);
            return trip;
        }

        if (command.PodPendingOverride.HasValue)
        {
            EnsureManager(actor);
            RequirePodPendingRemarks(command.PodPendingOverride.Value, remarks);
            trip.PodPending = command.PodPendingOverride.Value;
        }

        trip.Status = toStatus;
        trip.UpdatedAt = DateTime.UtcNow;
        AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks, command.EventAt);
        AddStatusAudit(actor.UserId, trip.Id, trip.Status);
        await SaveChangesAsync(cancellationToken);
        return trip;
    }

    public async Task<Trip> CorrectStatusAsync(
        CorrectDispatchTripStatusCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        EnsureDispatcherOrManager(actor);

        var tripId = command.TripId;
        var toStatus = command.ToStatus;
        var eventAt = command.EventAt;
        var remarks = command.Remarks;

        if (string.IsNullOrWhiteSpace(remarks))
        {
            throw new BusinessRuleViolationException("Remarks are required for status correction.");
        }

        var trip = await _dbContext.DispatchTrips
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        ApplyRowVersion(trip, command.RowVersion);

        EnsureTripAccess(trip, actor);

        if (trip.Status == TripStatus.Closed || trip.Status == TripStatus.Cancelled)
        {
            throw new ConflictDomainException("Closed or cancelled trips cannot be corrected.");
        }

        if (toStatus == TripStatus.Draft)
        {
            throw new ConflictDomainException("Trips cannot be corrected to draft.");
        }

        if (trip.Status == toStatus)
        {
            throw new ConflictDomainException("Trip is already in that status.");
        }

        await EnsureValidEventAtAsync(trip, eventAt, cancellationToken);

        var fromStatus = trip.Status;
        var baseStatus = await ResolveCorrectionBaseStatusAsync(trip, cancellationToken);

        if (toStatus == TripStatus.Cancelled || toStatus == TripStatus.Closed)
        {
            throw new ConflictDomainException("Status correction cannot set a trip to cancelled or closed.");
        }

        if (toStatus == TripStatus.OnHold)
        {
            trip.HoldPreviousStatus = trip.Status;
            trip.Status = TripStatus.OnHold;
        }
        else if (toStatus == TripStatus.FailedAttempt)
        {
            if (!IsFailedAttemptEligible(baseStatus))
            {
                throw new ConflictDomainException("Failed attempt is only allowed during pickup or dropoff travel.");
            }

            trip.Status = TripStatus.FailedAttempt;
        }
        else
        {
            if (trip.DriverUserId is null)
            {
                throw new ConflictDomainException("Correcting to an operational status requires an assigned driver.");
            }

            EnsureForwardOnlyCorrection(baseStatus, toStatus);
            trip.Status = toStatus;
            if (fromStatus == TripStatus.OnHold)
            {
                trip.HoldPreviousStatus = null;
            }
        }

        if (trip.Status != TripStatus.OnHold)
        {
            trip.HoldPreviousStatus = null;
        }

        trip.UpdatedAt = DateTime.UtcNow;
        AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks.Trim(), eventAt, TripHistoryEventType.StatusCorrected);

        _auditService.AddEntry(
            actor.UserId,
            AuditActions.DispatchTripStatusCorrected,
            EntityTypes.DispatchTrip,
            trip.Id,
            null,
            new
            {
                FromStatus = fromStatus,
                ToStatus = trip.Status,
                EventAt = eventAt,
                Remarks = remarks.Trim()
            },
            tripId: trip.Id);

        await SaveChangesAsync(cancellationToken);
        return trip;
    }

    private static void EnsureManager(DispatchActorContext actor)
    {
        if (!actor.IsManager)
        {
            throw new ForbiddenDomainException("Manager role required.");
        }
    }

    private static void EnsureDispatcherOrManager(DispatchActorContext actor)
    {
        if (!actor.IsManager && !actor.IsDispatcher)
        {
            throw new ForbiddenDomainException("Dispatcher or Manager role required.");
        }
    }

    private static void EnsureScheduledStops(IReadOnlyCollection<DispatchTripStopInput>? stops)
    {
        if (stops is null || stops.Count == 0)
        {
            throw new BusinessRuleViolationException("Pickup and dropoff stops are required.");
        }

        var pickup = stops.FirstOrDefault(s => s.StopType == TripStopType.Pickup);
        var dropoff = stops.FirstOrDefault(s => s.StopType == TripStopType.Dropoff);

        if (pickup is null || dropoff is null)
        {
            throw new BusinessRuleViolationException("Both pickup and dropoff stops are required.");
        }

        if (string.IsNullOrWhiteSpace(pickup.LocationText) || string.IsNullOrWhiteSpace(dropoff.LocationText))
        {
            throw new BusinessRuleViolationException("Stop locations are required.");
        }

        if (!pickup.ScheduledAt.HasValue || !dropoff.ScheduledAt.HasValue)
        {
            throw new BusinessRuleViolationException("Scheduled pickup and dropoff times are required.");
        }

        if (pickup.ScheduledAt.Value >= dropoff.ScheduledAt.Value)
        {
            throw new BusinessRuleViolationException("Pickup time must be earlier than dropoff time.");
        }
    }

    private static void ApplyNonDraftScheduleUpdate(
        Trip trip,
        IReadOnlyCollection<DispatchTripStopInput> stops,
        DispatchActorContext actor,
        ref bool scheduleChanged)
    {
        var pickupInput = stops.FirstOrDefault(s => s.StopType == TripStopType.Pickup);
        var dropoffInput = stops.FirstOrDefault(s => s.StopType == TripStopType.Dropoff);

        if (pickupInput is null || dropoffInput is null)
        {
            throw new BusinessRuleViolationException("Pickup and dropoff stops are required.");
        }

        if (!pickupInput.ScheduledAt.HasValue || !dropoffInput.ScheduledAt.HasValue)
        {
            throw new BusinessRuleViolationException("Scheduled pickup and dropoff times are required.");
        }

        if (pickupInput.ScheduledAt.Value >= dropoffInput.ScheduledAt.Value)
        {
            throw new BusinessRuleViolationException("Pickup time must be earlier than dropoff time.");
        }

        var pickupStop = trip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Pickup);
        var dropoffStop = trip.Stops.FirstOrDefault(s => s.StopType == TripStopType.Dropoff);

        if (pickupStop is null || dropoffStop is null)
        {
            throw new ConflictDomainException("Trip stops cannot be changed after dispatch.");
        }

        if (actor.IsDispatcher)
        {
            if (!string.Equals(pickupStop.LocationText, pickupInput.LocationText, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(dropoffStop.LocationText, dropoffInput.LocationText, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictDomainException("Dispatchers cannot change stop locations after dispatch.");
            }
        }
        else
        {
            if (pickupStop.LocationText != pickupInput.LocationText)
            {
                scheduleChanged = true;
                pickupStop.LocationText = pickupInput.LocationText;
            }
            if (dropoffStop.LocationText != dropoffInput.LocationText)
            {
                scheduleChanged = true;
                dropoffStop.LocationText = dropoffInput.LocationText;
            }
        }

        if (pickupInput.ScheduledAt != pickupStop.ScheduledAt)
        {
            scheduleChanged = true;
            pickupStop.ScheduledAt = pickupInput.ScheduledAt;
        }

        if (dropoffInput.ScheduledAt != dropoffStop.ScheduledAt)
        {
            scheduleChanged = true;
            dropoffStop.ScheduledAt = dropoffInput.ScheduledAt;
        }
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

    private static void ApplyHold(Trip trip, DispatchActorContext actor, string? remarks)
    {
        if (!actor.IsManager && !actor.IsDriver)
        {
            throw new ForbiddenDomainException("Only drivers or managers can put trips on hold.");
        }

        if (trip.Status is TripStatus.Closed or TripStatus.Cancelled)
        {
            throw new ConflictDomainException("Closed or cancelled trips cannot be put on hold.");
        }

        if (string.IsNullOrWhiteSpace(remarks))
        {
            throw new BusinessRuleViolationException("Remarks are required to place a trip on hold.");
        }

        trip.HoldPreviousStatus = trip.Status;
        trip.Status = TripStatus.OnHold;
        trip.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyFailedAttempt(Trip trip, DispatchActorContext actor, string? remarks)
    {
        if (!actor.IsManager && !actor.IsDriver)
        {
            throw new ForbiddenDomainException("Only drivers or managers can mark failed attempts.");
        }

        if (string.IsNullOrWhiteSpace(remarks))
        {
            throw new BusinessRuleViolationException("Remarks are required to mark a failed attempt.");
        }

        if (!IsFailedAttemptEligible(trip.Status))
        {
            throw new ConflictDomainException("Failed attempt is only allowed during pickup or dropoff travel.");
        }

        trip.Status = TripStatus.FailedAttempt;
        trip.UpdatedAt = DateTime.UtcNow;
    }

    private static bool IsFailedAttemptEligible(TripStatus status)
    {
        return status is TripStatus.EnroutePickup
            or TripStatus.AtPickup
            or TripStatus.EnrouteDropoff
            or TripStatus.AtDropoff;
    }

    private async Task EnsureTransitionAllowedAsync(
        Trip trip,
        TripStatus toStatus,
        DispatchActorContext actor,
        string? remarks,
        bool allowDraftDispatch,
        CancellationToken cancellationToken)
    {
        var fromStatus = trip.Status;

        if (fromStatus is TripStatus.Closed or TripStatus.Cancelled)
        {
            throw new ConflictDomainException("Closed or cancelled trips cannot transition.");
        }

        if (toStatus == TripStatus.Draft)
        {
            throw new ConflictDomainException("Trips cannot transition back to draft.");
        }

        if (toStatus == TripStatus.Cancelled)
        {
            EnsureManager(actor);
            return;
        }

        if (fromStatus == TripStatus.OnHold)
        {
            EnsureManager(actor);
            if (trip.HoldPreviousStatus is null)
            {
                throw new ConflictDomainException("Hold resume status is missing.");
            }

            if (toStatus != trip.HoldPreviousStatus)
            {
                throw new ConflictDomainException("On-hold trips can only resume to the previous status.");
            }

            return;
        }

        if (fromStatus == TripStatus.FailedAttempt)
        {
            EnsureManager(actor);
            var previous = await GetFailedAttemptResumeStatusAsync(trip.Id, cancellationToken);
            if (previous is null)
            {
                throw new ConflictDomainException("Failed attempt resume status is missing.");
            }

            if (toStatus != previous.Value && toStatus != TripStatus.OnHold)
            {
                throw new ConflictDomainException("Failed attempt can only resume to the previous status or be put on hold.");
            }

            return;
        }

        if (!TransitionMap.TryGetValue(fromStatus, out var allowed) || allowed.Length == 0)
        {
            throw new ConflictDomainException("Trip status cannot transition to the requested status.");
        }

        if (Array.IndexOf(allowed, toStatus) < 0)
        {
            throw new ConflictDomainException("Trip status cannot transition to the requested status.");
        }

        if (toStatus == TripStatus.OnHold)
        {
            if (!actor.IsManager && !actor.IsDriver)
            {
                throw new ForbiddenDomainException("Only drivers or managers can put trips on hold.");
            }

            if (actor.IsDriver && !_options.AllowDriverOnHold)
            {
                throw new ForbiddenDomainException("Drivers are not allowed to place trips on hold.");
            }

            if (actor.IsDriver)
            {
                var driverAllowed = fromStatus is TripStatus.EnroutePickup
                    or TripStatus.AtPickup
                    or TripStatus.Loaded
                    or TripStatus.EnrouteDropoff
                    or TripStatus.AtDropoff;
                if (!driverAllowed)
                {
                    throw new ConflictDomainException("Drivers can only place trips on hold during active operations.");
                }
            }

            if (string.IsNullOrWhiteSpace(remarks))
            {
                throw new BusinessRuleViolationException("Remarks are required to place a trip on hold.");
            }

            return;
        }

        if (toStatus == TripStatus.FailedAttempt)
        {
            if (!actor.IsManager && !actor.IsDriver)
            {
                throw new ForbiddenDomainException("Only drivers or managers can mark failed attempts.");
            }

            if (string.IsNullOrWhiteSpace(remarks))
            {
                throw new BusinessRuleViolationException("Remarks are required to mark a failed attempt.");
            }

            if (!IsFailedAttemptEligible(fromStatus))
            {
                throw new ConflictDomainException("Failed attempt is only allowed during pickup or dropoff travel.");
            }

            return;
        }

        if (toStatus == TripStatus.Closed)
        {
            EnsureManager(actor);
            if (fromStatus != TripStatus.Delivered)
            {
                throw new ConflictDomainException("Trip must be delivered before closing.");
            }

            return;
        }

        if (IsOperationalStatus(toStatus))
        {
            if (fromStatus == TripStatus.Draft && !allowDraftDispatch)
            {
                throw new ConflictDomainException("Draft trips must be dispatched using the dispatch action.");
            }

            if (trip.DriverUserId is null)
            {
                throw new ConflictDomainException("Dispatching requires an assigned driver.");
            }

            if (fromStatus == TripStatus.Draft && allowDraftDispatch)
            {
                return;
            }

            var expected = GetNextOperationalStatus(fromStatus);
            if (expected is null || expected.Value != toStatus)
            {
                throw new ConflictDomainException("Trip must advance to the next operational status.");
            }
        }
    }

    private static bool IsOperationalStatus(TripStatus status)
    {
        return Array.IndexOf(OperationalFlow, status) >= 0;
    }

    private static TripStatus? GetNextOperationalStatus(TripStatus status)
    {
        var index = Array.IndexOf(OperationalFlow, status);
        if (index < 0 || index + 1 >= OperationalFlow.Length)
        {
            return null;
        }

        return OperationalFlow[index + 1];
    }

    private static void EnsureForwardOnlyCorrection(TripStatus fromStatus, TripStatus toStatus)
    {
        var toIndex = Array.IndexOf(OperationalFlow, toStatus);
        if (toIndex < 0)
        {
            throw new ConflictDomainException("Status correction target must be an operational status.");
        }

        if (fromStatus == TripStatus.Draft)
        {
            return;
        }

        var fromIndex = Array.IndexOf(OperationalFlow, fromStatus);
        if (fromIndex < 0)
        {
            throw new ConflictDomainException("Status correction cannot move from the current status.");
        }

        if (toIndex <= fromIndex)
        {
            throw new ConflictDomainException("Status correction must move forward.");
        }
    }

    private async Task<TripStatus> ResolveCorrectionBaseStatusAsync(Trip trip, CancellationToken cancellationToken)
    {
        if (trip.Status == TripStatus.OnHold && trip.HoldPreviousStatus.HasValue)
        {
            return trip.HoldPreviousStatus.Value;
        }

        if (trip.Status == TripStatus.FailedAttempt)
        {
            var previous = await GetFailedAttemptResumeStatusAsync(trip.Id, cancellationToken);
            if (previous.HasValue)
            {
                return previous.Value;
            }

            throw new ConflictDomainException("Failed attempt resume status is missing.");
        }

        return trip.Status;
    }

    private static (DateTime PickupAt, DateTime DropoffAt) GetScheduledWindow(IEnumerable<TripStop> stops)
    {
        var pickup = stops.FirstOrDefault(s => s.StopType == TripStopType.Pickup)?.ScheduledAt;
        var dropoff = stops.FirstOrDefault(s => s.StopType == TripStopType.Dropoff)?.ScheduledAt;

        if (!pickup.HasValue || !dropoff.HasValue)
        {
            throw new BusinessRuleViolationException("Scheduled pickup and dropoff times are required.");
        }

        if (pickup.Value >= dropoff.Value)
        {
            throw new BusinessRuleViolationException("Pickup time must be earlier than dropoff time.");
        }

        return (pickup.Value, dropoff.Value);
    }

    private sealed record AssignmentConflictDetail(
        Guid TripId,
        Guid? DriverUserId,
        string? DriverUsername,
        Guid? TruckAssetId,
        string? TruckAssetCode,
        DateTime WindowStart,
        DateTime WindowEnd);

    private async Task<IReadOnlyCollection<AssignmentConflictDetail>> FindScheduleConflictsAsync(
        Guid tripId,
        Guid? driverUserId,
        Guid? truckAssetId,
        DateTime pickupAt,
        DateTime dropoffAt,
        CancellationToken cancellationToken)
    {
        if (!driverUserId.HasValue && !truckAssetId.HasValue)
        {
            return Array.Empty<AssignmentConflictDetail>();
        }

        var query = _dbContext.DispatchTrips
            .AsNoTracking()
            .Where(t => t.Id != tripId && t.Status != TripStatus.Cancelled && t.Status != TripStatus.Closed);

        if (driverUserId.HasValue && truckAssetId.HasValue)
        {
            query = query.Where(t => t.DriverUserId == driverUserId.Value || t.TruckAssetId == truckAssetId.Value);
        }
        else if (driverUserId.HasValue)
        {
            query = query.Where(t => t.DriverUserId == driverUserId.Value);
        }
        else if (truckAssetId.HasValue)
        {
            query = query.Where(t => t.TruckAssetId == truckAssetId.Value);
        }

        return await query
            .Select(t => new
            {
                t.Id,
                t.DriverUserId,
                DriverUsername = t.Driver != null ? t.Driver.Username : null,
                t.TruckAssetId,
                TruckAssetCode = t.TruckAsset != null ? t.TruckAsset.AssetCode : null,
                Pickup = t.Stops.Where(s => s.StopType == TripStopType.Pickup).Select(s => s.ScheduledAt).FirstOrDefault(),
                Dropoff = t.Stops.Where(s => s.StopType == TripStopType.Dropoff).Select(s => s.ScheduledAt).FirstOrDefault()
            })
            .Where(t => t.Pickup.HasValue && t.Dropoff.HasValue && t.Pickup.Value < dropoffAt && t.Dropoff.Value > pickupAt)
            .Select(t => new AssignmentConflictDetail(
                t.Id,
                t.DriverUserId,
                t.DriverUsername,
                t.TruckAssetId,
                t.TruckAssetCode,
                t.Pickup!.Value,
                t.Dropoff!.Value))
            .ToListAsync(cancellationToken);
    }

    private async Task EnforceAssignmentConflictsAsync(
        Guid tripId,
        Guid? driverUserId,
        Guid? truckAssetId,
        DateTime pickupAt,
        DateTime dropoffAt,
        DispatchActorContext actor,
        string? remarks,
        CancellationToken cancellationToken)
    {
        var conflicts = await FindScheduleConflictsAsync(
            tripId,
            driverUserId,
            truckAssetId,
            pickupAt,
            dropoffAt,
            cancellationToken);

        if (conflicts.Count == 0)
        {
            return;
        }

        if (!actor.IsManager)
        {
            var summary = BuildConflictSummary(conflicts);
            throw new ConflictDomainException(summary, BuildConflictDetails(summary, conflicts));
        }

        if (string.IsNullOrWhiteSpace(remarks))
        {
            throw new BusinessRuleViolationException("Remarks are required to override assignment conflicts.");
        }

        _auditService.AddEntry(
            actor.UserId,
            AuditActions.DispatchTripConflictOverride,
            EntityTypes.DispatchTrip,
            tripId,
            null,
            new
            {
                DriverUserId = driverUserId,
                TruckAssetId = truckAssetId,
                PickupAt = pickupAt,
                DropoffAt = dropoffAt,
                Conflicts = conflicts.Select(conflict => new
                {
                    conflict.TripId,
                    conflict.DriverUserId,
                    conflict.DriverUsername,
                    conflict.TruckAssetId,
                    conflict.TruckAssetCode,
                    conflict.WindowStart,
                    conflict.WindowEnd
                }).ToList(),
                Remarks = remarks.Trim()
            },
            tripId: tripId);
    }

    private static object BuildConflictDetails(
        string summary,
        IReadOnlyCollection<AssignmentConflictDetail> conflicts)
    {
        return new
        {
            Summary = summary,
            Conflicts = conflicts.Select(conflict => new
            {
                conflict.TripId,
                TripReference = BuildTripReference(conflict.TripId),
                conflict.DriverUserId,
                conflict.DriverUsername,
                conflict.TruckAssetId,
                conflict.TruckAssetCode,
                WindowStart = conflict.WindowStart,
                WindowEnd = conflict.WindowEnd,
                Message = BuildConflictItemMessage(conflict)
            }).ToList()
        };
    }

    private static string BuildConflictSummary(IReadOnlyCollection<AssignmentConflictDetail> conflicts)
    {
        if (conflicts.Count == 1)
        {
            var conflict = conflicts.First();
            return $"{BuildAssignmentLabel(conflict)} is already assigned from {conflict.WindowStart:HH:mm} to {conflict.WindowEnd:HH:mm}.";
        }

        return "Scheduling conflict: the selected driver or truck is already assigned in overlapping windows.";
    }

    private static string BuildConflictItemMessage(AssignmentConflictDetail conflict)
    {
        return $"{BuildAssignmentLabel(conflict)} is assigned to Trip {BuildTripReference(conflict.TripId)} from {conflict.WindowStart:HH:mm} to {conflict.WindowEnd:HH:mm}.";
    }

    private static string BuildAssignmentLabel(AssignmentConflictDetail conflict)
    {
        var driverLabel = !string.IsNullOrWhiteSpace(conflict.DriverUsername)
            ? $"Driver {conflict.DriverUsername}"
            : null;
        var truckLabel = !string.IsNullOrWhiteSpace(conflict.TruckAssetCode)
            ? $"truck {conflict.TruckAssetCode}"
            : null;

        if (!string.IsNullOrWhiteSpace(driverLabel) && !string.IsNullOrWhiteSpace(truckLabel))
        {
            return $"{driverLabel} or {truckLabel}";
        }

        if (!string.IsNullOrWhiteSpace(driverLabel))
        {
            return driverLabel;
        }

        if (!string.IsNullOrWhiteSpace(truckLabel))
        {
            return truckLabel;
        }

        return "The selected assignment";
    }

    private static string BuildTripReference(Guid tripId)
    {
        return tripId.ToString("N").Substring(0, 8).ToUpperInvariant();
    }

    private async Task<TripStatus?> GetFailedAttemptResumeStatusAsync(Guid tripId, CancellationToken cancellationToken)
    {
        return await _dbContext.DispatchTripStatusHistories
            .AsNoTracking()
            .Where(history => history.TripId == tripId && history.ToStatus == TripStatus.FailedAttempt)
            .OrderByDescending(history => history.EventAt)
            .Select(history => (TripStatus?)history.FromStatus)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private void AddHistory(
        Guid tripId,
        TripStatus fromStatus,
        TripStatus toStatus,
        Guid actorUserId,
        string? remarks,
        DateTime eventAt,
        TripHistoryEventType eventType = TripHistoryEventType.StatusChange)
    {
        _dbContext.DispatchTripStatusHistories.Add(new TripStatusHistory
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            EventType = eventType,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorUserId = actorUserId,
            Remarks = remarks,
            EventAt = eventAt,
            RecordedAt = DateTime.UtcNow
        });
    }

    private void AddStatusAudit(Guid actorUserId, Guid tripId, TripStatus toStatus)
    {
        var action = toStatus switch
        {
            TripStatus.Cancelled => AuditActions.DispatchTripCancelled,
            TripStatus.OnHold => AuditActions.DispatchTripOnHold,
            TripStatus.Closed => AuditActions.DispatchTripClosed,
            _ => AuditActions.DispatchTripStatusChanged
        };

        _auditService.AddEntry(
            actorUserId,
            action,
            EntityTypes.DispatchTrip,
            tripId,
            null,
            new { Status = toStatus },
            tripId: tripId);
    }

    private static void RequirePodPendingRemarks(bool podPending, string? remarks)
    {
        if (podPending && string.IsNullOrWhiteSpace(remarks))
        {
            throw new BusinessRuleViolationException("Remarks are required when marking POD pending.");
        }
    }

    private static DispatchTripStopInput? ResolveStopInput(
        UpdateDispatchTripCommand command,
        Trip trip,
        TripStopType stopType)
    {
        var fromCommand = command.Stops?.FirstOrDefault(stop => stop.StopType == stopType);
        if (fromCommand is not null)
        {
            return fromCommand;
        }

        var stop = trip.Stops.FirstOrDefault(s => s.StopType == stopType);
        return stop is null
            ? null
            : new DispatchTripStopInput(stop.StopType, stop.LocationText, stop.ScheduledAt);
    }

    private static string? BuildScheduleChangeSummary(
        Guid originalCustomerId,
        Guid? originalDriverUserId,
        Guid? originalTruckAssetId,
        string? originalNotes,
        TripStop? originalPickup,
        TripStop? originalDropoff,
        UpdateDispatchTripCommand command,
        Trip trip)
    {
        var changes = new List<string>();

        void AddChange(string label, string? before, string? after)
        {
            if (before == after)
            {
                return;
            }

            changes.Add($"{label}: {before ?? "—"} → {after ?? "—"}");
        }

        AddChange("Customer", originalCustomerId.ToString(), trip.CustomerId.ToString());
        AddChange("Driver", originalDriverUserId?.ToString(), trip.DriverUserId?.ToString());
        AddChange("Truck", originalTruckAssetId?.ToString(), trip.TruckAssetId?.ToString());
        AddChange("Notes", originalNotes, trip.Notes);

        var updatedPickup = ResolveStopInput(command, trip, TripStopType.Pickup);
        var updatedDropoff = ResolveStopInput(command, trip, TripStopType.Dropoff);

        AddChange("Pickup location", originalPickup?.LocationText, updatedPickup?.LocationText);
        AddChange("Pickup time", FormatTime(originalPickup?.ScheduledAt), FormatTime(updatedPickup?.ScheduledAt));
        AddChange("Dropoff location", originalDropoff?.LocationText, updatedDropoff?.LocationText);
        AddChange("Dropoff time", FormatTime(originalDropoff?.ScheduledAt), FormatTime(updatedDropoff?.ScheduledAt));

        return changes.Count == 0 ? null : $"Schedule updated: {string.Join("; ", changes)}";
    }

    private static string? FormatTime(DateTime? value)
    {
        return value?.ToString("yyyy-MM-dd HH:mm");
    }

    private async Task<TripStatus> ResolveReassignmentStatusAsync(Trip trip, CancellationToken cancellationToken)
    {
        if (trip.Status == TripStatus.OnHold && trip.HoldPreviousStatus.HasValue)
        {
            return trip.HoldPreviousStatus.Value;
        }

        if (trip.Status == TripStatus.FailedAttempt)
        {
            var previous = await GetFailedAttemptResumeStatusAsync(trip.Id, cancellationToken);
            if (previous.HasValue)
            {
                return previous.Value;
            }
        }

        return trip.Status;
    }

    private static bool IsLoadedOrLater(TripStatus status)
    {
        return status is TripStatus.Loaded
            or TripStatus.EnrouteDropoff
            or TripStatus.AtDropoff
            or TripStatus.Delivered;
    }

    private static void RequireReassignmentRemarks(string? remarks)
    {
        if (string.IsNullOrWhiteSpace(remarks))
        {
            throw new BusinessRuleViolationException("Remarks are required to reassign after loading.");
        }
    }

    private static string? BuildHistoryRemarks(string? summary, string? remarks)
    {
        if (string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(remarks))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(remarks))
        {
            return summary;
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            return $"Remarks: {remarks}";
        }

        return $"{summary} | Remarks: {remarks}";
    }

    private async Task EnsureValidEventAtAsync(Trip trip, DateTime eventAt, CancellationToken cancellationToken)
    {
        if (eventAt == default)
        {
            throw new BusinessRuleViolationException("EventAt is required.");
        }

        var now = DateTime.UtcNow;
        if (eventAt > now.AddMinutes(5))
        {
            throw new BusinessRuleViolationException("EventAt cannot be far in the future.");
        }

        if (eventAt < trip.CreatedAt)
        {
            throw new BusinessRuleViolationException("EventAt cannot be earlier than trip creation.");
        }

        var lastEventAt = await _dbContext.DispatchTripStatusHistories
            .AsNoTracking()
            .Where(history => history.TripId == trip.Id)
            .OrderByDescending(history => history.EventAt)
            .Select(history => (DateTime?)history.EventAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastEventAt.HasValue && eventAt < lastEventAt.Value)
        {
            throw new BusinessRuleViolationException("EventAt must be on or after the last recorded event time.");
        }
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("The trip was updated by another user. Please refresh and retry.");
        }
    }

    private void ApplyRowVersion(Trip trip, byte[] rowVersion)
    {
        if (rowVersion is null || rowVersion.Length == 0)
        {
            throw new BusinessRuleViolationException("RowVersion is required.");
        }

        _dbContext.Entry(trip).Property(t => t.RowVersion).OriginalValue = rowVersion;
    }
}

