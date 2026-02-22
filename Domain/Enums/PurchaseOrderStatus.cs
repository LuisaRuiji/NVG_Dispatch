namespace NVGInventory.Domain.Enums;

public enum PurchaseOrderStatus
{
    Draft,
    PendingManager,
    PendingFinance,
    PendingCeo,
    Approved,
    Rejected,
    PartiallyReceived,
    Closed
}
