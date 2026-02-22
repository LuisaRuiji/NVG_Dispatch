using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed record CreateApprovalCommand(
    string WorkflowKey,
    string EntityType,
    Guid EntityId,
    Guid CreatedBy);

public sealed record ApprovalDecisionResult(
    Approval Approval,
    Request? Request);

public sealed class ApprovalService
{
    private readonly InventoryDbContext _dbContext;
    private readonly UserService _userService;

    public ApprovalService(InventoryDbContext dbContext, UserService userService)
    {
        _dbContext = dbContext;
        _userService = userService;
    }

    public async Task<Approval> CreateApprovalAsync(
        CreateApprovalCommand command,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _dbContext.Workflows
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.WorkflowKey == command.WorkflowKey, cancellationToken);

        if (workflow is null || !workflow.IsActive)
        {
            throw new NotFoundException("Workflow not found or inactive.");
        }

        if (workflow.Steps.Count == 0)
        {
            throw new BusinessRuleViolationException("Workflow step configuration is invalid.");
        }

        var currentStep = workflow.Steps.Min(step => step.StepOrder);
        var now = DateTime.UtcNow;

        var approval = new Approval
        {
            Id = Guid.NewGuid(),
            WorkflowKey = command.WorkflowKey,
            EntityType = command.EntityType,
            EntityId = command.EntityId,
            Status = ApprovalStatus.Pending,
            CurrentStep = currentStep,
            CreatedBy = command.CreatedBy,
            CreatedAt = now
        };

        _dbContext.Approvals.Add(approval);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return approval;
    }

    public async Task<ApprovalDecisionResult> ApplyDecisionAsync(
        Guid approvalId,
        Guid actorUserId,
        ApprovalDecision decision,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        var approval = await _dbContext.Approvals
            .FirstOrDefaultAsync(a => a.Id == approvalId, cancellationToken);

        if (approval is null)
        {
            throw new NotFoundException("Approval not found.");
        }

        if (approval.Status != ApprovalStatus.Pending)
        {
            throw new BusinessRuleViolationException("Approval is not pending.");
        }

        var steps = await _dbContext.WorkflowSteps
            .Where(step => step.WorkflowKey == approval.WorkflowKey)
            .OrderBy(step => step.StepOrder)
            .ToListAsync(cancellationToken);

        if (steps.Count == 0)
        {
            throw new BusinessRuleViolationException("Workflow step configuration is invalid.");
        }

        var currentStep = steps.FirstOrDefault(step => step.StepOrder == approval.CurrentStep);
        if (currentStep is null)
        {
            throw new BusinessRuleViolationException("Workflow step configuration is invalid.");
        }

        await _userService.EnsureUserHasRoleAsync(actorUserId, currentStep.RequiredRole, cancellationToken);

        var now = DateTime.UtcNow;
        var action = new ApprovalAction
        {
            Id = Guid.NewGuid(),
            ApprovalId = approval.Id,
            StepOrder = currentStep.StepOrder,
            ActorUserId = actorUserId,
            Decision = decision,
            Remarks = remarks,
            ActedAt = now
        };

        _dbContext.ApprovalActions.Add(action);

        Request? request = null;
        if (approval.EntityType == EntityTypes.Request)
        {
            request = await _dbContext.Requests
                .FirstOrDefaultAsync(r => r.Id == approval.EntityId, cancellationToken);

            if (request is null)
            {
                throw new NotFoundException("Associated request not found.");
            }
        }

        if (decision == ApprovalDecision.Reject)
        {
            approval.Status = ApprovalStatus.Rejected;
            approval.UpdatedAt = now;

            if (request is not null)
            {
                request.Status = RequestStatus.Rejected;
                request.UpdatedAt = now;
            }
        }
        else
        {
            var nextStep = steps.FirstOrDefault(step => step.StepOrder > approval.CurrentStep);
            if (nextStep is null)
            {
                approval.Status = ApprovalStatus.Approved;
                approval.UpdatedAt = now;

                if (request is not null)
                {
                    request.Status = RequestStatus.Approved;
                    request.ApprovedAt = now;
                    request.UpdatedAt = now;
                }
            }
            else
            {
                approval.CurrentStep = nextStep.StepOrder;
                approval.UpdatedAt = now;

                if (request is not null)
                {
                    var nextStatus = MapRoleToRequestStatus(nextStep.RequiredRole);
                    if (nextStatus.HasValue)
                    {
                        request.Status = nextStatus.Value;
                        request.UpdatedAt = now;
                    }
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ApprovalDecisionResult(approval, request);
    }

    private static RequestStatus? MapRoleToRequestStatus(string requiredRole)
    {
        if (string.Equals(requiredRole, RoleNames.InventoryOfficer, StringComparison.OrdinalIgnoreCase))
        {
            return RequestStatus.PendingIO;
        }

        if (string.Equals(requiredRole, RoleNames.Manager, StringComparison.OrdinalIgnoreCase))
        {
            return RequestStatus.PendingManager;
        }

        return null;
    }
}
