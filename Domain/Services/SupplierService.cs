using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Constants;
using NVGInventory.Domain.Entities;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Domain.Services;

public sealed class SupplierService
{
    private readonly InventoryDbContext _dbContext;
    private readonly IAuditService? _auditService;

    public SupplierService(InventoryDbContext dbContext, IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task<Supplier> CreateAsync(
        Guid actorUserId,
        string name,
        string? contactName,
        string? contactPhone,
        string? contactEmail,
        string? address,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleViolationException("Supplier name is required.");
        }

        var normalized = name.Trim();
        var exists = await _dbContext.Suppliers
            .AnyAsync(s => s.Name == normalized, cancellationToken);

        if (exists)
        {
            throw new BusinessRuleViolationException("Supplier already exists.");
        }

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = normalized,
            ContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim(),
            ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim(),
            ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim(),
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Suppliers.Add(supplier);
        _auditService?.AddEntry(
            actorUserId,
            AuditActions.SupplierCreated,
            EntityTypes.Supplier,
            supplier.Id,
            null,
            new { supplier.Name, supplier.IsActive });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return supplier;
    }

    public async Task<IReadOnlyCollection<Supplier>> GetSuppliersAsync(
        bool activeOnly,
        string? search,
        CancellationToken cancellationToken = default)
    {
        _ = activeOnly;
        var query = _dbContext.Suppliers
            .AsNoTracking()
            .Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.Name.Contains(term));
        }

        return await query
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);

        if (supplier is null)
        {
            throw new NotFoundException("Supplier not found.");
        }

        if (!supplier.IsActive)
        {
            return;
        }

        supplier.IsActive = false;
        _auditService?.AddEntry(
            actorUserId,
            AuditActions.SupplierDeactivated,
            EntityTypes.Supplier,
            supplier.Id,
            null,
            new { supplier.IsActive });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
