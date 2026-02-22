using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Services;
using NVGInventory.Security;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly PurchaseOrderWorkflowService _workflowService;
    private readonly PurchaseOrderQueryService _queryService;

    public PurchaseOrdersController(
        PurchaseOrderWorkflowService workflowService,
        PurchaseOrderQueryService queryService)
    {
        _workflowService = workflowService;
        _queryService = queryService;
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.InventoryOfficer)]
    public async Task<ActionResult<CreatePurchaseOrderDraftResponse>> CreateDraft(
        CreatePurchaseOrderDraftRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var po = await _workflowService.CreateDraftAsync(
            new CreatePurchaseOrderDraftCommand(
                actorUserId,
                request.SupplierId,
                request.Notes,
                request.Lines.Select(line => new PurchaseOrderLineInput(
                    line.InventoryId,
                    line.Quantity,
                    line.UnitPrice,
                    line.Remarks)).ToList()),
            cancellationToken);

        return Ok(new CreatePurchaseOrderDraftResponse(po.Id, po.Status));
    }

    [HttpPost("{purchaseOrderId:guid}/submit")]
    [Authorize(Roles = RoleNames.InventoryOfficer)]
    public async Task<ActionResult<SubmitPurchaseOrderResponse>> Submit(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await _workflowService.SubmitAsync(purchaseOrderId, actorUserId, cancellationToken);
        return Ok(new SubmitPurchaseOrderResponse(result.PurchaseOrderId, result.Status));
    }

    [HttpPost("{purchaseOrderId:guid}/approve")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.HeadOfFinance},{RoleNames.Ceo}")]
    public async Task<ActionResult<PurchaseOrderDecisionResponse>> Approve(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await _workflowService.ApplyDecisionAsync(
            purchaseOrderId,
            actorUserId,
            ApprovalDecision.Approve,
            null,
            cancellationToken);

        return Ok(new PurchaseOrderDecisionResponse(
            result.PurchaseOrderId,
            result.Status,
            result.ApprovalStatus));
    }

    [HttpPost("{purchaseOrderId:guid}/decision")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.HeadOfFinance},{RoleNames.Ceo}")]
    public async Task<ActionResult<PurchaseOrderDecisionResponse>> Decision(
        Guid purchaseOrderId,
        PurchaseOrderDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await _workflowService.ApplyDecisionAsync(
            purchaseOrderId,
            actorUserId,
            request.Decision,
            request.Remarks,
            cancellationToken);

        return Ok(new PurchaseOrderDecisionResponse(
            result.PurchaseOrderId,
            result.Status,
            result.ApprovalStatus));
    }

    [HttpPost("{purchaseOrderId:guid}/reject")]
    [Authorize(Roles = $"{RoleNames.Manager},{RoleNames.HeadOfFinance},{RoleNames.Ceo}")]
    public async Task<ActionResult<PurchaseOrderDecisionResponse>> Reject(
        Guid purchaseOrderId,
        PurchaseOrderDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await _workflowService.ApplyDecisionAsync(
            purchaseOrderId,
            actorUserId,
            ApprovalDecision.Reject,
            request.Remarks,
            cancellationToken);

        return Ok(new PurchaseOrderDecisionResponse(
            result.PurchaseOrderId,
            result.Status,
            result.ApprovalStatus));
    }

    [HttpPost("{purchaseOrderId:guid}/receive")]
    [Authorize(Roles = RoleNames.InventoryOfficer)]
    public async Task<ActionResult<PurchaseOrderReceiveResponse>> Receive(
        Guid purchaseOrderId,
        PurchaseOrderReceiveRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await _workflowService.ReceiveAsync(
            purchaseOrderId,
            actorUserId,
            request.Lines.Select(line => new PurchaseOrderReceiveLineInput(
                line.PurchaseOrderLineId,
                line.QtyReceived,
                line.Remarks)).ToList(),
            request.Remarks,
            cancellationToken);

        return Ok(new PurchaseOrderReceiveResponse(result.PurchaseOrderId, result.Status));
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.HeadOfFinance},{RoleNames.Ceo}")]
    public async Task<ActionResult<PagedResult<PurchaseOrderListItemResponse>>> GetPurchaseOrders(
        [FromQuery] string[]? status,
        [FromQuery] Guid? createdByUserId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
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

        List<PurchaseOrderStatus>? statuses = null;
        if (rawStatuses.Count > 0)
        {
            statuses = new List<PurchaseOrderStatus>();
            foreach (var rawStatus in rawStatuses)
            {
                if (!QueryParsing.TryParseEnum(rawStatus, out PurchaseOrderStatus parsedStatus))
                {
                    return BadRequest("Invalid purchase order status.");
                }

                if (!statuses.Contains(parsedStatus))
                {
                    statuses.Add(parsedStatus);
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

        var results = await _queryService.GetPurchaseOrdersAsync(
            statuses,
            createdByUserId,
            resolvedPage,
            resolvedPageSize,
            cancellationToken);

        var responseItems = results.Items
            .Select(item => new PurchaseOrderListItemResponse(
                item.Id,
                item.Status,
                item.CreatedByUserId,
                item.CreatedByUsername,
                item.SupplierId,
                item.SupplierName,
                item.CreatedAt,
                item.SubmittedAt,
                item.ApprovedAt,
                item.ReceivedAt,
                item.ClosedAt))
            .ToList();

        var response = new PagedResult<PurchaseOrderListItemResponse>(
            responseItems,
            results.TotalCount,
            resolvedPage,
            resolvedPageSize);

        return Ok(response);
    }

    [HttpGet("{purchaseOrderId:guid}")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager},{RoleNames.HeadOfFinance},{RoleNames.Ceo}")]
    public async Task<ActionResult<PurchaseOrderDetailResponse>> GetPurchaseOrder(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var detail = await _queryService.GetPurchaseOrderDetailAsync(purchaseOrderId, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        var response = new PurchaseOrderDetailResponse(
            detail.Id,
            detail.Status,
            detail.CreatedByUserId,
            detail.CreatedByUsername,
            detail.SupplierId,
            detail.SupplierName,
            detail.Notes,
            detail.CreatedAt,
            detail.SubmittedAt,
            detail.ApprovedAt,
            detail.RejectedAt,
            detail.RejectionReason,
            detail.ReceivedAt,
            detail.ClosedAt,
            detail.Lines.Select(line => new PurchaseOrderLineDetailResponse(
                line.Id,
                line.InventoryId,
                line.InventoryName,
                line.Unit,
                line.ItemType,
                line.QtyOrdered,
                line.QtyReceived,
                line.UnitPrice,
                line.Remarks)).ToList(),
            detail.Receipts.Select(receipt => new PurchaseOrderReceiptResponse(
                receipt.Id,
                receipt.PurchaseOrderLineId,
                receipt.QtyReceivedIncrement,
                receipt.ReceivedByUserId,
                receipt.ReceivedByUsername,
                receipt.ReceivedAt)).ToList(),
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
                action.StepOrder)).ToList());

        return Ok(response);
    }
}
