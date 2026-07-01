using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Hubs;
using NVGInventory.Hubs.Events;
using NVGInventory.Modules.Dispatching.Contracts;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Services;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed class DispatchDocumentWorkflowService : IDispatchDocumentWorkflowService, IDispatchDocumentReadService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService? _auditService;
    private readonly IServiceScopeFactory? _serviceScopeFactory;

    public DispatchDocumentWorkflowService(
        InventoryDbContext dbContext,
        IAuditService? auditService = null,
        IServiceScopeFactory? serviceScopeFactory = null)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<TripDocument> UploadDocumentAsync(
        UploadTripDocumentCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips
            .FirstOrDefaultAsync(t => t.Id == command.TripId, cancellationToken);

        if (trip is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        ValidateDocumentUploadPermission(command.Type, actor, trip);

        if (trip.Status is TripStatus.Cancelled or TripStatus.Closed)
        {
            throw new ConflictDomainException("Documents cannot be uploaded for closed or cancelled trips.");
        }

        if (string.IsNullOrWhiteSpace(command.StorageKey))
        {
            throw new BusinessRuleViolationException("Storage key is required.");
        }

        var now = DateTime.UtcNow;
        var active = await _dbContext.DispatchTripDocuments
            .FirstOrDefaultAsync(
                d => d.TripId == trip.Id && d.Type == command.Type && d.IsActive,
                cancellationToken);

        if (active is not null)
        {
            active.IsActive = false;
        }

        var doc = new TripDocument
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            SupersedesDocumentId = active?.Id,
            IsActive = true,
            Type = command.Type,
            State = TripDocumentState.Uploaded,
            StorageKey = command.StorageKey,
            UploadedByUserId = actor.UserId,
            UploadedAt = now
        };

        _dbContext.DispatchTripDocuments.Add(doc);

        _auditService?.AddEntry(
            actor.UserId,
            AuditActions.DispatchTripDocumentUploaded,
            EntityTypes.DispatchTripDocument,
            doc.Id,
            null,
            new { doc.Type, doc.State, TripId = trip.Id },
            tripId: trip.Id);

        await SaveChangesAsync(cancellationToken);
        QueueDocumentUploadedBroadcast(doc.Id);
        return doc;
    }

    public async Task<TripDocument> VerifyDocumentAsync(
        VerifyTripDocumentCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsManager && !actor.IsDispatcher && !actor.IsAdmin)
        {
            throw new ForbiddenDomainException("Only dispatcher, manager, or admin can verify documents.");
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

        if (!doc.IsActive)
        {
            throw new ConflictDomainException("Only active documents can be verified.");
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
            new { doc.Type, doc.State, TripId = doc.TripId },
            tripId: doc.TripId);

        await SaveChangesAsync(cancellationToken);
        QueueDocumentVerifiedBroadcast(doc.Id, isVerified: true);
        return doc;
    }

    public async Task<TripDocument> RejectDocumentAsync(
        RejectTripDocumentCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        if (!actor.IsManager && !actor.IsDispatcher && !actor.IsAdmin)
        {
            throw new ForbiddenDomainException("Only dispatcher, manager, or admin can reject documents.");
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

        if (!doc.IsActive)
        {
            throw new ConflictDomainException("Only active documents can be rejected.");
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
            new { doc.Type, doc.State, TripId = doc.TripId },
            tripId: doc.TripId);

        await SaveChangesAsync(cancellationToken);
        if (doc.Trip?.DriverUserId.HasValue == true)
        {
            QueuePushToUser(
                doc.Trip.DriverUserId.Value,
                "Document Rejected",
                $"Your {DispatchEventFormatting.DocumentType(doc.Type)} was rejected - please resubmit",
                new Dictionary<string, string>
                {
                    ["tripId"] = doc.TripId.ToString(),
                    ["documentType"] = DispatchEventFormatting.DocumentType(doc.Type)
                });
        }
        QueueDocumentVerifiedBroadcast(doc.Id, isVerified: false);
        return doc;
    }

    public Task<bool> HasVerifiedPodAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .AnyAsync(
                d => d.TripId == tripId
                     && d.IsActive
                     && d.Type == TripDocumentType.Pod
                     && d.State == TripDocumentState.Verified,
                cancellationToken);
    }

    public Task<bool> HasUploadedOrVerifiedPodAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .AnyAsync(
                d => d.TripId == tripId
                     && d.IsActive
                     && d.Type == TripDocumentType.Pod
                     && (d.State == TripDocumentState.Uploaded || d.State == TripDocumentState.Verified),
                cancellationToken);
    }

    public Task<bool> HasVerifiedDocumentAsync(
        Guid tripId,
        TripDocumentType type,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.DispatchTripDocuments
            .AsNoTracking()
            .AnyAsync(
                d => d.TripId == tripId
                     && d.IsActive
                     && d.Type == type
                     && d.State == TripDocumentState.Verified,
                cancellationToken);
    }

    private static void ValidateDocumentUploadPermission(
        TripDocumentType documentType,
        DispatchActorContext actor,
        Trip trip)
    {
        switch (documentType)
        {
            case TripDocumentType.Waybill:
                throw new ConflictDomainException("Waybill is system-generated. Use the generate waybill endpoint.");

            case TripDocumentType.Atw:
                if (actor.IsDispatcher || actor.IsManager || actor.IsAdmin)
                {
                    return;
                }

                throw new ForbiddenDomainException("Only dispatchers, managers, or admins can upload ATW.");

            case TripDocumentType.Eir:
            case TripDocumentType.GatePass:
                EnsureAssignedDriver(actor, trip, "Only the assigned driver can upload EIR and Gate Pass.");
                EnsureStatusAtLeast(trip.Status, TripStatus.AtPickup, "EIR and Gate Pass can only be uploaded after pickup.");
                return;

            case TripDocumentType.Dr:
                EnsureAssignedDriver(actor, trip, "Only the assigned driver can upload DR.");
                EnsureStatusAtLeast(trip.Status, TripStatus.AtDropoff, "DR can only be uploaded after dropoff arrival.");
                return;

            case TripDocumentType.Pod:
                if (actor.IsDispatcher)
                {
                    EnsureStatusAtLeast(trip.Status, TripStatus.Delivered, "POD can only be uploaded after delivery.");
                    return;
                }

                EnsureAssignedDriver(actor, trip, "Only the assigned driver or dispatcher can upload POD.");
                EnsureStatusAtLeast(trip.Status, TripStatus.Delivered, "POD can only be uploaded after delivery.");
                return;

            default:
                throw new BusinessRuleViolationException("Unsupported document type.");
        }
    }

    private static void EnsureAssignedDriver(DispatchActorContext actor, Trip trip, string message)
    {
        if (actor.IsDriver && trip.DriverUserId == actor.UserId)
        {
            return;
        }

        throw new ForbiddenDomainException(message);
    }

    private static void EnsureStatusAtLeast(TripStatus current, TripStatus required, string message)
    {
        if (StatusRank(current) >= StatusRank(required))
        {
            return;
        }

        throw new ConflictDomainException(message);
    }

    private static int StatusRank(TripStatus status)
    {
        return status switch
        {
            TripStatus.Draft => 0,
            TripStatus.Dispatched => 1,
            TripStatus.EnroutePickup => 2,
            TripStatus.AtPickup => 3,
            TripStatus.Loaded => 4,
            TripStatus.EnrouteDropoff => 5,
            TripStatus.AtDropoff => 6,
            TripStatus.Delivered => 7,
            TripStatus.Closed => 8,
            _ => -1
        };
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

    private void QueueDocumentUploadedBroadcast(Guid documentId)
    {
        if (_serviceScopeFactory is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DispatchDocumentWorkflowService>>();
            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                var hubContext = scope.ServiceProvider
                    .GetRequiredService<IHubContext<VaiaDispatchHub, IVaiaDispatchClient>>();
                var doc = await dbContext.DispatchTripDocuments
                    .Include(item => item.Trip)
                    .Include(item => item.UploadedBy)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == documentId);
                if (doc?.Trip is null)
                {
                    return;
                }

                var e = new DocumentUploadedEvent(
                    doc.TripId,
                    doc.Trip.ContainerNumber ?? string.Empty,
                    DispatchEventFormatting.DocumentType(doc.Type),
                    doc.UploadedBy?.Username ?? "Unknown user",
                    doc.UploadedAt);

                await hubContext.Clients
                    .Group(VaiaDispatchHub.DispatchOpsGroup)
                    .DocumentUploaded(e);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to broadcast document upload for document {DocumentId}.",
                    documentId);
            }
        });
    }

    private void QueueDocumentVerifiedBroadcast(Guid documentId, bool isVerified)
    {
        if (_serviceScopeFactory is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DispatchDocumentWorkflowService>>();
            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                var hubContext = scope.ServiceProvider
                    .GetRequiredService<IHubContext<VaiaDispatchHub, IVaiaDispatchClient>>();
                var doc = await dbContext.DispatchTripDocuments
                    .Include(item => item.Trip)
                    .Include(item => item.VerifiedBy)
                    .Include(item => item.RejectedBy)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == documentId);
                if (doc?.Trip is null)
                {
                    return;
                }

                var reviewedAt = isVerified
                    ? doc.VerifiedAt.GetValueOrDefault(DateTime.UtcNow)
                    : doc.RejectedAt.GetValueOrDefault(DateTime.UtcNow);
                var e = new DocumentVerifiedEvent(
                    doc.TripId,
                    doc.Trip.ContainerNumber ?? string.Empty,
                    DispatchEventFormatting.DocumentType(doc.Type),
                    isVerified,
                    (isVerified ? doc.VerifiedBy?.Username : doc.RejectedBy?.Username) ?? "Unknown user",
                    reviewedAt);

                var sends = new List<Task>
                {
                    hubContext.Clients.Group(VaiaDispatchHub.DispatchOpsGroup).DocumentVerified(e)
                };

                if (doc.Trip.DriverUserId.HasValue)
                {
                    sends.Add(hubContext.Clients
                        .Group(VaiaDispatchHub.DriverGroup(doc.Trip.DriverUserId.Value))
                        .DocumentVerified(e));
                }

                await Task.WhenAll(sends);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to broadcast document review for document {DocumentId}.",
                    documentId);
            }
        });
    }

    private void QueuePushToUser(
        Guid userId,
        string title,
        string body,
        Dictionary<string, string>? data = null)
    {
        if (_serviceScopeFactory is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DispatchDocumentWorkflowService>>();
            try
            {
                var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                await pushService.SendToUserAsync(userId, title, body, data);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to queue document push notification for user {UserId}.", userId);
            }
        });
    }
}
