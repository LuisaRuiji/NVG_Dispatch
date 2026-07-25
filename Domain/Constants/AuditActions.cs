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

    public const string UserCreated = "USER_CREATED";
    public const string UserProfileUpdated = "USER_PROFILE_UPDATED";
    public const string UserRoleAssigned = "USER_ROLE_ASSIGNED";
    public const string UserRolesReplaced = "USER_ROLES_REPLACED";
    public const string UserStatusUpdated = "USER_STATUS_UPDATED";
    public const string UserPasswordReset = "USER_PASSWORD_RESET";
    public const string UserPasswordChanged = "USER_PASSWORD_CHANGED";
    public const string UserDeactivated = "USER_DEACTIVATED";

    public const string DispatchTripCreated = "DISPATCH_TRIP_CREATED";
    public const string DispatchTripUpdated = "DISPATCH_TRIP_UPDATED";
    public const string DispatchTripDispatched = "DISPATCH_TRIP_DISPATCHED";
    public const string DispatchTripStatusChanged = "DISPATCH_TRIP_STATUS_CHANGED";
    public const string DispatchTripStatusCorrected = "DISPATCH_TRIP_STATUS_CORRECTED";
    public const string DispatchTripCancelled = "DISPATCH_TRIP_CANCELLED";
    public const string DispatchTripOnHold = "DISPATCH_TRIP_ON_HOLD";
    public const string DispatchTripClosed = "DISPATCH_TRIP_CLOSED";
    public const string DispatchTripConflictOverride = "DISPATCH_TRIP_CONFLICT_OVERRIDE";
    public const string DispatchTripDocumentUploaded = "DISPATCH_TRIP_DOCUMENT_UPLOADED";
    public const string DispatchTripDocumentVerified = "DISPATCH_TRIP_DOCUMENT_VERIFIED";
    public const string DispatchTripDocumentRejected = "DISPATCH_TRIP_DOCUMENT_REJECTED";
    public const string RecommendationAccepted = "RECOMMENDATION_ACCEPTED";
    public const string RecommendationIgnored = "RECOMMENDATION_IGNORED";
    public const string WaybillGenerated = "WAYBILL_GENERATED";
    public const string FinancialFieldAccessed = "FINANCIAL_FIELD_ACCESSED";

    public const string ModuleSettingUpdated = "MODULE_SETTING_UPDATED";
    public const string OptimizationWeightSettingsUpdated = "OPTIMIZATION_WEIGHT_SETTINGS_UPDATED";
    public const string OptimizationPlanApproved = "OPTIMIZATION_PLAN_APPROVED";
    public const string OptimizationPlanDispatched = "OPTIMIZATION_PLAN_DISPATCHED";
    public const string OptimizationPlanOverridden = "OPTIMIZATION_PLAN_OVERRIDDEN";
    public const string LocationTrackingStarted = "LOCATION_TRACKING_STARTED";
    public const string LocationTrackingStopped = "LOCATION_TRACKING_STOPPED";
}
