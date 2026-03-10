using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Services;

public interface ITripLifecycleService
{
    Task<Trip> CreateDraftAsync(
        CreateDispatchTripCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);

    Task<Trip> UpdateTripAsync(
        Guid tripId,
        UpdateDispatchTripCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);

    Task<Trip> DispatchAsync(
        DispatchTripCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);

    Task<Trip> ChangeStatusAsync(
        ChangeDispatchTripStatusCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);

    Task<Trip> CorrectStatusAsync(
        CorrectDispatchTripStatusCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);
}
