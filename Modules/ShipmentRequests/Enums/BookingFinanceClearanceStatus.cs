namespace NVGInventory.Modules.ShipmentRequests.Enums;

public enum BookingFinanceClearanceStatus
{
    NotRequested = 0,
    AwaitingPayment = 1,
    AwaitingCreditReview = 2,
    Cleared = 3,
    Blocked = 4,
    AuthorizedException = 5
}
