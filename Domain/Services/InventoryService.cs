using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed record CreateInventoryItemCommand(
    string Name,
    string Unit,
    Enums.ItemType ItemType,
    decimal Quantity,
    decimal? ReorderLevel,
    string? Location,
    decimal? UnitValue);

public sealed class InventoryService
{
    private readonly InventoryDbContext _dbContext;

    public InventoryService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InventoryItem> CreateItemAsync(
        CreateInventoryItemCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new BusinessRuleViolationException("Item name is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Unit))
        {
            throw new BusinessRuleViolationException("Item unit is required.");
        }

        if (command.Quantity < 0)
        {
            throw new BusinessRuleViolationException("Quantity cannot be negative.");
        }

        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = command.Name.Trim(),
            Unit = command.Unit.Trim(),
            ItemType = command.ItemType,
            Quantity = command.Quantity,
            ReorderLevel = command.ReorderLevel,
            Location = string.IsNullOrWhiteSpace(command.Location) ? null : command.Location.Trim(),
            UnitValue = command.UnitValue,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.InventoryItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return item;
    }

    public async Task<IReadOnlyCollection<InventoryItem>> GetItemsAsync(
        Enums.ItemType? itemType,
        bool? lowStock,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.InventoryItems.AsNoTracking().Where(item => item.IsActive);

        if (itemType.HasValue)
        {
            query = query.Where(item => item.ItemType == itemType.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Name.Contains(term));
        }

        if (lowStock == true)
        {
            query = query.Where(item => item.ReorderLevel.HasValue && item.Quantity <= item.ReorderLevel.Value);
        }

        return await query
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
    }
}
