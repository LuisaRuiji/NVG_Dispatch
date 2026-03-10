using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;
using NVGInventory.Domain.Exceptions;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed class PortalCustomerAccessService : IPortalCustomerAccessService
{
    private readonly InventoryDbContext _dbContext;

    public PortalCustomerAccessService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> GetRequiredPortalCustomerIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var customerId = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!customerId.HasValue)
        {
            throw new ForbiddenDomainException("Customer access denied.");
        }

        return customerId.Value;
    }
}
