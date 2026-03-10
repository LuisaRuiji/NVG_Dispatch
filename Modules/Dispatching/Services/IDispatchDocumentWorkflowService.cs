using NVGInventory.Modules.Dispatching.Contracts;
using NVGInventory.Modules.Dispatching.Entities;

namespace NVGInventory.Modules.Dispatching.Services;

public interface IDispatchDocumentWorkflowService
{
    Task<TripDocument> UploadDocumentAsync(
        UploadTripDocumentCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);

    Task<TripDocument> VerifyDocumentAsync(
        VerifyTripDocumentCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);

    Task<TripDocument> RejectDocumentAsync(
        RejectTripDocumentCommand command,
        DispatchActorContext actor,
        CancellationToken cancellationToken = default);
}
