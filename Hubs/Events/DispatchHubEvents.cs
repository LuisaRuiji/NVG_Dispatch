namespace NVGInventory.Hubs.Events;

public sealed record TripStatusChangedEvent(
    Guid TripId,
    string ContainerNumber,
    string NewStatus,
    string PreviousStatus,
    string DriverName,
    DateTime ChangedAt);

public sealed record DocumentUploadedEvent(
    Guid TripId,
    string ContainerNumber,
    string DocumentType,
    string UploaderName,
    DateTime UploadedAt);

public sealed record DocumentVerifiedEvent(
    Guid TripId,
    string ContainerNumber,
    string DocumentType,
    bool IsVerified,
    string VerifiedBy,
    DateTime VerifiedAt);

public sealed record RecommendationGeneratedEvent(
    Guid CompletedTripId,
    string DriverName,
    string TruckPlate,
    string DeliveredAt,
    int RecommendationCount);

public sealed record ShipmentRequestSubmittedEvent(
    Guid RequestId,
    string CustomerName,
    string PickupLocation,
    string DropoffLocation,
    DateTime SubmittedAt);
