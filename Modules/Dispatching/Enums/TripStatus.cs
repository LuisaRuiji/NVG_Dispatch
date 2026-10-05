namespace NVGInventory.Modules.Dispatching.Enums;

public enum TripStatus
{
    Draft = 0,
    Planning = 1,
    Assigned = 2,
    ReadyForDispatch = 3,
    Dispatched = 4,
    EnroutePickup = 5,
    AtPickup = 6,
    Loaded = 7,
    EnrouteDropoff = 8,
    AtDropoff = 9,
    DeliveryCompleted = 10,

    // Compatibility aliases for records and clients created before the controlled
    // lifecycle migration. New writes use DELIVERY_COMPLETED/OPERATIONALLY_CLOSED.
    Delivered = DeliveryCompleted,
    DocumentsPending = 11,
    OperationallyClosed = 12,
    Closed = OperationallyClosed,
    Cancelled = 13,
    OnHold = 14,
    FailedAttempt = 15
}
