namespace NVGInventory.Modules.Dispatching.Services;

using NVGInventory.Modules.Dispatching.Enums;

public interface IDispatchDocumentReadService
{
    Task<bool> HasVerifiedPodAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<bool> HasUploadedOrVerifiedPodAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<bool> HasVerifiedDocumentAsync(
        Guid tripId,
        TripDocumentType type,
        CancellationToken cancellationToken = default);
}
