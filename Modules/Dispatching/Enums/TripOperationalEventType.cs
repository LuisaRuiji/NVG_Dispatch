namespace NVGInventory.Modules.Dispatching.Enums;

public enum TripOperationalEventType
{
    DriverAssigned = 0,
    TruckAssigned = 1,
    TrailerAssigned = 2,
    TripReleased = 3,
    Departed = 4,
    ArrivedAtPickup = 5,
    ArrivedAtDepot = 6,
    ArrivedAtTerminal = 7,
    GateIn = 8,
    GateOut = 9,
    ArrivedAtConsignee = 10,
    DeliveryCompleted = 11,
    EmptyContainerReturned = 12,
    DocumentsVerified = 13,
    OperationallyClosed = 14,
    Custom = 15
}
