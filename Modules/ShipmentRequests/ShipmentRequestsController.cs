using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Security;
using NVGInventory.Modules.Dispatching.Services;
using NVGInventory.Modules.ShipmentRequests.Enums;
using NVGInventory.Modules.ShipmentRequests.Services;

namespace NVGInventory.Modules.ShipmentRequests;

[ApiController]
[Route("api/portal/requests")]
[Authorize(Roles = RoleNames.Customer)]
public sealed class PortalShipmentRequestsController : ControllerBase
{
    private readonly ShipmentRequestService _service;
    private readonly ShipmentRequestQueryService _queryService;
    private readonly IPortalCustomerAccessService _portalCustomerAccessService;

    public PortalShipmentRequestsController(
        ShipmentRequestService service,
        ShipmentRequestQueryService queryService,
        IPortalCustomerAccessService portalCustomerAccessService)
    {
        _service = service;
        _queryService = queryService;
        _portalCustomerAccessService = portalCustomerAccessService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ShipmentRequestListItemResponse>>> GetRequests(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        var customerId = await GetCustomerIdAsync(cancellationToken);
        var result = await _queryService.GetCustomerRequestsAsync(
            customerId,
            resolvedPage,
            resolvedPageSize,
            cancellationToken);

        var items = result.Items.Select(item => new ShipmentRequestListItemResponse(
            item.Id,
            item.Status,
            item.PickupLocation,
            item.DropoffLocation,
            item.RequestedPickupTime,
            item.DocumentsCount,
            item.CreatedAt,
            item.ApprovedAt,
            item.ConvertedTripId)).ToList();

        return Ok(new PagedResult<ShipmentRequestListItemResponse>(
            items,
            result.TotalCount,
            resolvedPage,
            resolvedPageSize));
    }

    [HttpPost]
    public async Task<ActionResult<ShipmentRequestStatusResponse>> CreateRequest(
        CreateShipmentRequestRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = await GetCustomerIdAsync(cancellationToken);
        var command = new CreateShipmentRequestCommand(
            customerId,
            request.PickupLocation,
            request.DropoffLocation,
            request.RequestedPickupTime,
            request.CargoDescription,
            request.CargoWeight,
            request.SpecialInstructions,
            User.GetUserId());

        var created = await _service.CreateDraftAsync(command, cancellationToken);
        return Ok(new ShipmentRequestStatusResponse(created.Id, created.Status));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ShipmentRequestStatusResponse>> UpdateRequest(
        Guid id,
        UpdateShipmentRequestRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = await GetCustomerIdAsync(cancellationToken);
        var command = new UpdateShipmentRequestCommand(
            customerId,
            request.PickupLocation,
            request.DropoffLocation,
            request.RequestedPickupTime,
            request.CargoDescription,
            request.CargoWeight,
            request.SpecialInstructions,
            User.GetUserId());

        var updated = await _service.UpdateDraftAsync(id, command, cancellationToken);
        return Ok(new ShipmentRequestStatusResponse(updated.Id, updated.Status));
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<ShipmentRequestStatusResponse>> SubmitRequest(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customerId = await GetCustomerIdAsync(cancellationToken);
        var submitted = await _service.SubmitAsync(id, customerId, User.GetUserId(), cancellationToken);
        return Ok(new ShipmentRequestStatusResponse(submitted.Id, submitted.Status));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShipmentRequestDetailResponse>> GetRequestDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customerId = await GetCustomerIdAsync(cancellationToken);
        var detail = await _queryService.GetCustomerRequestDetailAsync(id, customerId, cancellationToken);

        var docs = detail.Documents.Select(doc => new ShipmentRequestDocumentResponse(
            doc.Id,
            doc.DocumentType,
            doc.StorageKey,
            doc.UploadedByUserId,
            doc.UploadedByUser?.Username,
            doc.UploadedAt)).ToList();

        return Ok(new ShipmentRequestDetailResponse(
            detail.Id,
            detail.Status,
            detail.PickupLocation,
            detail.DropoffLocation,
            detail.RequestedPickupTime,
            detail.CargoDescription,
            detail.CargoWeight,
            detail.SpecialInstructions,
            detail.CreatedAt,
            detail.ApprovedAt,
            detail.ConvertedTripId,
            docs));
    }

    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<ShipmentRequestDocumentResponse>> UploadDocument(
        Guid id,
        ShipmentRequestDocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = await GetCustomerIdAsync(cancellationToken);
        var command = new UploadShipmentRequestDocumentCommand(
            id,
            request.DocumentType,
            request.StorageKey,
            User.GetUserId());

        var doc = await _service.UploadDocumentAsync(command, customerId, cancellationToken);

        return Ok(new ShipmentRequestDocumentResponse(
            doc.Id,
            doc.DocumentType,
            doc.StorageKey,
            doc.UploadedByUserId,
            doc.UploadedByUser?.Username,
            doc.UploadedAt));
    }

    private async Task<Guid> GetCustomerIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return await _portalCustomerAccessService.GetRequiredPortalCustomerIdAsync(userId, cancellationToken);
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
}

[ApiController]
[Route("api/dispatch/requests")]
[Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Manager}")]
public sealed class DispatchShipmentRequestsController : ControllerBase
{
    private readonly ShipmentRequestService _service;
    private readonly ShipmentRequestQueryService _queryService;

    public DispatchShipmentRequestsController(
        ShipmentRequestService service,
        ShipmentRequestQueryService queryService)
    {
        _service = service;
        _queryService = queryService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DispatchShipmentRequestQueueItemResponse>>> GetQueue(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        var result = await _queryService.GetDispatchQueueAsync(resolvedPage, resolvedPageSize, cancellationToken);
        var items = result.Items.Select(item => new DispatchShipmentRequestQueueItemResponse(
            item.Id,
            item.CustomerId,
            item.CustomerName,
            item.PickupLocation,
            item.DropoffLocation,
            item.RequestedPickupTime,
            item.DocumentsCount,
            item.CreatedAt)).ToList();

        return Ok(new PagedResult<DispatchShipmentRequestQueueItemResponse>(
            items,
            result.TotalCount,
            resolvedPage,
            resolvedPageSize));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ShipmentRequestStatusResponse>> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var approved = await _service.ApproveAsync(id, User.GetUserId(), cancellationToken);
        return Ok(new ShipmentRequestStatusResponse(approved.Id, approved.Status));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ShipmentRequestStatusResponse>> Reject(
        Guid id,
        ShipmentRequestRejectRequest request,
        CancellationToken cancellationToken)
    {
        var rejected = await _service.RejectAsync(id, User.GetUserId(), request.Remarks, cancellationToken);
        return Ok(new ShipmentRequestStatusResponse(rejected.Id, rejected.Status));
    }

    [HttpPost("{id:guid}/convert")]
    public async Task<ActionResult<ShipmentRequestConversionResponse>> Convert(
        Guid id,
        CancellationToken cancellationToken)
    {
        var actor = BuildDispatchActor();
        var result = await _service.ConvertToTripAsync(id, actor, cancellationToken);
        return Ok(new ShipmentRequestConversionResponse(result.Request.Id, result.TripId, result.Request.Status));
    }

    private DispatchActorContext BuildDispatchActor()
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
}

[ApiController]
[Route("api/portal/shipments")]
[Authorize(Roles = RoleNames.Customer)]
public sealed class PortalShipmentsController : ControllerBase
{
    private readonly ShipmentRequestQueryService _queryService;
    private readonly IPortalCustomerAccessService _portalCustomerAccessService;

    public PortalShipmentsController(
        ShipmentRequestQueryService queryService,
        IPortalCustomerAccessService portalCustomerAccessService)
    {
        _queryService = queryService;
        _portalCustomerAccessService = portalCustomerAccessService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerShipmentListItemResponse>>> GetShipments(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        var customerId = await GetCustomerIdAsync(cancellationToken);
        var result = await _queryService.GetCustomerShipmentsAsync(
            customerId,
            resolvedPage,
            resolvedPageSize,
            cancellationToken);

        var items = result.Items.Select(item => new CustomerShipmentListItemResponse(
            item.TripId,
            item.PickupLocation,
            item.DropoffLocation,
            item.Status,
            item.PickupTime,
            item.DeliveredTime,
            item.PodState)).ToList();

        return Ok(new PagedResult<CustomerShipmentListItemResponse>(
            items,
            result.TotalCount,
            resolvedPage,
            resolvedPageSize));
    }

    [HttpGet("{tripId:guid}")]
    public async Task<ActionResult<CustomerShipmentDetailResponse>> GetShipmentDetail(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var customerId = await GetCustomerIdAsync(cancellationToken);
        var detail = await _queryService.GetCustomerShipmentDetailAsync(tripId, customerId, cancellationToken);
        var stops = detail.Stops.Select(stop => new CustomerShipmentStopResponse(
            stop.StopType,
            stop.LocationText,
            stop.ScheduledAt,
            stop.ActualAt)).ToList();

        return Ok(new CustomerShipmentDetailResponse(
            detail.TripId,
            detail.Status,
            detail.PickupLocation,
            detail.DropoffLocation,
            detail.PickupTime,
            detail.DropoffTime,
            detail.DeliveredTime,
            detail.PodState,
            stops));
    }

    [HttpGet("{tripId:guid}/timeline")]
    public async Task<ActionResult<IReadOnlyCollection<CustomerShipmentTimelineEntryResponse>>> GetShipmentTimeline(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var customerId = await GetCustomerIdAsync(cancellationToken);
        var timeline = await _queryService.GetCustomerShipmentTimelineAsync(tripId, customerId, cancellationToken);
        var response = timeline.Select(entry => new CustomerShipmentTimelineEntryResponse(
            entry.FromStatus,
            entry.ToStatus,
            entry.EventAt)).ToList();
        return Ok(response);
    }

    [HttpGet("{tripId:guid}/documents")]
    public async Task<ActionResult<IReadOnlyCollection<CustomerShipmentDocumentResponse>>> GetShipmentDocuments(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var customerId = await GetCustomerIdAsync(cancellationToken);
        var docs = await _queryService.GetCustomerShipmentDocumentsAsync(tripId, customerId, cancellationToken);
        var response = docs.Select(doc => new CustomerShipmentDocumentResponse(
            doc.Type,
            doc.State,
            doc.StorageKey,
            doc.UploadedAt)).ToList();
        return Ok(response);
    }

    private async Task<Guid> GetCustomerIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return await _portalCustomerAccessService.GetRequiredPortalCustomerIdAsync(userId, cancellationToken);
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
}
