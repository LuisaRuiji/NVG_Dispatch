namespace NVGInventory.Modules.ShipmentRequests.Services;

public interface IPortalCustomerAccessService
{
    Task<Guid> GetRequiredPortalCustomerIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
