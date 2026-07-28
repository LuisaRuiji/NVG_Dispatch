using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
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
    private readonly IAtwDocumentIntelligenceService _documentIntelligenceService;
    private readonly IAtwScanSessionStore _atwScanSessionStore;

    public PortalShipmentRequestsController(
        ShipmentRequestService service,
        ShipmentRequestQueryService queryService,
        IPortalCustomerAccessService portalCustomerAccessService,
        IAtwDocumentIntelligenceService documentIntelligenceService,
        IAtwScanSessionStore atwScanSessionStore)
    {
        _service = service;
        _queryService = queryService;
        _portalCustomerAccessService = portalCustomerAccessService;
        _documentIntelligenceService = documentIntelligenceService;
        _atwScanSessionStore = atwScanSessionStore;
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
            item.ContainerSize,
            item.TripType,
            item.ContainerNumber,
            item.ShippingLine,
            item.BookingNumber,
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
            request.PickupLatitude,
            request.PickupLongitude,
            request.DropoffLocation,
            request.DropoffLatitude,
            request.DropoffLongitude,
            request.RequestedPickupTime,
            request.ContainerSize,
            request.TripType,
            request.ContainerNumber,
            request.ShippingLine,
            request.BookingNumber,
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
            request.PickupLatitude,
            request.PickupLongitude,
            request.DropoffLocation,
            request.DropoffLatitude,
            request.DropoffLongitude,
            request.RequestedPickupTime,
            request.ContainerSize,
            request.TripType,
            request.ContainerNumber,
            request.ShippingLine,
            request.BookingNumber,
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
            detail.ContainerSize,
            detail.TripType,
            detail.ContainerNumber,
            detail.ShippingLine,
            detail.BookingNumber,
            detail.CargoDescription,
            detail.CargoWeight,
            detail.SpecialInstructions,
            detail.ReviewRemarks,
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

    [HttpPost("atw/scan")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<AtwScanResponse>> ScanAtw(
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!IsValidAtwFile(file, out var error))
        {
            return BadRequest(error);
        }

        var userId = User.GetUserId();
        string fileHash;
        await using (var hashContent = file.OpenReadStream())
        {
            fileHash = Convert.ToHexString(await SHA256.HashDataAsync(hashContent, cancellationToken));
        }

        AtwExtractionResult analysis;
        await using (var content = file.OpenReadStream())
        {
            analysis = await _documentIntelligenceService.AnalyzeAsync(content, file.ContentType, cancellationToken);
        }

        var scanId = _atwScanSessionStore.Create(userId, fileHash, analysis);
        return Ok(new AtwScanResponse(
            scanId,
            analysis.ContainerNumber,
            analysis.BookingNumber,
            analysis.ShippingLine,
            analysis.Confidence,
            analysis.RiskFlags,
            analysis.Error,
            analysis.PickupLocation,
            analysis.DropoffLocation,
            analysis.ContainerSize,
            analysis.CargoDescription,
            analysis.CargoWeight,
            analysis.SpecialInstructions,
            analysis.RequestedPickupTime,
            analysis.IssueDate,
            analysis.ValidUntil));
    }

    [HttpPost("{id:guid}/documents/atw-upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<AtwDocumentUploadResponse>> UploadAndAnalyzeAtw(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] Guid? scanId,
        CancellationToken cancellationToken)
    {
        if (!IsValidAtwFile(file, out var error))
        {
            return BadRequest(error);
        }

        var customerId = await GetCustomerIdAsync(cancellationToken);
        AtwExtractionResult? scannedAnalysis = null;
        if (scanId.HasValue)
        {
            string fileHash;
            await using (var hashContent = file.OpenReadStream())
            {
                fileHash = Convert.ToHexString(await SHA256.HashDataAsync(hashContent, cancellationToken));
            }

            if (!_atwScanSessionStore.TryTake(scanId.Value, User.GetUserId(), fileHash, out var cachedAnalysis))
            {
                return BadRequest("The ATW scan expired or the file changed. Scan this file again before creating the request.");
            }

            scannedAnalysis = cachedAnalysis;
        }

        await using var content = file.OpenReadStream();
        var result = await _service.UploadAndAnalyzeAtwAsync(
            id,
            customerId,
            User.GetUserId(),
            content,
            file.FileName,
            file.ContentType,
            file.Length,
            scannedAnalysis,
            cancellationToken);

        return Ok(new AtwDocumentUploadResponse(
            result.Document.Id,
            result.Document.AnalysisStatus.ToString().ToUpperInvariant(),
            result.Analysis.ContainerNumber,
            result.Analysis.BookingNumber,
            result.Analysis.ShippingLine,
            result.Analysis.Confidence,
            result.Analysis.RiskFlags,
            result.AppliedFields,
            result.Analysis.Error));
    }

    private async Task<Guid> GetCustomerIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return await _portalCustomerAccessService.GetRequiredPortalCustomerIdAsync(userId, cancellationToken);
    }

    private static bool IsValidAtwFile(IFormFile? file, out string? error)
    {
        if (file is null || file.Length == 0)
        {
            error = "ATW file is required.";
            return false;
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            error = "ATW files must be 10 MB or smaller.";
            return false;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".pdf" and not ".jpg" and not ".jpeg" and not ".png")
        {
            error = "Upload an ATW as a PDF, JPG, or PNG file.";
            return false;
        }

        error = null;
        return true;
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
    private readonly IShipmentRequestDocumentStorage _documentStorage;

    public DispatchShipmentRequestsController(
        ShipmentRequestService service,
        ShipmentRequestQueryService queryService,
        IShipmentRequestDocumentStorage documentStorage)
    {
        _service = service;
        _queryService = queryService;
        _documentStorage = documentStorage;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DispatchShipmentRequestQueueItemResponse>>> GetQueue(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] string? priority,
        [FromQuery] string? atwStatus,
        [FromQuery] string? sort,
        CancellationToken cancellationToken)
    {
        if (!TryResolvePaging(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var pagingError))
        {
            return BadRequest(pagingError);
        }

        if (!TryResolveStatus(status, out var resolvedStatus, out var statusError))
        {
            return BadRequest(statusError);
        }

        if (!TryResolvePriority(priority, out var resolvedPriority, out var priorityError))
        {
            return BadRequest(priorityError);
        }

        if (!TryResolveAtwStatus(atwStatus, out var resolvedAtwStatus, out var atwError))
        {
            return BadRequest(atwError);
        }

        if (!TryResolveSort(sort, out var resolvedSort, out var sortError))
        {
            return BadRequest(sortError);
        }

        var result = await _queryService.GetDispatchQueueAsync(
            resolvedPage,
            resolvedPageSize,
            resolvedStatus,
            search,
            resolvedPriority,
            resolvedAtwStatus,
            resolvedSort,
            cancellationToken);
        var items = result.Items.Select(item => new DispatchShipmentRequestQueueItemResponse(
            item.Id,
            item.CustomerId,
            item.CustomerName,
            item.PickupLocation,
            item.PickupLatitude,
            item.PickupLongitude,
            item.DropoffLocation,
            item.DropoffLatitude,
            item.DropoffLongitude,
            item.RequestedPickupTime,
            item.ContainerSize,
            item.TripType,
            item.ContainerNumber,
            item.ShippingLine,
            item.BookingNumber,
            item.DocumentsCount,
            item.AtwDocumentId,
            item.AtwOriginalFileName,
            item.AtwAnalysisStatus?.ToString().ToUpperInvariant(),
            item.AtwUploadedAt,
            item.CreatedAt,
            ToStatusValue(item.Status),
            item.Priority.ToString().ToUpperInvariant(),
            item.ReviewRemarks)).ToList();

        return Ok(new PagedResult<DispatchShipmentRequestQueueItemResponse>(
            items,
            result.TotalCount,
            resolvedPage,
            resolvedPageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DispatchShipmentRequestDetailResponse>> GetDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await _queryService.GetDispatchRequestDetailAsync(id, cancellationToken);
        var documents = detail.Documents.Select(document => new DispatchShipmentRequestDocumentResponse(
            document.Id,
            document.DocumentType,
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.AnalysisStatus.ToString().ToUpperInvariant(),
            document.AnalysisError,
            document.ExtractionConfidence,
            document.UploadedByUserId,
            document.UploadedByUser?.Username,
            document.UploadedAt)).ToList();
        var activity = detail.Activity.Select(item => new DispatchShipmentRequestActivityResponse(
            item.Action,
            item.ActorUsername,
            item.CreatedAt)).ToList();
        var assignment = detail.Assignment is null
            ? null
            : new DispatchShipmentRequestAssignmentResponse(
                detail.Assignment.TripId,
                detail.Assignment.DriverUserId,
                detail.Assignment.DriverUsername,
                detail.Assignment.TruckAssetId,
                detail.Assignment.TruckAssetCode,
                detail.Assignment.TrailerAssetId,
                detail.Assignment.TrailerAssetCode);

        return Ok(new DispatchShipmentRequestDetailResponse(
            detail.Id,
            detail.CustomerId,
            detail.CustomerName,
            ToStatusValue(detail.Status),
            detail.PickupLocation,
            detail.PickupLatitude,
            detail.PickupLongitude,
            detail.DropoffLocation,
            detail.DropoffLatitude,
            detail.DropoffLongitude,
            detail.RequestedPickupTime,
            detail.ContainerSize,
            detail.TripType,
            detail.ContainerNumber,
            detail.ShippingLine,
            detail.BookingNumber,
            detail.CargoDescription,
            detail.CargoWeight,
            detail.SpecialInstructions,
            detail.ReviewRemarks,
            detail.CreatedAt,
            detail.ApprovedAt,
            detail.ConvertedTripId,
            documents,
            activity,
            assignment));
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/content")]
    public async Task<IActionResult> GetDocumentContent(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var detail = await _queryService.GetDispatchRequestDetailAsync(id, cancellationToken);
        var document = detail.Documents.FirstOrDefault(item => item.Id == documentId);
        if (document is null)
        {
            return NotFound("Document not found.");
        }

        var readable = await _documentStorage.OpenReadAsync(
            document.StorageKey,
            document.OriginalFileName,
            document.ContentType,
            cancellationToken);
        return File(readable.Content, readable.ContentType, enableRangeProcessing: true);
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

    [HttpPost("{id:guid}/request-changes")]
    public async Task<ActionResult<ShipmentRequestStatusResponse>> RequestChanges(
        Guid id,
        ShipmentRequestRequestChangesRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.RequestChangesAsync(id, User.GetUserId(), request.Remarks, cancellationToken);
        return Ok(new ShipmentRequestStatusResponse(updated.Id, updated.Status));
    }

    [HttpPost("{id:guid}/convert")]
    public async Task<ActionResult<ShipmentRequestConversionResponse>> Convert(
        Guid id,
        ShipmentRequestConvertRequest? request,
        CancellationToken cancellationToken)
    {
        var actor = BuildDispatchActor();
        var result = await _service.ConvertToTripAsync(id, actor, request?.ScheduledPickupTime, cancellationToken);
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

    private static bool TryResolveStatus(
        string? value,
        out ShipmentRequestStatus? status,
        out string? error)
    {
        status = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        status = value.Trim().ToUpperInvariant() switch
        {
            "SUBMITTED" => ShipmentRequestStatus.Submitted,
            "APPROVED" => ShipmentRequestStatus.Approved,
            "NEEDS_REVISION" => ShipmentRequestStatus.NeedsRevision,
            "REJECTED" => ShipmentRequestStatus.Rejected,
            _ => null
        };

        if (status.HasValue)
        {
            return true;
        }

        error = "Status must be SUBMITTED, APPROVED, NEEDS_REVISION, or REJECTED.";
        return false;
    }

    private static bool TryResolvePriority(
        string? value,
        out ShipmentRequestQueuePriority? priority,
        out string? error)
    {
        priority = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        priority = value.Trim().ToUpperInvariant() switch
        {
            "CRITICAL" => ShipmentRequestQueuePriority.Critical,
            "HIGH" => ShipmentRequestQueuePriority.High,
            "NORMAL" => ShipmentRequestQueuePriority.Normal,
            _ => null
        };
        if (priority.HasValue) return true;
        error = "Priority must be CRITICAL, HIGH, or NORMAL.";
        return false;
    }

    private static bool TryResolveAtwStatus(
        string? value,
        out ShipmentRequestQueueAtwStatus? status,
        out string? error)
    {
        status = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        status = value.Trim().ToUpperInvariant() switch
        {
            "UPLOADED" => ShipmentRequestQueueAtwStatus.Uploaded,
            "MISSING" => ShipmentRequestQueueAtwStatus.Missing,
            _ => null
        };
        if (status.HasValue) return true;
        error = "ATW status must be UPLOADED or MISSING.";
        return false;
    }

    private static bool TryResolveSort(
        string? value,
        out ShipmentRequestQueueSort sort,
        out string? error)
    {
        error = null;
        sort = ShipmentRequestQueueSort.Priority;
        if (string.IsNullOrWhiteSpace(value)) return true;
        var parsed = value.Trim().ToUpperInvariant() switch
        {
            "PRIORITY" => ShipmentRequestQueueSort.Priority,
            "REQUESTED_TIME" => ShipmentRequestQueueSort.RequestedTime,
            "REQUESTED_TIME_DESC" => ShipmentRequestQueueSort.RequestedTimeDescending,
            "NEWEST" => ShipmentRequestQueueSort.Newest,
            "OLDEST" => ShipmentRequestQueueSort.Oldest,
            "CUSTOMER" => ShipmentRequestQueueSort.Customer,
            _ => (ShipmentRequestQueueSort?)null
        };
        if (parsed.HasValue)
        {
            sort = parsed.Value;
            return true;
        }
        error = "Sort must be PRIORITY, REQUESTED_TIME, REQUESTED_TIME_DESC, NEWEST, OLDEST, or CUSTOMER.";
        return false;
    }

    private static string ToStatusValue(ShipmentRequestStatus status)
    {
        return status switch
        {
            ShipmentRequestStatus.Draft => "DRAFT",
            ShipmentRequestStatus.Submitted => "SUBMITTED",
            ShipmentRequestStatus.Approved => "APPROVED",
            ShipmentRequestStatus.Rejected => "REJECTED",
            ShipmentRequestStatus.ConvertedToTrip => "CONVERTED_TO_TRIP",
            ShipmentRequestStatus.NeedsRevision => "NEEDS_REVISION",
            _ => "DRAFT"
        };
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
            item.ContainerNumber,
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
            detail.ContainerNumber,
            detail.Status,
            detail.PickupLocation,
            detail.DropoffLocation,
            detail.PickupTime,
            detail.DropoffTime,
            detail.DeliveredTime,
            detail.PodState,
            detail.AtwState,
            detail.WaybillGenerated,
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
