using NVGInventory.Modules.Dispatching.Enums;

namespace NVGInventory.Modules.Dispatching.Contracts;

public sealed record UploadTripDocumentCommand(
    Guid TripId,
    TripDocumentType Type,
    string StorageKey,
    string? OriginalFileName = null,
    string? ContentType = null,
    long? SizeBytes = null,
    string? ReferenceNumber = null,
    DateTime? ExpiryDate = null,
    string? Carrier = null,
    string? TerminalOrDepot = null,
    DocumentDirection Direction = DocumentDirection.NotApplicable,
    DateTime? DocumentEventAt = null,
    string? ContainerCondition = null,
    bool IsProofOfDelivery = false);

public sealed record VerifyTripDocumentCommand(Guid TripId, Guid DocumentId);

public sealed record RejectTripDocumentCommand(Guid TripId, Guid DocumentId, string Remarks);
