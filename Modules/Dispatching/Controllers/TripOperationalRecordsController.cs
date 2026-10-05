using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Modules.Dispatching.Entities;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Security;

namespace NVGInventory.Modules.Dispatching.Controllers;

[ApiController]
[Route("api/dispatch/trips/{tripId:guid}")]
[Authorize(Roles = $"{RoleNames.Driver},{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Admin}")]
public sealed class TripOperationalRecordsController : ControllerBase
{
    private readonly TripOperationalRecordService _service;
    private readonly InventoryDbContext _dbContext;

    public TripOperationalRecordsController(TripOperationalRecordService service, InventoryDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyCollection<TripOperationalEventResponse>>> GetEvents(Guid tripId, CancellationToken cancellationToken)
    {
        var items = await _dbContext.DispatchTripOperationalEvents.AsNoTracking()
            .Where(item => item.TripId == tripId).OrderBy(item => item.EventAt).ToListAsync(cancellationToken);
        return Ok(items.Select(Map));
    }

    [HttpPost("events")]
    public async Task<ActionResult<TripOperationalEventResponse>> RecordEvent(Guid tripId, RecordTripOperationalEventRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await _service.RecordEventAsync(tripId, new RecordTripOperationalEventCommand(request.EventType, request.EventAt, request.Location, request.Reason, request.ReferenceNumber, request.RelatedDocumentId), BuildActor(), cancellationToken)));

    [HttpGet("container-inspection")]
    public async Task<ActionResult<ContainerInspectionResponse>> GetInspection(Guid tripId, CancellationToken cancellationToken)
    {
        var item = await _dbContext.ContainerQualityInspections.AsNoTracking().FirstOrDefaultAsync(inspection => inspection.TripId == tripId, cancellationToken);
        return item is null ? NotFound() : Ok(Map(item));
    }

    [HttpPost("container-inspection")]
    public async Task<ActionResult<ContainerInspectionResponse>> RecordInspection(Guid tripId, RecordContainerInspectionRequest request, CancellationToken cancellationToken) =>
        Ok(Map(await _service.RecordInspectionAsync(tripId, new RecordContainerInspectionCommand(
            request.HasDents, request.HasHoles, request.HasRust, request.HasOdor, request.HasResidue, request.HasStains,
            request.HasInsects, request.IsClean, request.FoodGradeRequired, request.FoodGradePassed, request.Outcome, request.Reason), BuildActor(), cancellationToken)));

    private DispatchActorContext BuildActor() => new(User.GetUserId(), User.IsInRole(RoleNames.Manager), User.IsInRole(RoleNames.Dispatcher), User.IsInRole(RoleNames.Driver), false, false, User.IsInRole(RoleNames.Admin));
    private static TripOperationalEventResponse Map(TripOperationalEvent item) => new(item.Id, item.EventType, item.EventAt, item.RecordedAt, item.ActorUserId, item.ActorRole, item.Location, item.Reason, item.ReferenceNumber, item.RelatedDocumentId);
    private static ContainerInspectionResponse Map(ContainerQualityInspection item) => new(item.TripId, item.HasDents, item.HasHoles, item.HasRust, item.HasOdor, item.HasResidue, item.HasStains, item.HasInsects, item.IsClean, item.FoodGradeRequired, item.FoodGradePassed, item.Outcome, item.Reason, item.InspectedByUserId, item.InspectedAt);
}

public sealed record RecordTripOperationalEventRequest(TripOperationalEventType EventType, DateTime EventAt, string? Location, string? Reason, string? ReferenceNumber, Guid? RelatedDocumentId);
public sealed record TripOperationalEventResponse(Guid Id, TripOperationalEventType EventType, DateTime EventAt, DateTime RecordedAt, Guid ActorUserId, string ActorRole, string? Location, string? Reason, string? ReferenceNumber, Guid? RelatedDocumentId);
public sealed record RecordContainerInspectionRequest(bool HasDents, bool HasHoles, bool HasRust, bool HasOdor, bool HasResidue, bool HasStains, bool HasInsects, bool IsClean, bool FoodGradeRequired, bool FoodGradePassed, ContainerInspectionOutcome Outcome, string? Reason);
public sealed record ContainerInspectionResponse(Guid TripId, bool HasDents, bool HasHoles, bool HasRust, bool HasOdor, bool HasResidue, bool HasStains, bool HasInsects, bool IsClean, bool FoodGradeRequired, bool FoodGradePassed, ContainerInspectionOutcome Outcome, string? Reason, Guid InspectedByUserId, DateTime InspectedAt);
