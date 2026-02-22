using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Contracts;
using NVGInventory.Domain.Services;
using NVGInventory.Security;

namespace NVGInventory.Controllers;

[ApiController]
[Route("api/approvals")]
[Authorize]
public sealed class ApprovalsController : ControllerBase
{
    private readonly ApprovalService _approvalService;

    public ApprovalsController(ApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    [HttpPost("{approvalId:guid}/actions")]
    public async Task<ActionResult<ApprovalActionResponse>> ApplyAction(
        Guid approvalId,
        ApprovalActionRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await _approvalService.ApplyDecisionAsync(
            approvalId,
            actorUserId,
            request.Decision,
            request.Remarks,
            cancellationToken);

        return Ok(new ApprovalActionResponse(result.Approval.Id, result.Approval.Status, result.Approval.CurrentStep));
    }
}
