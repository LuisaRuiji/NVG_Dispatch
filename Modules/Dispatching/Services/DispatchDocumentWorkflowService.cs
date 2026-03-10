using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Contracts;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed class DispatchDocumentWorkflowService : IDispatchDocumentWorkflowService, IDispatchDocumentReadService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService? _auditService;

    public DispatchDocumentWorkflowService(
        InventoryDbContext dbContext,
        IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _auditService = auditService;
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

        if (command.Type == TripDocumentType.Pod && trip.Status != TripStatus.Delivered)
        {
            throw new ConflictDomainException("POD can only be uploaded after delivery.");
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
        return doc;
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
}
