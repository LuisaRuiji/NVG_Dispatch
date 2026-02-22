using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;
using NVGInventory.Security;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize]
public sealed class RequestsController : ControllerBase
{
    private readonly RequestWorkflowService _workflowService;
    private readonly RequestQueryService _queryService;
    private readonly RequestService _requestService;
    private readonly UserService _userService;

    public RequestsController(
        RequestWorkflowService workflowService,
        RequestQueryService queryService,
        RequestService requestService,
        UserService userService)
    {
        _workflowService = workflowService;
        _queryService = queryService;
        _requestService = requestService;
        _userService = userService;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<CreateRequestDraftResponse>> CreateRequestDraft(
        CreateRequestDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (request.RequestType is not (RequestType.MaintenanceIssue or RequestType.Borrow))
        {
            return BadRequest("Unsupported request type.");
        }

        var requesterUserId = User.GetUserId();
        await _userService.EnsureActiveUserAsync(requesterUserId, cancellationToken);

        var created = await _requestService.CreateRequestAsync(
            new CreateRequestCommand(
                request.RequestType,
                requesterUserId,
                request.AssetId,
                request.Purpose,
                request.Lines.Select(line => new RequestLineInput(line.InventoryId, line.Quantity, line.Remarks)).ToList()),
            RequestStatus.Draft,
            cancellationToken);

        return Ok(new CreateRequestDraftResponse(created.Id, created.Status));
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<RequestListItemResponse>>> GetRequests(
        [FromQuery] string? type,
        [FromQuery] string[]? status,
        [FromQuery] Guid? requesterUserId,
        [FromQuery] Guid? assetId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        RequestType? requestType = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!QueryParsing.TryParseEnum(type, out RequestType parsedType))
            {
                return BadRequest("Invalid request type.");
            }

            requestType = parsedType;
        }

        var rawStatuses = new List<string>();
        if (status is { Length: > 0 })
        {
            foreach (var entry in status)
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                rawStatuses.AddRange(entry.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        List<RequestStatus>? requestStatuses = null;
        if (rawStatuses.Count > 0)
        {
            requestStatuses = new List<RequestStatus>();
            foreach (var rawStatus in rawStatuses)
            {
                if (!QueryParsing.TryParseEnum(rawStatus, out RequestStatus parsedStatus))
                {
                    return BadRequest("Invalid request status.");
                }

                if (!requestStatuses.Contains(parsedStatus))
                {
                    requestStatuses.Add(parsedStatus);
                }
            }
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

        var results = await _queryService.GetRequestsAsync(
            requestType,
            requestStatuses,
            requesterUserId,
            assetId,
            resolvedPage,
            resolvedPageSize,
            cancellationToken);

        var responseItems = results.Items
            .Select(item => new RequestListItemResponse(
                item.Id,
                item.RequestType,
                item.Status,
                item.RequesterUserId,
                item.RequesterUsername,
                item.AssetId,
                item.AssetCode,
                item.Purpose,
                item.CreatedAt,
                item.SubmittedAt,
                item.ApprovedAt,
                item.IssuedAt,
                item.ClosedAt))
            .ToList();

        var response = new PagedResult<RequestListItemResponse>(
            responseItems,
            results.TotalCount,
            resolvedPage,
            resolvedPageSize);

        return Ok(response);
    }

    [HttpGet("{requestId:guid}")]
    [Authorize]
    public async Task<ActionResult<RequestDetailResponse>> GetRequest(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var detail = await _queryService.GetRequestDetailAsync(requestId, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        var response = new RequestDetailResponse(
            detail.Id,
            detail.RequestType,
            detail.Status,
            detail.RequesterUserId,
            detail.RequesterUsername,
            detail.AssetId,
            detail.AssetCode,
            detail.Purpose,
            detail.CreatedAt,
            detail.SubmittedAt,
            detail.ApprovedAt,
            detail.IssuedAt,
            detail.ClosedAt,
            detail.LoanId,
            detail.Lines.Select(line => new RequestLineDetailResponse(
                line.Id,
                line.InventoryId,
                line.InventoryName,
                line.Unit,
                line.ItemType,
                line.CurrentQuantity,
                line.QtyRequested,
                line.QtyApproved,
                line.Remarks)).ToList(),
            detail.Approval is null
                ? null
                : new ApprovalSummaryResponse(
                    detail.Approval.ApprovalId,
                    detail.Approval.Status,
                    detail.Approval.CurrentStep,
                    detail.Approval.NextApproverRole,
                    detail.Approval.WorkflowKey),
            detail.ApprovalActions.Select(action => new ApprovalActionSummaryResponse(
                action.Id,
                action.Decision,
                action.Remarks,
                action.ActorUserId,
                action.ActorUsername,
                action.CreatedAt,
                action.StepOrder)).ToList(),
            detail.StockLogs.Select(log => new StockLogSummaryResponse(
                log.Id,
                log.MovementType,
                log.Quantity,
                log.ActorUserId,
                log.ActorUsername,
                log.CreatedAt)).ToList(),
            BuildHistory(detail));

        return Ok(response);
    }

    private static IReadOnlyCollection<HistoryEntryResponse> BuildHistory(RequestDetail detail)
    {
        var history = new List<HistoryEntryResponse>();

        foreach (var action in detail.ApprovalActions)
        {
            history.Add(new HistoryEntryResponse(
                "APPROVAL",
                action.ActorUsername ?? action.ActorUserId.ToString(),
                action.Decision.ToString().ToUpperInvariant(),
                action.StepOrder,
                null,
                null,
                null,
                action.CreatedAt));
        }

        foreach (var log in detail.StockLogs)
        {
            var quantity = log.MovementType == Domain.Enums.StockMovementType.Adjustment
                ? log.Quantity
                : Math.Abs(log.Quantity);

            history.Add(new HistoryEntryResponse(
                "STOCK_MOVEMENT",
                log.ActorUsername ?? log.ActorUserId.ToString(),
                null,
                null,
                log.MovementType.ToString().ToUpperInvariant(),
                quantity,
                null,
                log.CreatedAt));
        }

        return history
            .OrderBy(entry => entry.Timestamp)
            .ToList();
    }

    [HttpPost("maintenance-issue")]
    [Authorize]
    public async Task<ActionResult<SubmitMaintenanceIssueResponse>> SubmitMaintenanceIssue(
        SubmitMaintenanceIssueRequest request,
        CancellationToken cancellationToken)
    {
        var requesterUserId = User.GetUserId();
        var result = await _workflowService.SubmitMaintenanceIssueAsync(
            new SubmitMaintenanceIssueCommand(
                requesterUserId,
                request.AssetId,
                request.Purpose,
                request.Lines.Select(line => new RequestLineInput(line.InventoryId, line.Quantity, line.Remarks)).ToList()),
            cancellationToken);

        return Ok(new SubmitMaintenanceIssueResponse(result.RequestId, result.ApprovalId, result.Status));
    }

    [HttpPost("{requestId:guid}/issue")]
    [Authorize(Roles = RoleNames.InventoryOfficer)]
    public async Task<ActionResult<IssueRequestResponse>> IssueMaintenanceRequest(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await _workflowService.IssueMaintenanceIssueAsync(
            new IssueRequestCommand(requestId, actorUserId),
            cancellationToken);

        return Ok(new IssueRequestResponse(result.RequestId, result.Status));
    }

    [HttpPost("{requestId:guid}/submit")]
    [Authorize]
    public async Task<ActionResult<SubmitRequestResponse>> SubmitRequest(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var status = await _workflowService.SubmitRequest(requestId, cancellationToken);
        return Ok(new SubmitRequestResponse(requestId, status));
    }

    [HttpPost("{requestId:guid}/io-review")]
    [Authorize(Roles = RoleNames.InventoryOfficer)]
    public async Task<ActionResult<InventoryOfficerReviewResponse>> InventoryOfficerReview(
        Guid requestId,
        InventoryOfficerReviewRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var remarks = request.Remarks ?? request.IoRemarks;
        var lines = request.Lines;

        if (request.Decision == ApprovalDecision.Reject)
        {
            if (string.IsNullOrWhiteSpace(remarks))
            {
                return BadRequest("Remarks are required to reject a request.");
            }

            lines = request.Lines
                .Select(line => new InventoryOfficerReviewLine(line.RequestLineId, 0m, line.Remarks))
                .ToList();
        }

        var status = await _workflowService.InventoryOfficerReviewAsync(
            requestId,
            actorUserId,
            lines.Select(line => (line.RequestLineId, line.QtyApproved, line.Remarks)).ToList(),
            remarks,
            cancellationToken);

        return Ok(new InventoryOfficerReviewResponse(requestId, status));
    }

    [HttpPost("{requestId:guid}/manager-decision")]
    [Authorize(Roles = RoleNames.Manager)]
    public async Task<ActionResult<ApproveAsManagerResponse>> ManagerDecision(
        Guid requestId,
        ManagerDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var status = await _workflowService.ManagerDecisionAsync(
            requestId,
            actorUserId,
            request.Decision,
            request.Remarks,
            cancellationToken);

        return Ok(new ApproveAsManagerResponse(requestId, status));
    }

    [HttpPost("{requestId:guid}/issue-stock")]
    [Authorize(Roles = RoleNames.InventoryOfficer)]
    public async Task<ActionResult<IssueRequestResponse>> IssueRequest(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var status = await _workflowService.IssueRequest(requestId, actorUserId, cancellationToken);
        return Ok(new IssueRequestResponse(requestId, status));
    }

}
