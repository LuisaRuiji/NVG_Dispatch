namespace NVGInventory.Modules.ShipmentRequests.Enums;

public enum ShipmentRequestStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5,
    AwaitingFinanceClearance = 6,
    ClearedForPlanning = 7,
    ConvertedToTrip = 8,
    NeedsRevision = 9
}
