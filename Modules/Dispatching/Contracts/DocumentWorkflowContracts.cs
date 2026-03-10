using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Contracts;

public sealed record UploadTripDocumentCommand(
    Guid TripId,
    TripDocumentType Type,
    string StorageKey);

public sealed record VerifyTripDocumentCommand(Guid TripId, Guid DocumentId);

public sealed record RejectTripDocumentCommand(Guid TripId, Guid DocumentId, string Remarks);
