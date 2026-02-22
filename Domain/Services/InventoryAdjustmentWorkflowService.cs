using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed record InventoryAdjustmentLineInput(Guid InventoryId, decimal QtyDelta, string? Remarks);

public sealed record CreateInventoryAdjustmentDraftCommand(
    Guid ActorUserId,
    string Reason,
    IReadOnlyCollection<InventoryAdjustmentLineInput> Lines);

public sealed record CreateInventoryAdjustmentDraftResult(Guid AdjustmentId, InventoryAdjustmentStatus Status);

public sealed record SubmitInventoryAdjustmentResult(Guid AdjustmentId, InventoryAdjustmentStatus Status, Guid ApprovalId);

public sealed record InventoryAdjustmentDecisionResult(Guid AdjustmentId, InventoryAdjustmentStatus Status, ApprovalStatus ApprovalStatus);

public sealed class InventoryAdjustmentWorkflowService
{
    private readonly InventoryDbContext _dbContext;
    private readonly ApprovalService _approvalService;
    private readonly StockLedgerService _stockLedgerService;
    private readonly UserService _userService;
    private readonly IAuditService? _auditService;

    public InventoryAdjustmentWorkflowService(
        InventoryDbContext dbContext,
        ApprovalService approvalService,
        StockLedgerService stockLedgerService,
        UserService userService,
        IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _approvalService = approvalService;
        _stockLedgerService = stockLedgerService;
        _userService = userService;
        _auditService = auditService;
    }

    public async Task<CreateInventoryAdjustmentDraftResult> CreateDraftAsync(
        CreateInventoryAdjustmentDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureUserHasRoleAsync(command.ActorUserId, RoleNames.InventoryOfficer, cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            throw new BusinessRuleViolationException("Adjustment reason is required.");
        }

        ValidateLines(command.Lines);

        var inventoryIds = command.Lines.Select(line => line.InventoryId).Distinct().ToArray();
        var inventoryItems = await _dbContext.InventoryItems
            .Where(item => inventoryIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        if (inventoryItems.Count != inventoryIds.Length)
        {
            throw new NotFoundException("One or more inventory items were not found.");
        }

        if (inventoryItems.Any(item => !item.IsActive))
        {
            throw new BusinessRuleViolationException("One or more inventory items are inactive.");
        }

        var now = DateTime.UtcNow;
        var adjustment = new InventoryAdjustment
        {
            Id = Guid.NewGuid(),
            Status = InventoryAdjustmentStatus.Draft,
            Reason = command.Reason,
            CreatedByUserId = command.ActorUserId,
            CreatedAt = now,
            UpdatedAt = now,
            Lines = command.Lines.Select(line => new InventoryAdjustmentLine
            {
                Id = Guid.NewGuid(),
                InventoryId = line.InventoryId,
                QtyDelta = line.QtyDelta,
                Remarks = line.Remarks,
                CreatedAt = now
            }).ToList()
        };

        _dbContext.InventoryAdjustments.Add(adjustment);
        _auditService?.AddEntry(
            command.ActorUserId,
            AuditActions.InventoryAdjustmentDraftCreated,
            EntityTypes.InventoryAdjustment,
            adjustment.Id,
            null,
            new { adjustment.Status, LineCount = adjustment.Lines.Count });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateInventoryAdjustmentDraftResult(adjustment.Id, adjustment.Status);
    }

    public async Task<SubmitInventoryAdjustmentResult> SubmitAsync(
        Guid adjustmentId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureUserHasRoleAsync(actorUserId, RoleNames.InventoryOfficer, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var adjustment = await _dbContext.InventoryAdjustments
            .Include(adjustment => adjustment.Lines)
            .FirstOrDefaultAsync(adjustment => adjustment.Id == adjustmentId, cancellationToken);

        if (adjustment is null)
        {
            throw new NotFoundException("Inventory adjustment not found.");
        }

        if (adjustment.Status != InventoryAdjustmentStatus.Draft)
        {
            throw new BusinessRuleViolationException("Only draft adjustments can be submitted.");
        }

        if (string.IsNullOrWhiteSpace(adjustment.Reason))
        {
            throw new BusinessRuleViolationException("Adjustment reason is required.");
        }

        if (adjustment.Lines.Count == 0)
        {
            throw new BusinessRuleViolationException("Adjustment must include at least one line.");
        }

        var approval = await _approvalService.CreateApprovalAsync(
            new CreateApprovalCommand(
                WorkflowKeys.AdjustmentApproval,
                EntityTypes.InventoryAdjustment,
                adjustment.Id,
                actorUserId),
            cancellationToken);

        var steps = await _dbContext.WorkflowSteps
            .Where(step => step.WorkflowKey == WorkflowKeys.AdjustmentApproval)
            .OrderBy(step => step.StepOrder)
            .ToListAsync(cancellationToken);

        if (steps.Count == 0)
        {
            throw new BusinessRuleViolationException("Adjustment workflow is not configured.");
        }

        var firstStep = steps.First();
        if (string.Equals(firstStep.RequiredRole, RoleNames.InventoryOfficer, StringComparison.OrdinalIgnoreCase))
        {
            var decision = await _approvalService.ApplyDecisionAsync(
                approval.Id,
                actorUserId,
                ApprovalDecision.Approve,
                "Submitted",
                cancellationToken);

            if (decision.Approval.Status == ApprovalStatus.Approved)
            {
                throw new BusinessRuleViolationException("Adjustment workflow must include a manager approval step.");
            }
        }

        var now = DateTime.UtcNow;
        adjustment.Status = InventoryAdjustmentStatus.PendingManager;
        adjustment.SubmittedAt = now;
        adjustment.UpdatedAt = now;

        _auditService?.AddEntry(
            actorUserId,
            AuditActions.InventoryAdjustmentSubmitted,
            EntityTypes.InventoryAdjustment,
            adjustment.Id,
            null,
            new { adjustment.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SubmitInventoryAdjustmentResult(adjustment.Id, adjustment.Status, approval.Id);
    }

    public async Task<InventoryAdjustmentDecisionResult> ApproveAsync(
        Guid adjustmentId,
        Guid actorUserId,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureUserHasRoleAsync(actorUserId, RoleNames.Manager, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var adjustment = await _dbContext.InventoryAdjustments
            .Include(adjustment => adjustment.Lines)
            .FirstOrDefaultAsync(adjustment => adjustment.Id == adjustmentId, cancellationToken);

        if (adjustment is null)
        {
            throw new NotFoundException("Inventory adjustment not found.");
        }

        if (adjustment.Status != InventoryAdjustmentStatus.PendingManager)
        {
            throw new BusinessRuleViolationException("Adjustment is not pending manager approval.");
        }

        var approval = await _dbContext.Approvals
            .FirstOrDefaultAsync(
                record => record.EntityType == EntityTypes.InventoryAdjustment
                          && record.EntityId == adjustment.Id,
                cancellationToken);

        if (approval is null)
        {
            throw new NotFoundException("Approval record not found for adjustment.");
        }

        var hasLogs = await _dbContext.StockLogs
            .AsNoTracking()
            .AnyAsync(
                log => log.RefType == EntityTypes.InventoryAdjustment
                       && log.RefId == adjustment.Id
                       && log.MovementType == StockMovementType.Adjustment,
                cancellationToken);

        if (hasLogs)
        {
            throw new BusinessRuleViolationException("Adjustment has already been applied.");
        }

        var decision = await _approvalService.ApplyDecisionAsync(
            approval.Id,
            actorUserId,
            ApprovalDecision.Approve,
            remarks,
            cancellationToken);

        foreach (var line in adjustment.Lines)
        {
            if (line.QtyDelta == 0)
            {
                continue;
            }

            await _stockLedgerService.ApplyMovement(
                StockMovementType.Adjustment,
                line.InventoryId,
                line.QtyDelta,
                EntityTypes.InventoryAdjustment,
                adjustment.Id,
                actorUserId,
                cancellationToken);
        }

        var now = DateTime.UtcNow;
        adjustment.Status = InventoryAdjustmentStatus.Approved;
        adjustment.ApprovedAt = now;
        adjustment.UpdatedAt = now;

        _auditService?.AddEntry(
            actorUserId,
            AuditActions.InventoryAdjustmentApproved,
            EntityTypes.InventoryAdjustment,
            adjustment.Id,
            null,
            new { adjustment.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new InventoryAdjustmentDecisionResult(
            adjustment.Id,
            adjustment.Status,
            decision.Approval.Status);
    }

    public async Task<InventoryAdjustmentDecisionResult> RejectAsync(
        Guid adjustmentId,
        Guid actorUserId,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureUserHasRoleAsync(actorUserId, RoleNames.Manager, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var adjustment = await _dbContext.InventoryAdjustments
            .Include(adjustment => adjustment.Lines)
            .FirstOrDefaultAsync(adjustment => adjustment.Id == adjustmentId, cancellationToken);

        if (adjustment is null)
        {
            throw new NotFoundException("Inventory adjustment not found.");
        }

        if (adjustment.Status != InventoryAdjustmentStatus.PendingManager)
        {
            throw new BusinessRuleViolationException("Adjustment is not pending manager approval.");
        }

        var approval = await _dbContext.Approvals
            .FirstOrDefaultAsync(
                record => record.EntityType == EntityTypes.InventoryAdjustment
                          && record.EntityId == adjustment.Id,
                cancellationToken);

        if (approval is null)
        {
            throw new NotFoundException("Approval record not found for adjustment.");
        }

        var decision = await _approvalService.ApplyDecisionAsync(
            approval.Id,
            actorUserId,
            ApprovalDecision.Reject,
            remarks,
            cancellationToken);

        var now = DateTime.UtcNow;
        adjustment.Status = InventoryAdjustmentStatus.Rejected;
        adjustment.RejectedAt = now;
        adjustment.RejectionReason = remarks;
        adjustment.UpdatedAt = now;

        _auditService?.AddEntry(
            actorUserId,
            AuditActions.InventoryAdjustmentRejected,
            EntityTypes.InventoryAdjustment,
            adjustment.Id,
            null,
            new { adjustment.Status, Remarks = remarks });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new InventoryAdjustmentDecisionResult(
            adjustment.Id,
            adjustment.Status,
            decision.Approval.Status);
    }

    private static void ValidateLines(IReadOnlyCollection<InventoryAdjustmentLineInput> lines)
    {
        if (lines.Count == 0)
        {
            throw new BusinessRuleViolationException("Adjustment must include at least one line.");
        }

        var duplicates = lines
            .GroupBy(line => line.InventoryId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleViolationException("Duplicate inventory items are not allowed on the same adjustment.");
        }

        foreach (var line in lines)
        {
            if (line.QtyDelta == 0)
            {
                throw new BusinessRuleViolationException("Adjustment quantity must be non-zero.");
            }

            if (line.QtyDelta % 1m != 0m)
            {
                throw new BusinessRuleViolationException("Adjustment quantity must be a whole number.");
            }
        }
    }
}
