using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Exceptions;
using NVGInventory.Domain.Services;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record RecordTripOperationalEventCommand(
    TripOperationalEventType EventType,
    DateTime EventAt,
    string? Location,
    string? Reason,
    string? ReferenceNumber,
    Guid? RelatedDocumentId);

public sealed record RecordContainerInspectionCommand(
    bool HasDents,
    bool HasHoles,
    bool HasRust,
    bool HasOdor,
    bool HasResidue,
    bool HasStains,
    bool HasInsects,
    bool IsClean,
    bool FoodGradeRequired,
    bool FoodGradePassed,
    ContainerInspectionOutcome Outcome,
    string? Reason);

public sealed class TripOperationalRecordService
{
    private static readonly HashSet<TripOperationalEventType> DriverEvents =
    [
        TripOperationalEventType.Departed,
        TripOperationalEventType.ArrivedAtPickup,
        TripOperationalEventType.ArrivedAtDepot,
        TripOperationalEventType.ArrivedAtTerminal,
        TripOperationalEventType.GateIn,
        TripOperationalEventType.GateOut,
        TripOperationalEventType.ArrivedAtConsignee,
        TripOperationalEventType.DeliveryCompleted,
        TripOperationalEventType.EmptyContainerReturned
    ];
    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService _auditService;

    public TripOperationalRecordService(InventoryDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task<TripOperationalEvent> RecordEventAsync(
        Guid tripId,
        RecordTripOperationalEventCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips.AsNoTracking().FirstOrDefaultAsync(item => item.Id == tripId, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");
        EnsureTripAccess(trip, actor);
        if (actor.IsDriver && !DriverEvents.Contains(command.EventType))
            throw new ForbiddenDomainException("Drivers can record execution events but cannot record approval or closure milestones.");
        if (command.EventAt == default || command.EventAt > DateTime.UtcNow.AddMinutes(5) || command.EventAt < trip.CreatedAt)
            throw new BusinessRuleViolationException("Event time must fall between trip creation and five minutes from now.");
        if (command.RelatedDocumentId.HasValue && !await _dbContext.DispatchTripDocuments.AsNoTracking()
                .AnyAsync(document => document.Id == command.RelatedDocumentId && document.TripId == tripId, cancellationToken))
            throw new BusinessRuleViolationException("The related attachment does not belong to this trip.");

        var item = new TripOperationalEvent
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            EventType = command.EventType,
            ActorUserId = actor.UserId,
            ActorRole = ResolveRole(actor),
            EventAt = command.EventAt,
            RecordedAt = DateTime.UtcNow,
            Location = Normalize(command.Location),
            Reason = Normalize(command.Reason),
            ReferenceNumber = Normalize(command.ReferenceNumber),
            RelatedDocumentId = command.RelatedDocumentId
        };
        _dbContext.DispatchTripOperationalEvents.Add(item);
        _auditService.AddEntry(actor.UserId, AuditActions.TripOperationalEventRecorded, EntityTypes.TripOperationalEvent, item.Id,
            null, new { item.TripId, item.EventType, item.EventAt, item.Location }, actorRole: item.ActorRole,
            tripId: tripId, reason: item.Reason, relatedAttachmentId: item.RelatedDocumentId, referenceNumber: item.ReferenceNumber);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<ContainerQualityInspection> RecordInspectionAsync(
        Guid tripId,
        RecordContainerInspectionCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var trip = await _dbContext.DispatchTrips.FirstOrDefaultAsync(item => item.Id == tripId, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");
        EnsureTripAccess(trip, actor);
        if (DispatchDocumentRules.NormalizeTripType(trip.TripType) != DispatchDocumentRules.ExportEmptyPickup)
            throw new ConflictDomainException("Container quality inspection is required only for export empty pickup trips.");
        if (command.Outcome is ContainerInspectionOutcome.Fail or ContainerInspectionOutcome.ReturnToDepot && string.IsNullOrWhiteSpace(command.Reason))
            throw new BusinessRuleViolationException("A rejection reason is required for failed or return-to-depot inspections.");
        if (command.FoodGradeRequired && command.Outcome == ContainerInspectionOutcome.Pass && !command.FoodGradePassed)
            throw new BusinessRuleViolationException("A food-grade container cannot pass unless the food-grade check passes.");
        if (command.Outcome is ContainerInspectionOutcome.Fail or ContainerInspectionOutcome.ReturnToDepot &&
            !await _dbContext.DispatchTripDocuments.AsNoTracking().AnyAsync(document =>
                document.TripId == tripId && document.Type == TripDocumentType.ContainerInspectionPhoto && document.IsActive, cancellationToken))
            throw new BusinessRuleViolationException("Photo evidence is required when rejecting a container.");

        var inspection = await _dbContext.ContainerQualityInspections.FirstOrDefaultAsync(item => item.TripId == tripId, cancellationToken);
        if (inspection is not null) throw new ConflictDomainException("Container inspections are immutable. Record a manager review instead of replacing the inspection.");
        inspection = new ContainerQualityInspection
        {
            TripId = tripId,
            HasDents = command.HasDents,
            HasHoles = command.HasHoles,
            HasRust = command.HasRust,
            HasOdor = command.HasOdor,
            HasResidue = command.HasResidue,
            HasStains = command.HasStains,
            HasInsects = command.HasInsects,
            IsClean = command.IsClean,
            FoodGradeRequired = command.FoodGradeRequired,
            FoodGradePassed = command.FoodGradePassed,
            Outcome = command.Outcome,
            Reason = Normalize(command.Reason),
            InspectedByUserId = actor.UserId,
            InspectedAt = DateTime.UtcNow
        };
        _dbContext.ContainerQualityInspections.Add(inspection);
        _auditService.AddEntry(actor.UserId, AuditActions.ContainerInspectionRecorded, EntityTypes.ContainerInspection, tripId,
            null, new { inspection.Outcome, inspection.FoodGradeRequired, inspection.FoodGradePassed }, actorRole: ResolveRole(actor),
            tripId: tripId, reason: inspection.Reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return inspection;
    }

    private static void EnsureTripAccess(Trip trip, DispatchActorContext actor)
    {
        if (actor.IsManager || actor.IsDispatcher || actor.IsAdmin) return;
        if (actor.IsDriver && trip.DriverUserId == actor.UserId) return;
        throw new ForbiddenDomainException("Trip access denied.");
    }

    private static string ResolveRole(DispatchActorContext actor) => actor.IsDriver ? RoleNames.Driver : actor.IsDispatcher ? RoleNames.Dispatcher : actor.IsManager ? RoleNames.Manager : RoleNames.Admin;
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
