using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Enums;
using NVGInventory.Security;
using NVGInventory.Controllers;
using NVGInventory.Modules.Dispatching;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.Dispatching.Services;

namespace NVGInventory.Modules.Dispatching.Controllers;

[ApiController]
[Route("api/dispatch/trips")]
[Authorize]
public sealed class DispatchTripsController : ControllerBase
{
    private readonly DispatchTripService _tripService;
    private readonly DispatchTripQueryService _queryService;
    private readonly DispatchingOptions _options;

    public DispatchTripsController(
        DispatchTripService tripService,
        DispatchTripQueryService queryService,
        IOptions<DispatchingOptions> options)
    {
        _tripService = tripService;
        _queryService = queryService;
        _options = options.Value ?? new DispatchingOptions();
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DispatchTripListItemResponse>>> GetTrips(
        [FromQuery] string? status,
        [FromQuery] Guid? driverId,
        [FromQuery] Guid? customerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        TripStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!QueryParsing.TryParseEnum(status, out TripStatus parsed))
            {
                return BadRequest("Invalid trip status.");
            }

            parsedStatus = parsed;
        }

        var resolvedPage = page.GetValueOrDefault(1);
        if (resolvedPage < 1)
        {
            return BadRequest("Page must be at least 1.");
        }

        var resolvedPageSize = pageSize.GetValueOrDefault(20);
        if (resolvedPageSize < 1 || resolvedPageSize > 100)
        {
            return BadRequest("Page size must be between 1 and 100.");
        }

        var results = await _queryService.GetTripsAsync(
            parsedStatus,
            driverId,
            customerId,
            from,
            to,
            resolvedPage,
            resolvedPageSize,
            BuildActor(),
            cancellationToken);

        var responseItems = results.Items
            .Select(item => new DispatchTripListItemResponse(
                item.Id,
                item.Status,
                new DispatchCustomerSummaryResponse(item.CustomerId, item.CustomerName),
                item.DriverUserId,
                item.DriverUsername,
                item.TruckAssetId,
                item.TruckAssetCode,
                item.PodPending,
                item.UploadedDocumentCount,
                item.RequiredDocumentCount,
                item.CreatedAt,
                item.UpdatedAt,
                item.PickupScheduledAt,
                item.DropoffScheduledAt))
            .ToList();

        return Ok(new PagedResult<DispatchTripListItemResponse>(
            responseItems,
            results.TotalCount,
            resolvedPage,
            resolvedPageSize));
    }

    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Dispatcher}")]
    public async Task<ActionResult<CreateDispatchTripResponse>> CreateTrip(
        CreateDispatchTripRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateDispatchTripCommand(
            request.CustomerId,
            request.DriverUserId,
            request.TruckAssetId,
            request.Notes,
            request.Stops?.Select(stop => new DispatchTripStopInput(stop.StopType, stop.LocationText, stop.ScheduledAt))
                .ToList());

        var trip = await _tripService.CreateDraftAsync(command, BuildActor(), cancellationToken);
        return Ok(new CreateDispatchTripResponse(trip.Id, trip.Status));
    }

    [HttpPut("{tripId:guid}")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Dispatcher}")]
    public async Task<ActionResult<CreateDispatchTripResponse>> UpdateTrip(
        Guid tripId,
        UpdateDispatchTripRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateDispatchTripCommand(
            request.CustomerId,
            request.DriverUserId,
            request.TruckAssetId,
            request.Notes,
            request.Stops?.Select(stop => new DispatchTripStopInput(stop.StopType, stop.LocationText, stop.ScheduledAt))
                .ToList(),
            request.Remarks);

        var trip = await _tripService.UpdateTripAsync(tripId, command, BuildActor(), cancellationToken);
        return Ok(new CreateDispatchTripResponse(trip.Id, trip.Status));
    }

    [HttpGet("{tripId:guid}")]
    public async Task<ActionResult<DispatchTripDetailResponse>> GetTrip(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var detail = await _queryService.GetTripDetailAsync(tripId, BuildActor(), cancellationToken);

        var response = new DispatchTripDetailResponse(
            detail.Id,
            detail.Status,
            new DispatchCustomerSummaryResponse(detail.CustomerId, detail.CustomerName),
            detail.DriverUserId,
            detail.DriverUsername,
            detail.TruckAssetId,
            detail.TruckAssetCode,
            detail.PodPending,
            detail.HoldPreviousStatus,
            detail.Notes,
            detail.CreatedAt,
            detail.UpdatedAt,
            detail.Stops.Select(stop => new DispatchTripStopResponse(
                stop.Id,
                stop.StopType,
                stop.LocationText,
                stop.ScheduledAt,
                stop.ActualAt)).ToList(),
            detail.Documents.Select(doc => new DispatchTripDocumentResponse(
                doc.Id,
                doc.Type,
                doc.State,
                doc.StorageKey,
                doc.UploadedByUserId,
                doc.UploadedBy?.Username,
                doc.VerifiedByUserId,
                doc.VerifiedBy?.Username,
                doc.RejectedByUserId,
                doc.RejectedBy?.Username,
                doc.Remarks,
                doc.UploadedAt,
                doc.VerifiedAt,
                doc.RejectedAt)).ToList(),
            detail.History.Select(entry => new DispatchTripHistoryResponse(
                entry.Id,
                entry.EventType,
                entry.FromStatus,
                entry.ToStatus,
                entry.ActorUserId,
                entry.Actor?.Username,
                entry.Remarks,
                entry.CreatedAt)).ToList(),
            _options.DocVerificationEnabled);

        return Ok(response);
    }

    [HttpPost("{tripId:guid}/dispatch")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Dispatcher}")]
    public async Task<ActionResult<DispatchTripActionResponse>> DispatchTrip(
        Guid tripId,
        DispatchTripActionRequest request,
        CancellationToken cancellationToken)
    {
        var trip = await _tripService.DispatchAsync(
            new DispatchTripCommand(tripId, request.DriverUserId, request.TruckAssetId, request.Remarks),
            BuildActor(),
            cancellationToken);

        return Ok(new DispatchTripActionResponse(trip.Id, trip.Status));
    }

    [HttpPost("{tripId:guid}/status")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Driver}")]
    public async Task<ActionResult<DispatchTripStatusResponse>> UpdateStatus(
        Guid tripId,
        DispatchTripStatusRequest request,
        CancellationToken cancellationToken)
    {
        var trip = await _tripService.ChangeStatusAsync(
            new ChangeDispatchTripStatusCommand(tripId, request.ToStatus, request.Remarks, request.PodPendingOverride),
            BuildActor(),
            cancellationToken);

        return Ok(new DispatchTripStatusResponse(trip.Id, trip.Status));
    }

    [HttpGet("{tripId:guid}/history")]
    public async Task<ActionResult<IReadOnlyCollection<DispatchTripHistoryResponse>>> GetHistory(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var history = await _queryService.GetTripHistoryAsync(tripId, BuildActor(), cancellationToken);

        var response = history.Select(entry => new DispatchTripHistoryResponse(
            entry.Id,
            entry.EventType,
            entry.FromStatus,
            entry.ToStatus,
            entry.ActorUserId,
            entry.Actor?.Username,
            entry.Remarks,
            entry.CreatedAt)).ToList();

        return Ok(response);
    }

    [HttpGet("{tripId:guid}/documents")]
    public async Task<ActionResult<IReadOnlyCollection<DispatchTripDocumentResponse>>> GetDocuments(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var docs = await _queryService.GetTripDocumentsAsync(tripId, BuildActor(), cancellationToken);

        var response = docs.Select(doc => new DispatchTripDocumentResponse(
            doc.Id,
            doc.Type,
            doc.State,
            doc.StorageKey,
            doc.UploadedByUserId,
            doc.UploadedBy?.Username,
            doc.VerifiedByUserId,
            doc.VerifiedBy?.Username,
            doc.RejectedByUserId,
            doc.RejectedBy?.Username,
            doc.Remarks,
            doc.UploadedAt,
            doc.VerifiedAt,
            doc.RejectedAt)).ToList();

        return Ok(response);
    }

    [HttpPost("{tripId:guid}/documents")]
    [Authorize(Roles = RoleNames.Driver)]
    public async Task<ActionResult<DispatchTripDocumentResponse>> UploadDocument(
        Guid tripId,
        DispatchTripDocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        var doc = await _tripService.UploadDocumentAsync(
            new UploadTripDocumentCommand(tripId, request.Type, request.StorageKey),
            BuildActor(),
            cancellationToken);

        return Ok(new DispatchTripDocumentResponse(
            doc.Id,
            doc.Type,
            doc.State,
            doc.StorageKey,
            doc.UploadedByUserId,
            doc.UploadedBy?.Username,
            doc.VerifiedByUserId,
            doc.VerifiedBy?.Username,
            doc.RejectedByUserId,
            doc.RejectedBy?.Username,
            doc.Remarks,
            doc.UploadedAt,
            doc.VerifiedAt,
            doc.RejectedAt));
    }

    [HttpPost("{tripId:guid}/documents/{docId:guid}/verify")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.HeadOfFinance}")]
    public async Task<ActionResult<DispatchTripDocumentResponse>> VerifyDocument(
        Guid tripId,
        Guid docId,
        CancellationToken cancellationToken)
    {
        var doc = await _tripService.VerifyDocumentAsync(
            new VerifyTripDocumentCommand(tripId, docId),
            BuildActor(),
            cancellationToken);

        return Ok(new DispatchTripDocumentResponse(
            doc.Id,
            doc.Type,
            doc.State,
            doc.StorageKey,
            doc.UploadedByUserId,
            doc.UploadedBy?.Username,
            doc.VerifiedByUserId,
            doc.VerifiedBy?.Username,
            doc.RejectedByUserId,
            doc.RejectedBy?.Username,
            doc.Remarks,
            doc.UploadedAt,
            doc.VerifiedAt,
            doc.RejectedAt));
    }

    [HttpPost("{tripId:guid}/documents/{docId:guid}/reject")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.HeadOfFinance}")]
    public async Task<ActionResult<DispatchTripDocumentResponse>> RejectDocument(
        Guid tripId,
        Guid docId,
        DispatchTripDocumentRejectRequest request,
        CancellationToken cancellationToken)
    {
        var doc = await _tripService.RejectDocumentAsync(
            new RejectTripDocumentCommand(tripId, docId, request.Remarks),
            BuildActor(),
            cancellationToken);

        return Ok(new DispatchTripDocumentResponse(
            doc.Id,
            doc.Type,
            doc.State,
            doc.StorageKey,
            doc.UploadedByUserId,
            doc.UploadedBy?.Username,
            doc.VerifiedByUserId,
            doc.VerifiedBy?.Username,
            doc.RejectedByUserId,
            doc.RejectedBy?.Username,
            doc.Remarks,
            doc.UploadedAt,
            doc.VerifiedAt,
            doc.RejectedAt));
    }

    private DispatchActorContext BuildActor()
    {
        return new DispatchActorContext(
            User.GetUserId(),
            User.IsInRole(RoleNames.Manager),
            User.IsInRole(RoleNames.Dispatcher),
            User.IsInRole(RoleNames.Driver),
            User.IsInRole(RoleNames.HeadOfFinance),
            User.IsInRole(RoleNames.Ceo));
    }
}
