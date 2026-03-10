namespace NVGInventory.Modules.Dispatching.Services;

public interface IDispatchDocumentReadService
{
    Task<bool> HasVerifiedPodAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<bool> HasUploadedOrVerifiedPodAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
}
