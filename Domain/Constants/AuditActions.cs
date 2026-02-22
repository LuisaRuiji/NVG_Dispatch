namespace NVGInventory.Domain.Constants;

public static class AuditActions
{
    public const string RequestCreated = "REQUEST_CREATED";
    public const string RequestSubmitted = "REQUEST_SUBMITTED";
    public const string RequestIoReviewApproved = "REQUEST_IO_REVIEW_APPROVED";
    public const string RequestIoReviewRejected = "REQUEST_IO_REVIEW_REJECTED";
    public const string RequestManagerApproved = "REQUEST_MANAGER_APPROVED";
    public const string RequestManagerRejected = "REQUEST_MANAGER_REJECTED";
    public const string RequestIssued = "REQUEST_ISSUED";

    public const string LoanReturn = "LOAN_RETURN";

    public const string PurchaseOrderDraftCreated = "PO_DRAFT_CREATED";
    public const string PurchaseOrderSubmitted = "PO_SUBMITTED";
    public const string PurchaseOrderApproved = "PO_APPROVED";
    public const string PurchaseOrderRejected = "PO_REJECTED";
    public const string PurchaseOrderReceived = "PO_RECEIVED";

    public const string InventoryAdjustmentDraftCreated = "ADJUSTMENT_DRAFT_CREATED";
    public const string InventoryAdjustmentSubmitted = "ADJUSTMENT_SUBMITTED";
    public const string InventoryAdjustmentApproved = "ADJUSTMENT_MANAGER_APPROVED";
    public const string InventoryAdjustmentRejected = "ADJUSTMENT_MANAGER_REJECTED";

    public const string SupplierCreated = "SUPPLIER_CREATED";
    public const string SupplierDeactivated = "SUPPLIER_DEACTIVATED";
}
