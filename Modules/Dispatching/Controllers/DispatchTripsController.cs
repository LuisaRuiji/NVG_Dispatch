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
        [FromQuery] Guid? truckId,
        [FromQuery] Guid? customerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] DateTime? deliveredFrom,
        [FromQuery] DateTime? deliveredTo,
        [FromQuery] string? podStatus,
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

        DispatchPodStatusFilter? parsedPodStatus = null;
        if (!string.IsNullOrWhiteSpace(podStatus))
        {
            if (!QueryParsing.TryParseEnum(podStatus, out DispatchPodStatusFilter parsed))
            {
                return BadRequest("Invalid pod status filter.");
            }

            parsedPodStatus = parsed;
        }

        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        var results = await _queryService.GetTripsAsync(
            parsedStatus,
            driverId,
            truckId,
            customerId,
            from,
            to,
            deliveredFrom,
            deliveredTo,
            parsedPodStatus,
            resolvedPage,
            resolvedPageSize,
            BuildActor(),
            cancellationToken);

        var responseItems = MapTripListItems(results.Items);

        return Ok(new PagedResult<DispatchTripListItemResponse>(
            responseItems,
            results.TotalCount,
            resolvedPage,
            resolvedPageSize));
    }

    [HttpGet("active")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Ceo}")]
    public async Task<ActionResult<PagedResult<DispatchTripListItemResponse>>> GetActiveTrips(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        var results = await _queryService.GetActiveTripsAsync(
            resolvedPage,
            resolvedPageSize,
            BuildActor(),
            cancellationToken);

        var responseItems = MapTripListItems(results.Items);

        return Ok(new PagedResult<DispatchTripListItemResponse>(
            responseItems,
            results.TotalCount,
            resolvedPage,
            resolvedPageSize));
    }

    [HttpGet("on-hold")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Ceo}")]
    public async Task<ActionResult<PagedResult<DispatchTripListItemResponse>>> GetOnHoldTrips(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        var results = await _queryService.GetOnHoldTripsAsync(
            resolvedPage,
            resolvedPageSize,
            BuildActor(),
            cancellationToken);

        var responseItems = MapTripListItems(results.Items);

        return Ok(new PagedResult<DispatchTripListItemResponse>(
            responseItems,
            results.TotalCount,
            resolvedPage,
            resolvedPageSize));
    }

    [HttpGet("failed-attempts")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Ceo}")]
    public async Task<ActionResult<PagedResult<DispatchTripListItemResponse>>> GetFailedAttemptTrips(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        var results = await _queryService.GetFailedAttemptTripsAsync(
            resolvedPage,
            resolvedPageSize,
            BuildActor(),
            cancellationToken);

        var responseItems = MapTripListItems(results.Items);

        return Ok(new PagedResult<DispatchTripListItemResponse>(
            responseItems,
            results.TotalCount,
            resolvedPage,
            resolvedPageSize));
    }

    [HttpGet("pod-pending")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager},{RoleNames.Ceo}")]
    public async Task<ActionResult<PagedResult<DispatchTripListItemResponse>>> GetPodPendingTrips(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        var results = await _queryService.GetPodPendingTripsAsync(
            resolvedPage,
            resolvedPageSize,
            BuildActor(),
            cancellationToken);

        var responseItems = MapTripListItems(results.Items);

        return Ok(new PagedResult<DispatchTripListItemResponse>(
            responseItems,
            results.TotalCount,
            resolvedPage,
            resolvedPageSize));
    }

    [HttpGet("{tripId:guid}/summary")]
    public async Task<ActionResult<DispatchTripSummaryResponse>> GetTripSummary(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var summary = await _queryService.GetTripSummaryAsync(tripId, BuildActor(), cancellationToken);

        var response = new DispatchTripSummaryResponse(
            summary.Id,
            summary.Status,
            new DispatchCustomerSummaryResponse(summary.CustomerId, summary.CustomerName),
            summary.DriverUserId,
            summary.DriverUsername,
            summary.TruckAssetId,
            summary.TruckAssetCode,
            summary.PodPending,
            summary.UploadedDocumentCount,
            summary.RequiredDocumentCount,
            summary.Documents.Select(doc => new DispatchTripDocumentChecklistResponse(doc.Type, doc.State)).ToList(),
            summary.PodState,
            summary.CreatedByUserId,
            summary.CreatedByUsername,
            summary.CreatedAt,
            summary.UpdatedAt,
            summary.PickupScheduledAt,
            summary.DropoffScheduledAt,
            summary.LatePickup,
            summary.LateDelivery,
            summary.OnHoldMinutes,
            Convert.ToBase64String(summary.RowVersion));

        return Ok(response);
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
        if (!TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return BadRequest("RowVersion is required and must be valid base64.");
        }

        var command = new UpdateDispatchTripCommand(
            request.CustomerId,
            request.DriverUserId,
            request.TruckAssetId,
            request.Notes,
            request.Stops?.Select(stop => new DispatchTripStopInput(stop.StopType, stop.LocationText, stop.ScheduledAt))
                .ToList(),
            request.Remarks,
            rowVersion);

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
                entry.EventAt,
                entry.RecordedAt)).ToList(),
            _options.DocVerificationEnabled,
            Convert.ToBase64String(detail.RowVersion));

        return Ok(response);
    }

    [HttpPost("{tripId:guid}/dispatch")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Dispatcher}")]
    public async Task<ActionResult<DispatchTripActionResponse>> DispatchTrip(
        Guid tripId,
        DispatchTripActionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return BadRequest("RowVersion is required and must be valid base64.");
        }

        var trip = await _tripService.DispatchAsync(
            new DispatchTripCommand(tripId, request.DriverUserId, request.TruckAssetId, request.Remarks, rowVersion),
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
        if (request.EventAt == default)
        {
            return BadRequest("EventAt is required.");
        }

        if (!TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return BadRequest("RowVersion is required and must be valid base64.");
        }

        var trip = await _tripService.ChangeStatusAsync(
            new ChangeDispatchTripStatusCommand(
                tripId,
                request.ToStatus,
                request.Remarks,
                request.PodPendingOverride,
                request.EventAt,
                rowVersion),
            BuildActor(),
            cancellationToken);

        return Ok(new DispatchTripStatusResponse(trip.Id, trip.Status));
    }

    [HttpPost("{tripId:guid}/start")]
    [Authorize(Roles = RoleNames.Driver)]
    public Task<ActionResult<DispatchTripStatusResponse>> StartTrip(
        Guid tripId,
        DispatchTripDriverActionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDriverAction(tripId, TripStatus.EnroutePickup, request, cancellationToken);
    }

    [HttpPost("{tripId:guid}/arrive-pickup")]
    [Authorize(Roles = RoleNames.Driver)]
    public Task<ActionResult<DispatchTripStatusResponse>> ArrivePickup(
        Guid tripId,
        DispatchTripDriverActionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDriverAction(tripId, TripStatus.AtPickup, request, cancellationToken);
    }

    [HttpPost("{tripId:guid}/confirm-loaded")]
    [Authorize(Roles = RoleNames.Driver)]
    public Task<ActionResult<DispatchTripStatusResponse>> ConfirmLoaded(
        Guid tripId,
        DispatchTripDriverActionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDriverAction(tripId, TripStatus.Loaded, request, cancellationToken);
    }

    [HttpPost("{tripId:guid}/depart-pickup")]
    [Authorize(Roles = RoleNames.Driver)]
    public Task<ActionResult<DispatchTripStatusResponse>> DepartPickup(
        Guid tripId,
        DispatchTripDriverActionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDriverAction(tripId, TripStatus.EnrouteDropoff, request, cancellationToken);
    }

    [HttpPost("{tripId:guid}/arrive-dropoff")]
    [Authorize(Roles = RoleNames.Driver)]
    public Task<ActionResult<DispatchTripStatusResponse>> ArriveDropoff(
        Guid tripId,
        DispatchTripDriverActionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDriverAction(tripId, TripStatus.AtDropoff, request, cancellationToken);
    }

    [HttpPost("{tripId:guid}/confirm-delivery")]
    [Authorize(Roles = RoleNames.Driver)]
    public Task<ActionResult<DispatchTripStatusResponse>> ConfirmDelivery(
        Guid tripId,
        DispatchTripDriverActionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDriverAction(tripId, TripStatus.Delivered, request, cancellationToken);
    }

    [HttpPost("{tripId:guid}/request-hold")]
    [Authorize(Roles = RoleNames.Driver)]
    public Task<ActionResult<DispatchTripStatusResponse>> RequestHold(
        Guid tripId,
        DispatchTripDriverActionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDriverAction(tripId, TripStatus.OnHold, request, cancellationToken);
    }

    [HttpPost("{tripId:guid}/report-failure")]
    [Authorize(Roles = RoleNames.Driver)]
    public Task<ActionResult<DispatchTripStatusResponse>> ReportFailure(
        Guid tripId,
        DispatchTripDriverActionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteDriverAction(tripId, TripStatus.FailedAttempt, request, cancellationToken);
    }

    [HttpPost("{tripId:guid}/correct-status")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.Dispatcher}")]
    public async Task<ActionResult<DispatchTripCorrectStatusResponse>> CorrectStatus(
        Guid tripId,
        DispatchTripCorrectStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EventAt == default)
        {
            return BadRequest("EventAt is required.");
        }

        if (!TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return BadRequest("RowVersion is required and must be valid base64.");
        }

        var trip = await _tripService.CorrectStatusAsync(
            tripId,
            request.ToStatus,
            request.EventAt,
            request.Remarks,
            rowVersion,
            BuildActor(),
            cancellationToken);

        return Ok(new DispatchTripCorrectStatusResponse(trip.Id, trip.Status));
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
            entry.EventAt,
            entry.RecordedAt)).ToList();

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

    [HttpGet("{tripId:guid}/documents/versions")]
    public async Task<ActionResult<PagedResult<DispatchTripDocumentVersionResponse>>> GetDocumentVersions(
        Guid tripId,
        [FromQuery] string? type,
        [FromQuery] string? state,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        TripDocumentType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!QueryParsing.TryParseEnum(type, out TripDocumentType parsed))
            {
                return BadRequest("Invalid document type.");
            }

            parsedType = parsed;
        }

        TripDocumentState? parsedState = null;
        if (!string.IsNullOrWhiteSpace(state))
        {
            if (!QueryParsing.TryParseEnum(state, out TripDocumentState parsed))
            {
                return BadRequest("Invalid document state.");
            }

            parsedState = parsed;
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

        var docs = await _queryService.GetTripDocumentVersionsPagedAsync(
            tripId,
            parsedType,
            parsedState,
            from,
            to,
            resolvedPage,
            resolvedPageSize,
            BuildActor(),
            cancellationToken);

        var response = docs.Items.Select(doc => new DispatchTripDocumentVersionResponse(
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
            doc.RejectedAt,
            doc.IsActive,
            doc.SupersedesDocumentId)).ToList();

        return Ok(new PagedResult<DispatchTripDocumentVersionResponse>(
            response,
            docs.TotalCount,
            resolvedPage,
            resolvedPageSize));
    }

    [HttpGet("{tripId:guid}/documents/{docId:guid}/link")]
    public async Task<ActionResult<DispatchTripDocumentLinkResponse>> GetDocumentLink(
        Guid tripId,
        Guid docId,
        CancellationToken cancellationToken)
    {
        var storageKey = await _queryService.GetTripDocumentLinkAsync(
            tripId,
            docId,
            BuildActor(),
            cancellationToken);

        return Ok(new DispatchTripDocumentLinkResponse(storageKey));
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

    private static bool TryResolvePaging(
        int? page,
        int? pageSize,
        out int resolvedPage,
        out int resolvedPageSize,
        out string? error)
    {
        resolvedPage = page.GetValueOrDefault(1);
        if (resolvedPage < 1)
        {
            error = "Page must be at least 1.";
            resolvedPageSize = 0;
            return false;
        }

        resolvedPageSize = pageSize.GetValueOrDefault(20);
        if (resolvedPageSize < 1 || resolvedPageSize > 100)
        {
            error = "Page size must be between 1 and 100.";
            return false;
        }

        error = null;
        return true;
    }

    private static List<DispatchTripListItemResponse> MapTripListItems(
        IReadOnlyCollection<DispatchTripListItem> items)
    {
        return items
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
                item.Documents.Select(doc => new DispatchTripDocumentChecklistResponse(
                    doc.Type,
                    doc.State)).ToList(),
                item.CreatedAt,
                item.UpdatedAt,
                item.PickupLocation,
                item.DropoffLocation,
                item.PickupScheduledAt,
                item.DropoffScheduledAt,
                item.LatePickup,
                item.LateDelivery,
                item.OnHoldMinutes,
                Convert.ToBase64String(item.RowVersion)))
            .ToList();
    }

    private static bool TryParseRowVersion(string? raw, out byte[] rowVersion)
    {
        rowVersion = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(raw);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task<ActionResult<DispatchTripStatusResponse>> ExecuteDriverAction(
        Guid tripId,
        TripStatus toStatus,
        DispatchTripDriverActionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EventAt == default)
        {
            return BadRequest("EventAt is required.");
        }

        if (!TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return BadRequest("RowVersion is required and must be valid base64.");
        }

        if ((toStatus == TripStatus.OnHold || toStatus == TripStatus.FailedAttempt)
            && string.IsNullOrWhiteSpace(request.Remarks))
        {
            return BadRequest("Remarks are required.");
        }

        var trip = await _tripService.ChangeStatusAsync(
            new ChangeDispatchTripStatusCommand(
                tripId,
                toStatus,
                request.Remarks,
                null,
                request.EventAt,
                rowVersion),
            BuildActor(),
            cancellationToken);

        return Ok(new DispatchTripStatusResponse(trip.Id, trip.Status));
    }
}
