using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Enums;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed record CreateKitComponentCommand(
    Guid InventoryId,
    string Name,
    decimal RequiredQty,
    bool IsRequired,
    string? Notes);

public sealed record UpdateKitComponentCommand(
    Guid InventoryId,
    Guid ComponentId,
    string Name,
    decimal RequiredQty,
    bool IsRequired,
    string? Notes);

public enum KitComponentImportMode
{
    Replace,
    Merge
}

public sealed record KitComponentImportLine(
    string Name,
    decimal RequiredQty,
    bool IsRequired,
    string? Notes);

public sealed record ImportKitComponentsCommand(
    Guid InventoryId,
    KitComponentImportMode Mode,
    IReadOnlyCollection<KitComponentImportLine> Lines);

public sealed record ImportKitComponentsResult(
    int Added,
    int Updated,
    int Removed);

public sealed class KitComponentService
{
    private readonly InventoryDbContext _dbContext;

    public KitComponentService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<KitComponent>> GetComponentsAsync(
        Guid inventoryId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.InventoryItems
            .AnyAsync(item => item.Id == inventoryId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Inventory item not found.");
        }

        return await _dbContext.KitComponents
            .AsNoTracking()
            .Where(component => component.InventoryItemId == inventoryId)
            .OrderBy(component => component.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<KitComponent> CreateComponentAsync(
        CreateKitComponentCommand command,
        CancellationToken cancellationToken = default)
    {
        Validate(command.Name, command.RequiredQty);

        var item = await _dbContext.InventoryItems
            .Include(i => i.KitComponents)
            .FirstOrDefaultAsync(i => i.Id == command.InventoryId, cancellationToken);

        if (item is null)
        {
            throw new NotFoundException("Inventory item not found.");
        }

        if (item.ItemType != ItemType.NonConsumable)
        {
            throw new BusinessRuleViolationException("Kit components are only allowed for non-consumable items.");
        }

        var normalizedName = command.Name.Trim();
        var duplicate = item.KitComponents.Any(component =>
            string.Equals(component.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            throw new BusinessRuleViolationException("Component already exists for this kit.");
        }

        var now = DateTime.UtcNow;
        var component = new KitComponent
        {
            Id = Guid.NewGuid(),
            InventoryItemId = item.Id,
            Name = normalizedName,
            RequiredQty = command.RequiredQty,
            IsRequired = command.IsRequired,
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            CreatedAt = now
        };

        _dbContext.KitComponents.Add(component);

        if (!item.IsKit)
        {
            item.IsKit = true;
            item.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return component;
    }

    public async Task<KitComponent> UpdateComponentAsync(
        UpdateKitComponentCommand command,
        CancellationToken cancellationToken = default)
    {
        Validate(command.Name, command.RequiredQty);

        var component = await _dbContext.KitComponents
            .Include(c => c.InventoryItem)
            .FirstOrDefaultAsync(
                c => c.Id == command.ComponentId && c.InventoryItemId == command.InventoryId,
                cancellationToken);

        if (component is null)
        {
            throw new NotFoundException("Kit component not found.");
        }

        if (component.InventoryItem?.ItemType != ItemType.NonConsumable)
        {
            throw new BusinessRuleViolationException("Kit components are only allowed for non-consumable items.");
        }

        var normalizedName = command.Name.Trim();
        var duplicate = await _dbContext.KitComponents
            .AsNoTracking()
            .AnyAsync(
                c => c.InventoryItemId == command.InventoryId
                     && c.Id != command.ComponentId
                     && c.Name == normalizedName,
                cancellationToken);
        if (duplicate)
        {
            throw new BusinessRuleViolationException("Component already exists for this kit.");
        }

        component.Name = normalizedName;
        component.RequiredQty = command.RequiredQty;
        component.IsRequired = command.IsRequired;
        component.Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return component;
    }

    public async Task DeleteComponentAsync(
        Guid inventoryId,
        Guid componentId,
        CancellationToken cancellationToken = default)
    {
        var component = await _dbContext.KitComponents
            .FirstOrDefaultAsync(
                c => c.Id == componentId && c.InventoryItemId == inventoryId,
                cancellationToken);

        if (component is null)
        {
            throw new NotFoundException("Kit component not found.");
        }

        _dbContext.KitComponents.Remove(component);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ImportKitComponentsResult> ImportComponentsAsync(
        ImportKitComponentsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Lines.Count == 0)
        {
            throw new BusinessRuleViolationException("At least one component is required.");
        }

        var normalized = new List<KitComponentImportLine>(command.Lines.Count);
        var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in command.Lines)
        {
            Validate(line.Name, line.RequiredQty, line.Notes);
            var name = line.Name.Trim();
            if (!nameSet.Add(name))
            {
                throw new BusinessRuleViolationException($"Duplicate component '{name}' in import payload.");
            }

            normalized.Add(new KitComponentImportLine(
                name,
                line.RequiredQty,
                line.IsRequired,
                string.IsNullOrWhiteSpace(line.Notes) ? null : line.Notes.Trim()));
        }

        var item = await _dbContext.InventoryItems
            .Include(i => i.KitComponents)
            .FirstOrDefaultAsync(i => i.Id == command.InventoryId, cancellationToken);

        if (item is null)
        {
            throw new NotFoundException("Inventory item not found.");
        }

        if (item.ItemType != ItemType.NonConsumable)
        {
            throw new BusinessRuleViolationException("Kit components are only allowed for non-consumable items.");
        }

        var now = DateTime.UtcNow;
        int added = 0;
        int updated = 0;
        int removed = 0;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existingByName = item.KitComponents
            .ToDictionary(component => component.Name, StringComparer.OrdinalIgnoreCase);

        if (command.Mode == KitComponentImportMode.Replace)
        {
            removed = item.KitComponents.Count;
            _dbContext.KitComponents.RemoveRange(item.KitComponents);

            foreach (var line in normalized)
            {
                var component = new KitComponent
                {
                    Id = Guid.NewGuid(),
                    InventoryItemId = item.Id,
                    Name = line.Name,
                    RequiredQty = line.RequiredQty,
                    IsRequired = line.IsRequired,
                    Notes = line.Notes,
                    CreatedAt = now
                };
                _dbContext.KitComponents.Add(component);
                added++;
            }
        }
        else
        {
            foreach (var line in normalized)
            {
                if (existingByName.TryGetValue(line.Name, out var component))
                {
                    component.RequiredQty = line.RequiredQty;
                    component.IsRequired = line.IsRequired;
                    component.Notes = line.Notes;
                    updated++;
                }
                else
                {
                    var newComponent = new KitComponent
                    {
                        Id = Guid.NewGuid(),
                        InventoryItemId = item.Id,
                        Name = line.Name,
                        RequiredQty = line.RequiredQty,
                        IsRequired = line.IsRequired,
                        Notes = line.Notes,
                        CreatedAt = now
                    };
                    _dbContext.KitComponents.Add(newComponent);
                    added++;
                }
            }
        }

        if (!item.IsKit)
        {
            item.IsKit = true;
            item.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ImportKitComponentsResult(added, updated, removed);
    }

    private static void Validate(string name, decimal requiredQty, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleViolationException("Component name is required.");
        }

        if (name.Trim().Length > 200)
        {
            throw new BusinessRuleViolationException("Component name must be 200 characters or fewer.");
        }

        if (requiredQty <= 0)
        {
            throw new BusinessRuleViolationException("Component quantity must be greater than zero.");
        }

        if (!string.IsNullOrWhiteSpace(notes) && notes.Trim().Length > 250)
        {
            throw new BusinessRuleViolationException("Component notes must be 250 characters or fewer.");
        }
    }
}
