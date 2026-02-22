using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Enums;

namespace NVGInventory.Domain.Services;

public sealed record PurchaseOrderListItem(
    Guid Id,
    PurchaseOrderStatus Status,
    Guid CreatedByUserId,
    string CreatedByUsername,
    Guid SupplierId,
    string SupplierName,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? ReceivedAt,
    DateTime? ClosedAt);

public sealed record PurchaseOrderLineDetail(
    Guid Id,
    Guid InventoryId,
    string InventoryName,
    string Unit,
    ItemType ItemType,
    decimal QtyOrdered,
    decimal QtyReceived,
    decimal? UnitPrice,
    string? Remarks);

public sealed record PurchaseOrderReceiptDetail(
    Guid Id,
    Guid PurchaseOrderLineId,
    decimal QtyReceivedIncrement,
    Guid ReceivedByUserId,
    string? ReceivedByUsername,
    DateTime ReceivedAt);

public sealed record PurchaseOrderDetail(
    Guid Id,
    PurchaseOrderStatus Status,
    Guid CreatedByUserId,
    string CreatedByUsername,
    Guid SupplierId,
    string SupplierName,
    string? Notes,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt,
    string? RejectionReason,
    DateTime? ReceivedAt,
    DateTime? ClosedAt,
    IReadOnlyCollection<PurchaseOrderLineDetail> Lines,
    IReadOnlyCollection<PurchaseOrderReceiptDetail> Receipts,
    ApprovalSummary? Approval,
    IReadOnlyCollection<ApprovalActionSummary> ApprovalActions);

public sealed class PurchaseOrderQueryService
{
    private readonly InventoryDbContext _dbContext;

    public PurchaseOrderQueryService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedQueryResult<PurchaseOrderListItem>> GetPurchaseOrdersAsync(
        IReadOnlyCollection<PurchaseOrderStatus>? statuses,
        Guid? createdByUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.CreatedBy)
            .Include(po => po.Supplier)
            .AsQueryable();

        if (statuses is { Count: > 0 })
        {
            query = query.Where(po => statuses.Contains(po.Status));
        }

        if (createdByUserId.HasValue)
        {
            query = query.Where(po => po.CreatedByUserId == createdByUserId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(po => po.SubmittedAt ?? po.CreatedAt)
            .ThenByDescending(po => po.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(po => new PurchaseOrderListItem(
                po.Id,
                po.Status,
                po.CreatedByUserId,
                po.CreatedBy != null ? po.CreatedBy.Username : string.Empty,
                po.SupplierId,
                po.Supplier != null ? po.Supplier.Name : string.Empty,
                po.CreatedAt,
                po.SubmittedAt,
                po.ApprovedAt,
                po.ReceivedAt,
                po.ClosedAt))
            .ToListAsync(cancellationToken);

        return new PagedQueryResult<PurchaseOrderListItem>(items, totalCount);
    }

    public async Task<PurchaseOrderDetail?> GetPurchaseOrderDetailAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var po = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(order => order.CreatedBy)
            .Include(order => order.Supplier)
            .Include(order => order.Lines)
            .ThenInclude(line => line.InventoryItem)
            .Include(order => order.Lines)
            .ThenInclude(line => line.Receipts)
            .ThenInclude(receipt => receipt.ReceivedBy)
            .FirstOrDefaultAsync(order => order.Id == purchaseOrderId, cancellationToken);

        if (po is null)
        {
            return null;
        }

        ApprovalSummary? approvalSummary = null;
        List<ApprovalActionSummary> approvalActions = new();

        var approval = await _dbContext.Approvals
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.EntityType == EntityTypes.PurchaseOrder && a.EntityId == po.Id,
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

        var lines = po.Lines
            .Select(line => new PurchaseOrderLineDetail(
                line.Id,
                line.InventoryId,
                line.InventoryItem?.Name ?? string.Empty,
                line.InventoryItem?.Unit ?? string.Empty,
                line.InventoryItem?.ItemType ?? ItemType.Consumable,
                line.QtyOrdered,
                line.QtyReceived,
                line.UnitPrice,
                line.Remarks))
            .ToList();

        var receipts = po.Lines
            .SelectMany(line => line.Receipts.Select(receipt => new PurchaseOrderReceiptDetail(
                receipt.Id,
                receipt.PurchaseOrderLineId,
                receipt.QtyReceivedIncrement,
                receipt.ReceivedByUserId,
                receipt.ReceivedBy?.Username,
                receipt.ReceivedAt)))
            .OrderBy(entry => entry.ReceivedAt)
            .ToList();

        return new PurchaseOrderDetail(
            po.Id,
            po.Status,
            po.CreatedByUserId,
            po.CreatedBy?.Username ?? string.Empty,
            po.SupplierId,
            po.Supplier?.Name ?? string.Empty,
            po.Notes,
            po.CreatedAt,
            po.SubmittedAt,
            po.ApprovedAt,
            po.RejectedAt,
            po.RejectionReason,
            po.ReceivedAt,
            po.ClosedAt,
            lines,
            receipts,
            approvalSummary,
            approvalActions);
    }
}
