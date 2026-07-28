using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Security;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Modules.ShipmentRequests.Services;

namespace NVGInventory.Modules.Dispatching.Controllers;

[ApiController]
[Route("api/dispatch/planning")]
[Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager}")]
public sealed class DispatchPlanningController : ControllerBase
{
    private readonly DispatchPlanningService _planningService;
    private readonly PlanningDecisionSupportService _decisionSupportService;
    private readonly ShipmentRequestService _shipmentRequestService;

    public DispatchPlanningController(
        DispatchPlanningService planningService,
        PlanningDecisionSupportService decisionSupportService,
        ShipmentRequestService shipmentRequestService)
    {
        _planningService = planningService;
        _decisionSupportService = decisionSupportService;
        _shipmentRequestService = shipmentRequestService;
    }

    [HttpGet("board")]
    public async Task<ActionResult<PlanningBoardSnapshot>> GetBoard(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? day = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) return BadRequest("Page must be at least 1.");
        if (pageSize is < 1 or > 100) return BadRequest("Page size must be between 1 and 100.");

        return Ok(await _planningService.GetBoardAsync(page, pageSize, search, day, cancellationToken));
    }

    [HttpGet("resources")]
    public async Task<ActionResult<PlanningResourceSnapshot>> GetResources(
        [FromQuery] DateTime? pickupAt,
        [FromQuery] DateTime? dropoffAt,
        [FromQuery] Guid? excludeTripId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _planningService.GetResourcesAsync(
                pickupAt,
                dropoffAt,
                excludeTripId,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("bookings/{requestId:guid}/start")]
    public async Task<ActionResult<PlanningStartResponse>> StartPlanning(
        Guid requestId,
        PlanningStartRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _shipmentRequestService.StartPlanningAsync(
            requestId,
            BuildActor(),
            request?.ScheduledPickupTime,
            cancellationToken);
        return Ok(new PlanningStartResponse(result.Request.Id, result.TripId, result.Request.Status, result.Created));
    }

    [HttpGet("trips/{tripId:guid}/decision-support")]
    public async Task<ActionResult<PlanningDecisionSupportResponse>> GetDecisionSupport(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return Ok(await _decisionSupportService.GetDecisionSupportAsync(tripId, cancellationToken));
    }

    [HttpPost("trips/{tripId:guid}/validate")]
    public async Task<ActionResult<PlanningDecisionSupportResponse>> ValidateForDispatch(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return Ok(await _decisionSupportService.ValidateForDispatchAsync(tripId, cancellationToken));
    }

    [HttpPost("trips/{tripId:guid}/ready")]
    public async Task<ActionResult<MarkTripReadyResult>> MarkReady(
        Guid tripId,
        PlanningMarkReadyRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _decisionSupportService.MarkReadyAsync(
            tripId,
            new MarkTripReadyCommand(request.RowVersion, request.RecommendationToken, request.SelectedRank, request.OverrideReason),
            BuildActor(),
            cancellationToken));
    }

    private DispatchActorContext BuildActor() => new(
        User.GetUserId(),
        User.IsInRole(RoleNames.Manager),
        User.IsInRole(RoleNames.Dispatcher),
        User.IsInRole(RoleNames.Driver),
        User.IsInRole(RoleNames.HeadOfFinance),
        User.IsInRole(RoleNames.Ceo),
        User.IsInRole(RoleNames.Admin));
}
