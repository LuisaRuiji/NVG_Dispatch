using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed record PurchaseOrderSubmitResult(Guid PurchaseOrderId, PurchaseOrderStatus Status, Guid ApprovalId);

public sealed record PurchaseOrderDecisionResult(Guid PurchaseOrderId, PurchaseOrderStatus Status, ApprovalStatus ApprovalStatus);

public sealed record PurchaseOrderReceiveLineInput(Guid PurchaseOrderLineId, decimal QtyReceived, string? Remarks);

public sealed record PurchaseOrderReceiveResult(Guid PurchaseOrderId, PurchaseOrderStatus Status);

public sealed class PurchaseOrderWorkflowService
{
    private readonly InventoryDbContext _dbContext;
    private readonly PurchaseOrderService _purchaseOrderService;
    private readonly ApprovalService _approvalService;
    private readonly StockLedgerService _stockLedgerService;
    private readonly UserService _userService;
    private readonly IAuditService? _auditService;
    private readonly ILogger<PurchaseOrderWorkflowService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PurchaseOrderWorkflowService(
        InventoryDbContext dbContext,
        PurchaseOrderService purchaseOrderService,
        ApprovalService approvalService,
        StockLedgerService stockLedgerService,
        UserService userService,
        IAuditService? auditService = null,
        ILogger<PurchaseOrderWorkflowService>? logger = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _dbContext = dbContext;
        _purchaseOrderService = purchaseOrderService;
        _approvalService = approvalService;
        _stockLedgerService = stockLedgerService;
        _userService = userService;
        _auditService = auditService;
        _logger = logger ?? NullLogger<PurchaseOrderWorkflowService>.Instance;
        _httpContextAccessor = httpContextAccessor ?? new HttpContextAccessor();
    }

    private string GetCorrelationId()
    {
        return _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString()
               ?? _httpContextAccessor.HttpContext?.TraceIdentifier
               ?? "-";
    }

    public async Task<PurchaseOrder> CreateDraftAsync(
        CreatePurchaseOrderDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        await _userService.EnsureUserHasRoleAsync(command.CreatedByUserId, RoleNames.InventoryOfficer, cancellationToken);
        return await _purchaseOrderService.CreateDraftAsync(command, cancellationToken);
    }

    public async Task<PurchaseOrderSubmitResult> SubmitAsync(
        Guid purchaseOrderId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _userService.EnsureUserHasRoleAsync(actorUserId, RoleNames.InventoryOfficer, cancellationToken);

        var purchaseOrder = await _dbContext.PurchaseOrders
            .Include(order => order.Lines)
            .FirstOrDefaultAsync(order => order.Id == purchaseOrderId, cancellationToken);

        if (purchaseOrder is null)
        {
            throw new NotFoundException("Purchase order not found.");
        }

        if (purchaseOrder.Status != PurchaseOrderStatus.Draft)
        {
            throw new BusinessRuleViolationException("Only draft purchase orders can be submitted.");
        }

        if (purchaseOrder.Lines.Count == 0)
        {
            throw new BusinessRuleViolationException("Purchase order must include at least one line.");
        }

        var approval = await _approvalService.CreateApprovalAsync(
            new CreateApprovalCommand(
                WorkflowKeys.PoApproval,
                EntityTypes.PurchaseOrder,
                purchaseOrder.Id,
                actorUserId),
            cancellationToken);

        purchaseOrder.Status = PurchaseOrderStatus.PendingManager;
        purchaseOrder.SubmittedAt = DateTime.UtcNow;
        purchaseOrder.UpdatedAt = purchaseOrder.SubmittedAt;

        _auditService?.AddEntry(
            actorUserId,
            AuditActions.PurchaseOrderSubmitted,
            EntityTypes.PurchaseOrder,
            purchaseOrder.Id,
            null,
            new { purchaseOrder.Status });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Action={Action} EntityId={EntityId} ActorUserId={ActorUserId} CorrelationId={CorrelationId}",
            "SubmitPurchaseOrder",
            purchaseOrder.Id,
            actorUserId,
            GetCorrelationId());

        return new PurchaseOrderSubmitResult(purchaseOrder.Id, purchaseOrder.Status, approval.Id);
    }

    public async Task<PurchaseOrderDecisionResult> ApplyDecisionAsync(
        Guid purchaseOrderId,
        Guid actorUserId,
        ApprovalDecision decision,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var purchaseOrder = await _dbContext.PurchaseOrders
            .FirstOrDefaultAsync(order => order.Id == purchaseOrderId, cancellationToken);

        if (purchaseOrder is null)
        {
            throw new NotFoundException("Purchase order not found.");
        }

        if (purchaseOrder.Status is not (PurchaseOrderStatus.PendingManager
            or PurchaseOrderStatus.PendingFinance
            or PurchaseOrderStatus.PendingCeo))
        {
            throw new BusinessRuleViolationException("Purchase order is not pending approval.");
        }

        var approval = await _dbContext.Approvals
            .FirstOrDefaultAsync(
                a => a.EntityType == EntityTypes.PurchaseOrder && a.EntityId == purchaseOrder.Id,
                cancellationToken);

        if (approval is null)
        {
            throw new NotFoundException("Approval record not found for purchase order.");
        }

        var result = await _approvalService.ApplyDecisionAsync(
            approval.Id,
            actorUserId,
            decision,
            remarks,
            cancellationToken);

        var now = DateTime.UtcNow;
        if (result.Approval.Status == ApprovalStatus.Rejected)
        {
            purchaseOrder.Status = PurchaseOrderStatus.Rejected;
            purchaseOrder.RejectedAt = now;
            purchaseOrder.RejectionReason = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
            purchaseOrder.UpdatedAt = now;

            _auditService?.AddEntry(
                actorUserId,
                AuditActions.PurchaseOrderRejected,
                EntityTypes.PurchaseOrder,
                purchaseOrder.Id,
                null,
                new { purchaseOrder.Status, Remarks = remarks });
        }
        else if (result.Approval.Status == ApprovalStatus.Approved)
        {
            purchaseOrder.Status = PurchaseOrderStatus.Approved;
            purchaseOrder.ApprovedAt = now;
            purchaseOrder.RejectedAt = null;
            purchaseOrder.RejectionReason = null;
            purchaseOrder.UpdatedAt = now;

            _auditService?.AddEntry(
                actorUserId,
                AuditActions.PurchaseOrderApproved,
                EntityTypes.PurchaseOrder,
                purchaseOrder.Id,
                null,
                new { purchaseOrder.Status });
        }
        else
        {
            var nextRole = await _dbContext.WorkflowSteps
                .Where(step => step.WorkflowKey == result.Approval.WorkflowKey && step.StepOrder == result.Approval.CurrentStep)
                .Select(step => step.RequiredRole)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(nextRole))
            {
                throw new BusinessRuleViolationException("Workflow step configuration is invalid.");
            }

            purchaseOrder.Status = MapRoleToStatus(nextRole);
            purchaseOrder.UpdatedAt = now;

            _auditService?.AddEntry(
                actorUserId,
                AuditActions.PurchaseOrderApproved,
                EntityTypes.PurchaseOrder,
                purchaseOrder.Id,
                null,
                new { purchaseOrder.Status });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Action={Action} EntityId={EntityId} ActorUserId={ActorUserId} CorrelationId={CorrelationId}",
            "PurchaseOrderDecision",
            purchaseOrder.Id,
            actorUserId,
            GetCorrelationId());

        return new PurchaseOrderDecisionResult(
            purchaseOrder.Id,
            purchaseOrder.Status,
            result.Approval.Status);
    }

    public async Task<PurchaseOrderReceiveResult> ReceiveAsync(
        Guid purchaseOrderId,
        Guid actorUserId,
        IReadOnlyCollection<PurchaseOrderReceiveLineInput> lines,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            throw new BusinessRuleViolationException("Receive must include at least one line.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var purchaseOrder = await _dbContext.PurchaseOrders
            .Include(order => order.Lines)
            .FirstOrDefaultAsync(order => order.Id == purchaseOrderId, cancellationToken);

        if (purchaseOrder is null)
        {
            throw new NotFoundException("Purchase order not found.");
        }

        if (purchaseOrder.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
        {
            throw new BusinessRuleViolationException("Purchase order must be approved before receiving.");
        }

        await _userService.EnsureUserHasRoleAsync(actorUserId, RoleNames.InventoryOfficer, cancellationToken);

        var duplicateLine = lines
            .GroupBy(line => line.PurchaseOrderLineId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateLine is not null)
        {
            throw new BusinessRuleViolationException("Receive includes duplicate purchase order lines.");
        }

        var lineById = purchaseOrder.Lines.ToDictionary(line => line.Id, line => line);

        foreach (var line in lines)
        {
            if (!lineById.TryGetValue(line.PurchaseOrderLineId, out var poLine))
            {
                throw new BusinessRuleViolationException("Receive includes invalid purchase order line.");
            }

            if (line.QtyReceived <= 0)
            {
                throw new BusinessRuleViolationException("Received quantity must be greater than zero.");
            }

            if (line.QtyReceived % 1m != 0m)
            {
                throw new BusinessRuleViolationException("Received quantity must be a whole number.");
            }

            var remaining = poLine.QtyOrdered - poLine.QtyReceived;
            if (remaining <= 0)
            {
                throw new BusinessRuleViolationException("All ordered quantity already received.");
            }

            if (!poLine.UnitPrice.HasValue || poLine.UnitPrice.Value <= 0m)
            {
                throw new BusinessRuleViolationException("Unit price must be set before receiving.");
            }

            if (line.QtyReceived > remaining)
            {
                throw new BusinessRuleViolationException("Received quantity exceeds ordered quantity.");
            }
        }

        var now = DateTime.UtcNow;

        foreach (var line in lines)
        {
            var poLine = lineById[line.PurchaseOrderLineId];
            poLine.QtyReceived += line.QtyReceived;

            var receipt = new PurchaseOrderReceipt
            {
                Id = Guid.NewGuid(),
                PurchaseOrderLineId = poLine.Id,
                QtyReceivedIncrement = line.QtyReceived,
                ReceivedByUserId = actorUserId,
                ReceivedAt = now
            };

            _dbContext.PurchaseOrderReceipts.Add(receipt);

            _auditService?.AddEntry(
                actorUserId,
                AuditActions.PurchaseOrderReceived,
                EntityTypes.PurchaseOrderReceipt,
                receipt.Id,
                null,
                new
                {
                    purchaseOrder.Id,
                    receipt.PurchaseOrderLineId,
                    receipt.QtyReceivedIncrement
                });

            await _stockLedgerService.ApplyMovement(
                StockMovementType.In,
                poLine.InventoryId,
                line.QtyReceived,
                EntityTypes.PurchaseOrder,
                purchaseOrder.Id,
                actorUserId,
                poLine.UnitPrice,
                cancellationToken);
        }

        var fullyReceived = purchaseOrder.Lines.All(line => line.QtyReceived >= line.QtyOrdered);
        purchaseOrder.Status = fullyReceived ? PurchaseOrderStatus.Closed : PurchaseOrderStatus.PartiallyReceived;
        purchaseOrder.ReceivedAt = now;
        purchaseOrder.UpdatedAt = now;
        if (fullyReceived)
        {
            purchaseOrder.ClosedAt = now;
        }

        _auditService?.AddEntry(
            actorUserId,
            AuditActions.PurchaseOrderReceived,
            EntityTypes.PurchaseOrder,
            purchaseOrder.Id,
            null,
            new { purchaseOrder.Status, LineCount = lines.Count });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Action={Action} EntityId={EntityId} ActorUserId={ActorUserId} CorrelationId={CorrelationId}",
            "ReceivePurchaseOrder",
            purchaseOrder.Id,
            actorUserId,
            GetCorrelationId());

        return new PurchaseOrderReceiveResult(purchaseOrder.Id, purchaseOrder.Status);
    }

    private static PurchaseOrderStatus MapRoleToStatus(string requiredRole)
    {
        if (string.Equals(requiredRole, RoleNames.Manager, StringComparison.OrdinalIgnoreCase))
        {
            return PurchaseOrderStatus.PendingManager;
        }

        if (string.Equals(requiredRole, RoleNames.HeadOfFinance, StringComparison.OrdinalIgnoreCase))
        {
            return PurchaseOrderStatus.PendingFinance;
        }

        if (string.Equals(requiredRole, RoleNames.Ceo, StringComparison.OrdinalIgnoreCase))
        {
            return PurchaseOrderStatus.PendingCeo;
        }

        throw new BusinessRuleViolationException("Workflow step role is not supported for purchase orders.");
    }
}
