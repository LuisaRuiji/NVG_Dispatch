using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed record PurchaseOrderLineInput(Guid InventoryId, decimal Quantity, decimal? UnitPrice, string? Remarks);

public sealed record CreatePurchaseOrderDraftCommand(
    Guid CreatedByUserId,
    Guid SupplierId,
    string? Notes,
    IReadOnlyCollection<PurchaseOrderLineInput> Lines);

public sealed class PurchaseOrderService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService? _auditService;

    public PurchaseOrderService(InventoryDbContext dbContext, IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task<PurchaseOrder> CreateDraftAsync(
        CreatePurchaseOrderDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Lines.Count == 0)
        {
            throw new BusinessRuleViolationException("Purchase order must include at least one line.");
        }

        foreach (var line in command.Lines)
        {
            if (line.Quantity <= 0)
            {
                throw new BusinessRuleViolationException("Ordered quantity must be greater than zero.");
            }

            if (line.Quantity % 1m != 0m)
            {
                throw new BusinessRuleViolationException("Ordered quantity must be a whole number.");
            }

            if (line.UnitPrice.HasValue && line.UnitPrice.Value < 0)
            {
                throw new BusinessRuleViolationException("Unit price cannot be negative.");
            }
        }

        var duplicateInventory = command.Lines
            .GroupBy(line => line.InventoryId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateInventory.Count > 0)
        {
            throw new BusinessRuleViolationException("Duplicate inventory items are not allowed on the same purchase order.");
        }

        var creatorExists = await _dbContext.Users
            .AnyAsync(user => user.Id == command.CreatedByUserId && user.IsActive, cancellationToken);
        if (!creatorExists)
        {
            throw new NotFoundException("Creator not found.");
        }

        var supplierExists = await _dbContext.Suppliers
            .AnyAsync(supplier => supplier.Id == command.SupplierId && supplier.IsActive, cancellationToken);
        if (!supplierExists)
        {
            throw new NotFoundException("Supplier not found.");
        }

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
        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            SupplierId = command.SupplierId,
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            Status = PurchaseOrderStatus.Draft,
            CreatedByUserId = command.CreatedByUserId,
            CreatedAt = now,
            UpdatedAt = now,
            Lines = command.Lines.Select(line => new PurchaseOrderLine
            {
                Id = Guid.NewGuid(),
                InventoryId = line.InventoryId,
                QtyOrdered = line.Quantity,
                QtyReceived = 0m,
                UnitPrice = line.UnitPrice,
                Remarks = line.Remarks,
            }).ToList()
        };

        _dbContext.PurchaseOrders.Add(purchaseOrder);
        _auditService?.AddEntry(
            command.CreatedByUserId,
            AuditActions.PurchaseOrderDraftCreated,
            EntityTypes.PurchaseOrder,
            purchaseOrder.Id,
            null,
            new { purchaseOrder.Status, LineCount = purchaseOrder.Lines.Count });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return purchaseOrder;
    }
}
