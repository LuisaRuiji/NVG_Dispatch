using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Services;
using NVGInventory.Security;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/inventory-adjustments")]
[Authorize]
public sealed class InventoryAdjustmentsController : ControllerBase
{
    private readonly InventoryAdjustmentWorkflowService _workflowService;
    private readonly InventoryAdjustmentQueryService _queryService;

    public InventoryAdjustmentsController(
        InventoryAdjustmentWorkflowService workflowService,
        InventoryAdjustmentQueryService queryService)
    {
        _workflowService = workflowService;
        _queryService = queryService;
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.InventoryOfficer)]
    public async Task<ActionResult<CreateInventoryAdjustmentDraftResponse>> CreateDraft(
        CreateInventoryAdjustmentDraftRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await _workflowService.CreateDraftAsync(
            new CreateInventoryAdjustmentDraftCommand(
                actorUserId,
                request.Reason,
                request.Lines.Select(line => new InventoryAdjustmentLineInput(
                    line.InventoryId,
                    line.QtyDelta,
                    line.Remarks)).ToList()),
            cancellationToken);

        return Ok(new CreateInventoryAdjustmentDraftResponse(result.AdjustmentId, result.Status));
    }

    [HttpPost("{adjustmentId:guid}/submit")]
    [Authorize(Roles = RoleNames.InventoryOfficer)]
    public async Task<ActionResult<SubmitInventoryAdjustmentResponse>> Submit(
        Guid adjustmentId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await _workflowService.SubmitAsync(adjustmentId, actorUserId, cancellationToken);
        return Ok(new SubmitInventoryAdjustmentResponse(result.AdjustmentId, result.Status));
    }

    [HttpPost("{adjustmentId:guid}/approve")]
    [Authorize(Roles = RoleNames.Manager)]
    public async Task<ActionResult<InventoryAdjustmentDecisionResponse>> Approve(
        Guid adjustmentId,
        InventoryAdjustmentDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await _workflowService.ApproveAsync(adjustmentId, actorUserId, request.Remarks, cancellationToken);
        return Ok(new InventoryAdjustmentDecisionResponse(result.AdjustmentId, result.Status, result.ApprovalStatus));
    }

    [HttpPost("{adjustmentId:guid}/reject")]
    [Authorize(Roles = RoleNames.Manager)]
    public async Task<ActionResult<InventoryAdjustmentDecisionResponse>> Reject(
        Guid adjustmentId,
        InventoryAdjustmentDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await _workflowService.RejectAsync(adjustmentId, actorUserId, request.Remarks, cancellationToken);
        return Ok(new InventoryAdjustmentDecisionResponse(result.AdjustmentId, result.Status, result.ApprovalStatus));
    }

    [HttpGet("{adjustmentId:guid}")]
    [Authorize(Roles = $"{RoleNames.InventoryOfficer},{RoleNames.Manager}")]
    public async Task<ActionResult<InventoryAdjustmentDetailResponse>> GetDetail(
        Guid adjustmentId,
        CancellationToken cancellationToken)
    {
        var detail = await _queryService.GetAdjustmentDetailAsync(adjustmentId, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        var response = new InventoryAdjustmentDetailResponse(
            detail.Id,
            detail.Status,
            detail.Reason,
            detail.CreatedByUserId,
            detail.CreatedByUsername,
            detail.CreatedAt,
            detail.SubmittedAt,
            detail.ApprovedAt,
            detail.RejectedAt,
            detail.RejectionReason,
            detail.Lines.Select(line => new InventoryAdjustmentLineDetailResponse(
                line.Id,
                line.InventoryId,
                line.InventoryName,
                line.QtyDelta,
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
                action.StepOrder)).ToList());

        return Ok(response);
    }
}
