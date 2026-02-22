using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed class StockLedgerService
{
    private readonly InventoryDbContext _dbContext;
    private readonly Action? _beforeCommitHook;

    public StockLedgerService(InventoryDbContext dbContext, Action? beforeCommitHook = null)
    {
        _dbContext = dbContext;
        _beforeCommitHook = beforeCommitHook;
    }

    public Task ApplyMovement(
        StockMovementType movementType,
        Guid inventoryId,
        decimal qtyDelta,
        string refType,
        Guid refId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        return ApplyMovementInternal(
            movementType,
            inventoryId,
            qtyDelta,
            refType,
            refId,
            actorId,
            null,
            cancellationToken);
    }

    public Task ApplyMovement(
        StockMovementType movementType,
        Guid inventoryId,
        decimal qtyDelta,
        string refType,
        Guid refId,
        Guid actorId,
        decimal? unitCost,
        CancellationToken cancellationToken = default)
    {
        return ApplyMovementInternal(
            movementType,
            inventoryId,
            qtyDelta,
            refType,
            refId,
            actorId,
            unitCost,
            cancellationToken);
    }

    private async Task ApplyMovementInternal(
        StockMovementType movementType,
        Guid inventoryId,
        decimal qtyDelta,
        string refType,
        Guid refId,
        Guid actorId,
        decimal? unitCost,
        CancellationToken cancellationToken)
    {
        if (qtyDelta == 0)
        {
            throw new BusinessRuleViolationException("Stock movement quantity must be non-zero.");
        }

        if (string.IsNullOrWhiteSpace(refType))
        {
            throw new BusinessRuleViolationException("Stock movement requires a reference type.");
        }

        if (movementType == StockMovementType.WriteOff && qtyDelta > 0)
        {
            throw new BusinessRuleViolationException("Write-off quantity must be negative.");
        }

        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var inventory = await _dbContext.InventoryItems
                .FromSqlRaw(
                    "SELECT * FROM dbo.inventory WITH (UPDLOCK, ROWLOCK) WHERE id = {0}",
                    inventoryId)
                .FirstOrDefaultAsync(cancellationToken);

            if (inventory is null)
            {
                throw new NotFoundException("Inventory item not found for stock movement.");
            }

            var existingQuantity = inventory.Quantity;
            var affectsQuantity = movementType != StockMovementType.WriteOff;
            var newQuantity = existingQuantity;
            if (affectsQuantity)
            {
                newQuantity = existingQuantity + qtyDelta;
                if (newQuantity < 0)
                {
                    throw new BusinessRuleViolationException("Stock movement would result in negative quantity.");
                }
            }

            var now = DateTime.UtcNow;
            if (refType == EntityTypes.Request && (movementType == StockMovementType.Out || movementType == StockMovementType.Borrow))
            {
                var existing = await _dbContext.StockLogs.AnyAsync(
                    log => log.RefType == refType
                           && log.RefId == refId
                           && log.InventoryId == inventoryId
                           && log.MovementType == movementType,
                    cancellationToken);

                if (existing)
                {
                    throw new BusinessRuleViolationException("Stock movement already recorded for this reference.");
                }
            }

            if (movementType == StockMovementType.In
                && unitCost.HasValue
                && string.Equals(refType, EntityTypes.PurchaseOrder, StringComparison.OrdinalIgnoreCase))
            {
                if (qtyDelta <= 0)
                {
                    throw new BusinessRuleViolationException("Received quantity must be greater than zero.");
                }

                if (existingQuantity == 0m)
                {
                    inventory.AverageCost = unitCost.Value;
                }
                else
                {
                    var totalCost = (existingQuantity * inventory.AverageCost) + (qtyDelta * unitCost.Value);
                    inventory.AverageCost = totalCost / (existingQuantity + qtyDelta);
                }

                inventory.LastCost = unitCost.Value;
            }

            decimal? logUnitCost = null;
            decimal? logTotalCost = null;
            if (movementType == StockMovementType.Out
                || movementType == StockMovementType.Borrow
                || movementType == StockMovementType.Adjustment
                || movementType == StockMovementType.WriteOff)
            {
                logUnitCost = inventory.AverageCost;
                logTotalCost = Math.Abs(qtyDelta) * logUnitCost;
            }
            else if (movementType == StockMovementType.In && unitCost.HasValue)
            {
                logUnitCost = unitCost.Value;
                logTotalCost = Math.Abs(qtyDelta) * logUnitCost;
            }

            var log = new StockLog
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                MovementType = movementType,
                QtyDelta = qtyDelta,
                UnitCostSnapshot = logUnitCost,
                TotalCostSnapshot = logTotalCost,
                RefType = refType,
                RefId = refId,
                ActorUserId = actorId,
                MetaJson = null,
                CreatedAt = now
            };

            if (affectsQuantity)
            {
                inventory.Quantity = newQuantity;
            }

            inventory.UpdatedAt = now;
            _dbContext.StockLogs.Add(log);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _beforeCommitHook?.Invoke();

            if (ownsTransaction && transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (ownsTransaction && transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }
}
