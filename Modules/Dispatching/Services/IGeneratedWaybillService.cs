using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Services;

public interface IGeneratedWaybillService
{
    Task<GeneratedWaybill> GenerateAsync(
        Guid tripId,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);

    Task<GeneratedWaybill?> GetActiveAsync(
        Guid tripId,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);
}
