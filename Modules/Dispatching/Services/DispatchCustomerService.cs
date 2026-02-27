using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Services;

public sealed record DispatchCustomerSummary(Guid Id, string Name);

public sealed class DispatchCustomerService
{
    private readonly InventoryDbContext _dbContext;

    public DispatchCustomerService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<DispatchCustomerSummary>> GetCustomersAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DispatchCustomers
            .AsNoTracking()
            .OrderBy(customer => customer.Name)
            .Select(customer => new DispatchCustomerSummary(customer.Id, customer.Name))
            .ToListAsync(cancellationToken);
    }
}

