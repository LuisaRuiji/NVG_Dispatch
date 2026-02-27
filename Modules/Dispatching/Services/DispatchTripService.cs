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
    string? Remarks);

public sealed record DispatchTripCommand(
    Guid TripId,
    Guid DriverUserId,
    Guid? TruckAssetId,
    string? Remarks);

public sealed record ChangeDispatchTripStatusCommand(
    Guid TripId,
    TripStatus ToStatus,
    string? Remarks,
    bool? PodPendingOverride);

public sealed record UploadTripDocumentCommand(
    Guid TripId,
    TripDocumentType Type,
    string StorageKey);

public sealed record VerifyTripDocumentCommand(Guid TripId, Guid DocumentId);

public sealed record RejectTripDocumentCommand(Guid TripId, Guid DocumentId, string Remarks);

public sealed class DispatchTripService
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
    private readonly UserService _userService;
    private readonly IAuditService? _auditService;
    private readonly DispatchingOptions _options;

    public DispatchTripService(
        InventoryDbContext dbContext,
        UserService userService,
        IOptions<DispatchingOptions> options,
        IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _userService = userService;
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

        _auditService?.AddEntry(
            actor.UserId,
            AuditActions.DispatchTripCreated,
            EntityTypes.DispatchTrip,
            trip.Id,
            null,
            new { trip.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
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
            AddHistory(
                trip.Id,
                trip.Status,
                trip.Status,
                actor.UserId,
                historyRemarks ?? "Schedule updated.",
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

            _auditService?.AddEntry(
                actor.UserId,
                AuditActions.DispatchTripUpdated,
                EntityTypes.DispatchTrip,
                trip.Id,
                before,
                after);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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

        var now = DateTime.UtcNow;
        var fromStatus = trip.Status;
        trip.DriverUserId = command.DriverUserId;
        trip.TruckAssetId = command.TruckAssetId;
        trip.Status = TripStatus.Dispatched;
        trip.UpdatedAt = now;

        AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, command.Remarks);

        _auditService?.AddEntry(
            actor.UserId,
            AuditActions.DispatchTripDispatched,
            EntityTypes.DispatchTrip,
            trip.Id,
            null,
            new { trip.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return trip;
    }

    public async Task<Trip> ChangeStatusAsync(
        ChangeDispatchTripStatusCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == command.TripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

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
            AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks);
            AddStatusAudit(actor.UserId, trip.Id, trip.Status);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return trip;
        }

        if (toStatus == TripStatus.OnHold)
        {
            ApplyHold(trip, actor, remarks);
            AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks);
            AddStatusAudit(actor.UserId, trip.Id, trip.Status);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return trip;
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

            trip.Status = toStatus;
            trip.HoldPreviousStatus = null;
            trip.UpdatedAt = DateTime.UtcNow;
            AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks);
            AddStatusAudit(actor.UserId, trip.Id, trip.Status);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return trip;
        }

        if (toStatus == TripStatus.FailedAttempt)
        {
            ApplyFailedAttempt(trip, actor);
            AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks);
            AddStatusAudit(actor.UserId, trip.Id, trip.Status);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return trip;
        }

        if (toStatus == TripStatus.Cancelled)
        {
            EnsureManager(actor);
            trip.Status = TripStatus.Cancelled;
            trip.UpdatedAt = DateTime.UtcNow;
            AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks);
            AddStatusAudit(actor.UserId, trip.Id, trip.Status);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return trip;
        }

        if (toStatus == TripStatus.Closed)
        {
            EnsureManager(actor);
            if (fromStatus != TripStatus.Delivered)
            {
                throw new ConflictDomainException("Trip must be delivered before closing.");
            }

            var podState = GetPodState(trip);
            if (_options.DocVerificationEnabled)
            {
                if (podState != TripDocumentState.Verified)
                {
                    throw new ConflictDomainException("Closing requires a verified POD.");
                }
            }
            else
            {
                var podUploaded = podState is TripDocumentState.Uploaded or TripDocumentState.Verified;
                if (!podUploaded && !trip.PodPending)
                {
                    throw new ConflictDomainException("Closing requires POD upload or POD pending.");
                }
            }

            trip.Status = TripStatus.Closed;
            trip.UpdatedAt = DateTime.UtcNow;
            AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks);
            AddStatusAudit(actor.UserId, trip.Id, trip.Status);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return trip;
        }

        if (toStatus == TripStatus.Delivered)
        {
            ValidateOperationalTransition(trip, actor, toStatus);

            if (command.PodPendingOverride.HasValue)
            {
                EnsureManager(actor);
                RequirePodPendingRemarks(command.PodPendingOverride.Value, remarks);
                trip.PodPending = command.PodPendingOverride.Value;
            }

            var podState = GetPodState(trip);
            var podUploaded = podState is TripDocumentState.Uploaded or TripDocumentState.Verified;
            if (!podUploaded && !trip.PodPending)
            {
                throw new ConflictDomainException("Delivered requires POD upload or POD pending.");
            }
        }
        else if (command.PodPendingOverride.HasValue)
        {
            EnsureManager(actor);
            RequirePodPendingRemarks(command.PodPendingOverride.Value, remarks);
            trip.PodPending = command.PodPendingOverride.Value;
        }

        ValidateOperationalTransition(trip, actor, toStatus);

        trip.Status = toStatus;
        trip.UpdatedAt = DateTime.UtcNow;
        AddHistory(trip.Id, fromStatus, trip.Status, actor.UserId, remarks);
        AddStatusAudit(actor.UserId, trip.Id, trip.Status);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return trip;
    }

    public async Task<TripDocument> UploadDocumentAsync(
        UploadTripDocumentCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsDriver)
        {
            throw new ForbiddenDomainException("Only drivers can upload trip documents.");
        }

        var trip = await _dbContext.DispatchTrips
            .FirstOrDefaultAsync(t => t.Id == command.TripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        EnsureTripAccess(trip, actor);

        if (trip.Status is TripStatus.Cancelled or TripStatus.Closed)
        {
            throw new ConflictDomainException("Documents cannot be uploaded for closed or cancelled trips.");
        }

        if (string.IsNullOrWhiteSpace(command.StorageKey))
        {
            throw new BusinessRuleViolationException("Storage key is required.");
        }

        var now = DateTime.UtcNow;
        var existing = await _dbContext.DispatchTripDocuments
            .FirstOrDefaultAsync(d => d.TripId == trip.Id && d.Type == command.Type, cancellationToken);

        if (existing is null)
        {
            existing = new TripDocument
            {
                Id = Guid.NewGuid(),
                TripId = trip.Id,
                Type = command.Type,
                State = TripDocumentState.Uploaded,
                StorageKey = command.StorageKey,
                UploadedByUserId = actor.UserId,
                UploadedAt = now
            };
            _dbContext.DispatchTripDocuments.Add(existing);
        }
        else
        {
            existing.StorageKey = command.StorageKey;
            existing.State = TripDocumentState.Uploaded;
            existing.UploadedByUserId = actor.UserId;
            existing.UploadedAt = now;
            existing.VerifiedByUserId = null;
            existing.VerifiedAt = null;
            existing.RejectedByUserId = null;
            existing.RejectedAt = null;
            existing.Remarks = null;
        }

        _auditService?.AddEntry(
            actor.UserId,
            AuditActions.DispatchTripDocumentUploaded,
            EntityTypes.DispatchTripDocument,
            existing.Id,
            null,
            new { existing.Type, existing.State, TripId = trip.Id });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<TripDocument> VerifyDocumentAsync(
        VerifyTripDocumentCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsManager && !actor.IsFinance)
        {
            throw new ForbiddenDomainException("Only finance or manager can verify documents.");
        }

        var doc = await _dbContext.DispatchTripDocuments
            .Include(d => d.Trip)
            .FirstOrDefaultAsync(d => d.Id == command.DocumentId && d.TripId == command.TripId, cancellationToken);

        if (doc is null)
        {
            throw new NotFoundException("Document not found.");
        }

        if (doc.State != TripDocumentState.Uploaded)
        {
            throw new ConflictDomainException("Only uploaded documents can be verified.");
        }

        doc.State = TripDocumentState.Verified;
        doc.VerifiedByUserId = actor.UserId;
        doc.VerifiedAt = DateTime.UtcNow;
        doc.RejectedByUserId = null;
        doc.RejectedAt = null;

        _auditService?.AddEntry(
            actor.UserId,
            AuditActions.DispatchTripDocumentVerified,
            EntityTypes.DispatchTripDocument,
            doc.Id,
            null,
            new { doc.Type, doc.State, TripId = doc.TripId });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return doc;
    }

    public async Task<TripDocument> RejectDocumentAsync(
        RejectTripDocumentCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsManager && !actor.IsFinance)
        {
            throw new ForbiddenDomainException("Only finance or manager can reject documents.");
        }

        if (string.IsNullOrWhiteSpace(command.Remarks))
        {
            throw new BusinessRuleViolationException("Remarks are required to reject a document.");
        }

        var doc = await _dbContext.DispatchTripDocuments
            .Include(d => d.Trip)
            .FirstOrDefaultAsync(d => d.Id == command.DocumentId && d.TripId == command.TripId, cancellationToken);

        if (doc is null)
        {
            throw new NotFoundException("Document not found.");
        }

        if (doc.State != TripDocumentState.Uploaded)
        {
            throw new ConflictDomainException("Only uploaded documents can be rejected.");
        }

        doc.State = TripDocumentState.Rejected;
        doc.RejectedByUserId = actor.UserId;
        doc.RejectedAt = DateTime.UtcNow;
        doc.VerifiedByUserId = null;
        doc.VerifiedAt = null;
        doc.Remarks = command.Remarks.Trim();

        _auditService?.AddEntry(
            actor.UserId,
            AuditActions.DispatchTripDocumentRejected,
            EntityTypes.DispatchTripDocument,
            doc.Id,
            null,
            new { doc.Type, doc.State, TripId = doc.TripId });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return doc;
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

        if (actor.IsDriver && string.IsNullOrWhiteSpace(remarks))
        {
            throw new BusinessRuleViolationException("Remarks are required to place a trip on hold.");
        }

        trip.HoldPreviousStatus = trip.Status;
        trip.Status = TripStatus.OnHold;
        trip.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyFailedAttempt(Trip trip, DispatchActorContext actor)
    {
        if (!actor.IsManager && !actor.IsDriver)
        {
            throw new ForbiddenDomainException("Only drivers or managers can mark failed attempts.");
        }

        if (trip.Status is not (TripStatus.EnroutePickup or TripStatus.AtPickup or TripStatus.EnrouteDropoff or TripStatus.AtDropoff))
        {
            throw new ConflictDomainException("Failed attempt is only allowed during pickup or dropoff travel.");
        }

        trip.Status = TripStatus.FailedAttempt;
        trip.UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateOperationalTransition(Trip trip, DispatchActorContext actor, TripStatus toStatus)
    {
        var fromStatus = trip.Status;
        var fromIndex = Array.IndexOf(OperationalFlow, fromStatus);
        var toIndex = Array.IndexOf(OperationalFlow, toStatus);

        if (toIndex < 0)
        {
            return;
        }

        if (fromStatus == TripStatus.Draft)
        {
            if (toStatus != TripStatus.Dispatched)
            {
                throw new ConflictDomainException("Trip must be dispatched before starting operations.");
            }

            if (trip.DriverUserId is null)
            {
                throw new ConflictDomainException("Dispatching requires an assigned driver.");
            }

            return;
        }

        if (fromIndex < 0)
        {
            throw new ConflictDomainException("Trip status cannot transition to the requested operational status.");
        }

        if (actor.IsDriver)
        {
            var expected = fromIndex + 1 < OperationalFlow.Length ? OperationalFlow[fromIndex + 1] : (TripStatus?)null;
            if (expected is null || expected.Value != toStatus)
            {
                throw new ConflictDomainException("Drivers can only advance to the next operational status.");
            }

            return;
        }

        if (toIndex <= fromIndex)
        {
            throw new ConflictDomainException("Trip status cannot move backward.");
        }
    }

    private static TripDocumentState? GetPodState(Trip trip)
    {
        var pod = trip.Documents.FirstOrDefault(d => d.Type == TripDocumentType.Pod);
        return pod?.State;
    }

    private async Task<TripStatus?> GetFailedAttemptResumeStatusAsync(Guid tripId, CancellationToken cancellationToken)
    {
        return await _dbContext.DispatchTripStatusHistories
            .AsNoTracking()
            .Where(history => history.TripId == tripId && history.ToStatus == TripStatus.FailedAttempt)
            .OrderByDescending(history => history.CreatedAt)
            .Select(history => (TripStatus?)history.FromStatus)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private void AddHistory(
        Guid tripId,
        TripStatus fromStatus,
        TripStatus toStatus,
        Guid actorUserId,
        string? remarks,
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
            CreatedAt = DateTime.UtcNow
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

        _auditService?.AddEntry(
            actorUserId,
            action,
            EntityTypes.DispatchTrip,
            tripId,
            null,
            new { Status = toStatus });
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
}
