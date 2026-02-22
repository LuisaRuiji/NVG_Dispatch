using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Services;

public sealed record InventoryAdjustmentLineDetail(
    Guid Id,
    Guid InventoryId,
    string InventoryName,
    decimal QtyDelta,
    string? Remarks);

public sealed record InventoryAdjustmentDetail(
    Guid Id,
    InventoryAdjustmentStatus Status,
    string Reason,
    Guid CreatedByUserId,
    string CreatedByUsername,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt,
    string? RejectionReason,
    IReadOnlyCollection<InventoryAdjustmentLineDetail> Lines,
    ApprovalSummary? Approval,
    IReadOnlyCollection<ApprovalActionSummary> ApprovalActions);

public sealed class InventoryAdjustmentQueryService
{
    private readonly InventoryDbContext _dbContext;

    public InventoryAdjustmentQueryService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InventoryAdjustmentDetail?> GetAdjustmentDetailAsync(
        Guid adjustmentId,
        CancellationToken cancellationToken = default)
    {
        var adjustment = await _dbContext.InventoryAdjustments
            .AsNoTracking()
            .Include(adj => adj.CreatedBy)
            .Include(adj => adj.Lines)
            .ThenInclude(line => line.InventoryItem)
            .FirstOrDefaultAsync(adj => adj.Id == adjustmentId, cancellationToken);

        if (adjustment is null)
        {
            return null;
        }

        ApprovalSummary? approvalSummary = null;
        List<ApprovalActionSummary> approvalActions = new();

        var approval = await _dbContext.Approvals
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.EntityType == EntityTypes.InventoryAdjustment && a.EntityId == adjustment.Id,
                cancellationToken);

        if (approval is not null)
        {
            string? nextApproverRole = null;
            if (approval.Status == ApprovalStatus.Pending)
            {
                nextApproverRole = await _dbContext.WorkflowSteps
                    .AsNoTracking()
                    .Where(step => step.WorkflowKey == approval.WorkflowKey && step.StepOrder == approval.CurrentStep)
                    .Select(step => step.RequiredRole)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            approvalSummary = new ApprovalSummary(
                approval.Id,
                approval.Status,
                approval.CurrentStep,
                nextApproverRole,
                approval.WorkflowKey);

            approvalActions = await _dbContext.ApprovalActions
                .AsNoTracking()
                .Include(action => action.Actor)
                .Where(action => action.ApprovalId == approval.Id)
                .OrderBy(action => action.ActedAt)
                .Select(action => new ApprovalActionSummary(
                    action.Id,
                    action.Decision,
                    action.Remarks,
                    action.ActorUserId,
                    action.Actor != null ? action.Actor.Username : null,
                    action.ActedAt,
                    action.StepOrder))
                .ToListAsync(cancellationToken);
        }

        var lines = adjustment.Lines
            .Select(line => new InventoryAdjustmentLineDetail(
                line.Id,
                line.InventoryId,
                line.InventoryItem?.Name ?? string.Empty,
                line.QtyDelta,
                line.Remarks))
            .ToList();

        return new InventoryAdjustmentDetail(
            adjustment.Id,
            adjustment.Status,
            adjustment.Reason,
            adjustment.CreatedByUserId,
            adjustment.CreatedBy?.Username ?? string.Empty,
            adjustment.CreatedAt,
            adjustment.SubmittedAt,
            adjustment.ApprovedAt,
            adjustment.RejectedAt,
            adjustment.RejectionReason,
            lines,
            approvalSummary,
            approvalActions);
    }
}
