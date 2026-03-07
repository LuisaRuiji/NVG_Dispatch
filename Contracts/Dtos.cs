using NVGInventory.Domain.Enums;
using NVGInventory.Modules.Dispatching.Enums;
using NVGInventory.Modules.ShipmentRequests.Enums;

namespace NVGInventory.Contracts;

public sealed record CreateUserRequest(string Username, string Password, string? Email);

public sealed record CreateUserResponse(Guid Id, string Username);

public sealed record AssignRoleRequest(string RoleName);

public sealed record UpdateUserRolesRequest(IReadOnlyCollection<string> RoleNames);

public sealed record UpdateUserStatusRequest(bool IsActive);

public sealed record ResetUserPasswordRequest(string NewPassword);

public sealed record UserSummaryResponse(
    Guid Id,
    string Username,
    string? Email,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyCollection<string> Roles);

public sealed record RoleSummaryResponse(string Name);

public sealed record ModuleSettingResponse(
    string ModuleKey,
    string DisplayName,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? UpdatedByUserId,
    string? UpdatedByUsername,
    string? Notes);

public sealed record UpdateModuleSettingRequest(bool IsEnabled, string? Notes);

public sealed record ModuleStatusResponse(
    string ModuleKey,
    string DisplayName,
    bool IsEnabled,
    string? Notes);

public sealed record ApiErrorResponse(string ErrorCode, string Message, string TraceId);

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string AccessToken,
    Guid UserId,
    IReadOnlyCollection<string> Roles);

public sealed record CurrentUserResponse(
    Guid UserId,
    string Username,
    IReadOnlyCollection<string> Roles);

public sealed record CreateSupplierRequest(
    string Name,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    string? Address);

public sealed record SupplierResponse(
    Guid Id,
    string Name,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    string? Address,
    bool IsActive);

public sealed record CreateInventoryItemRequest(
    string Name,
    string Unit,
    ItemType ItemType,
    decimal Quantity,
    decimal? ReorderLevel,
    string? Location,
    decimal? UnitValue,
    bool? IsKit = null);

public sealed record UpdateInventoryItemRequest(
    string Name,
    string Unit,
    ItemType ItemType,
    decimal? ReorderLevel,
    string? Location,
    decimal? UnitValue,
    bool IsKit);

public sealed record InventoryItemResponse(
    Guid Id,
    string Name,
    string Unit,
    ItemType ItemType,
    bool IsKit,
    decimal Quantity,
    decimal? ReorderLevel,
    string? Location,
    decimal? UnitValue);

public sealed record UpdateInventoryKitRequest(bool IsKit);

public sealed record KitComponentResponse(
    Guid Id,
    Guid InventoryId,
    string Name,
    decimal RequiredQty,
    bool IsRequired,
    string? Notes,
    DateTime CreatedAt);

public sealed record CreateKitComponentRequest(
    string Name,
    decimal RequiredQty,
    bool IsRequired,
    string? Notes);

public sealed record UpdateKitComponentRequest(
    string Name,
    decimal RequiredQty,
    bool IsRequired,
    string? Notes);

public sealed record KitComponentImportLineRequest(
    string Name,
    decimal RequiredQty,
    bool IsRequired,
    string? Notes);

public sealed record ImportKitComponentsRequest(
    string Mode,
    IReadOnlyCollection<KitComponentImportLineRequest> Lines);

public sealed record ImportKitComponentsResponse(
    int Added,
    int Updated,
    int Removed);

public sealed record CreateAssetRequest(
    string AssetCode,
    AssetType AssetType,
    string? PlateNo,
    AssetStatus Status = AssetStatus.Active);

public sealed record AssetResponse(
    Guid Id,
    string AssetCode,
    AssetType AssetType,
    AssetStatus Status,
    string? PlateNo);

public sealed record RequestLineInputDto(Guid InventoryId, decimal Quantity, string? Remarks);

public sealed record PurchaseOrderLineInputDto(Guid InventoryId, decimal Quantity, decimal? UnitPrice, string? Remarks);

public sealed record CreatePurchaseOrderDraftRequest(
    Guid SupplierId,
    string? Notes,
    IReadOnlyCollection<PurchaseOrderLineInputDto> Lines);

public sealed record CreatePurchaseOrderDraftResponse(Guid PurchaseOrderId, PurchaseOrderStatus Status);

public sealed record SubmitPurchaseOrderResponse(Guid PurchaseOrderId, PurchaseOrderStatus Status);

public sealed record PurchaseOrderDecisionRequest(ApprovalDecision Decision, string? Remarks);

public sealed record PurchaseOrderDecisionResponse(
    Guid PurchaseOrderId,
    PurchaseOrderStatus Status,
    ApprovalStatus ApprovalStatus);

public sealed record PurchaseOrderReceiveLineDto(Guid PurchaseOrderLineId, decimal QtyReceived, string? Remarks);

public sealed record PurchaseOrderReceiveRequest(
    IReadOnlyCollection<PurchaseOrderReceiveLineDto> Lines,
    string? Remarks);

public sealed record PurchaseOrderReceiveResponse(Guid PurchaseOrderId, PurchaseOrderStatus Status);

public sealed record PurchaseOrderListItemResponse(
    Guid Id,
    PurchaseOrderStatus Status,
    Guid CreatedByUserId,
    string CreatedByUsername,
    Guid SupplierId,
    string SupplierName,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? ReceivedAt,
    DateTime? ClosedAt);

public sealed record PurchaseOrderLineDetailResponse(
    Guid Id,
    Guid InventoryId,
    string InventoryName,
    string Unit,
    ItemType ItemType,
    decimal QtyOrdered,
    decimal QtyReceived,
    decimal? UnitPrice,
    string? Remarks);

public sealed record PurchaseOrderReceiptResponse(
    Guid Id,
    Guid PurchaseOrderLineId,
    decimal QtyReceivedIncrement,
    Guid ReceivedByUserId,
    string? ReceivedByUsername,
    DateTime ReceivedAt);

public sealed record PurchaseOrderDetailResponse(
    Guid Id,
    PurchaseOrderStatus Status,
    Guid CreatedByUserId,
    string CreatedByUsername,
    Guid SupplierId,
    string SupplierName,
    string? Notes,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt,
    string? RejectionReason,
    DateTime? ReceivedAt,
    DateTime? ClosedAt,
    IReadOnlyCollection<PurchaseOrderLineDetailResponse> Lines,
    IReadOnlyCollection<PurchaseOrderReceiptResponse> Receipts,
    ApprovalSummaryResponse? Approval,
    IReadOnlyCollection<ApprovalActionSummaryResponse> ApprovalActions);

public sealed record SubmitMaintenanceIssueRequest(
    Guid AssetId,
    string? Purpose,
    IReadOnlyCollection<RequestLineInputDto> Lines);

public sealed record SubmitMaintenanceIssueResponse(
    Guid RequestId,
    Guid ApprovalId,
    RequestStatus Status);

public sealed record CreateInventoryAdjustmentRequest(
    string Reason,
    IReadOnlyCollection<RequestLineInputDto> Lines);

public sealed record CreateInventoryAdjustmentResponse(
    Guid RequestId,
    Guid ApprovalId,
    RequestStatus Status);

public sealed record ApprovalActionRequest(
    ApprovalDecision Decision,
    string? Remarks);

public sealed record ApprovalActionResponse(
    Guid ApprovalId,
    ApprovalStatus Status,
    int CurrentStep);

public sealed record IssueRequestResponse(Guid RequestId, RequestStatus Status);

public sealed record SubmitRequestResponse(Guid RequestId, RequestStatus Status);

public sealed record ApproveAsManagerResponse(Guid RequestId, RequestStatus Status);

public sealed record InventoryOfficerReviewLine(Guid RequestLineId, decimal QtyApproved, string? Remarks);

public sealed record InventoryOfficerReviewRequest(
    ApprovalDecision? Decision,
    string? Remarks,
    string? IoRemarks,
    IReadOnlyCollection<InventoryOfficerReviewLine> Lines);

public sealed record InventoryOfficerReviewResponse(Guid RequestId, RequestStatus Status);

public sealed record ManagerDecisionRequest(ApprovalDecision Decision, string? Remarks);

public sealed record CreateRequestDraftRequest(
    RequestType RequestType,
    Guid? AssetId,
    string? Purpose,
    IReadOnlyCollection<RequestLineInputDto> Lines);

public sealed record CreateRequestDraftResponse(Guid RequestId, RequestStatus Status);

public sealed record RequestListItemResponse(
    Guid Id,
    RequestType RequestType,
    RequestStatus Status,
    Guid RequesterUserId,
    string RequesterUsername,
    Guid? AssetId,
    string? AssetCode,
    string? Purpose,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? IssuedAt,
    DateTime? ClosedAt);

public sealed record RequestLineDetailResponse(
    Guid Id,
    Guid InventoryId,
    string InventoryName,
    string Unit,
    ItemType ItemType,
    decimal CurrentQuantity,
    decimal QtyRequested,
    decimal? QtyApproved,
    string? Remarks);

public sealed record ApprovalSummaryResponse(
    Guid ApprovalId,
    ApprovalStatus Status,
    int CurrentStep,
    string? NextApproverRole,
    string WorkflowKey);

public sealed record RequestDetailResponse(
    Guid Id,
    RequestType RequestType,
    RequestStatus Status,
    Guid RequesterUserId,
    string RequesterUsername,
    Guid? AssetId,
    string? AssetCode,
    string? Purpose,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? IssuedAt,
    DateTime? ClosedAt,
    Guid? LoanId,
    IReadOnlyCollection<RequestLineDetailResponse> Lines,
    ApprovalSummaryResponse? Approval,
    IReadOnlyCollection<ApprovalActionSummaryResponse> ApprovalActions,
    IReadOnlyCollection<StockLogSummaryResponse> StockLogs,
    IReadOnlyCollection<HistoryEntryResponse> History);

public sealed record LoanReturnLineDto(
    Guid LoanLineId,
    decimal QtyReturnedIncrement,
    ReturnCondition Condition,
    string? MissingComponentsJson);

public sealed record LoanReturnRequest(IReadOnlyCollection<LoanReturnLineDto> Lines);

public sealed record LoanReturnResponse(Guid LoanId, LoanStatus Status);

public sealed record LoanListItemResponse(
    Guid Id,
    Guid RequestId,
    LoanStatus Status,
    Guid BorrowerUserId,
    string BorrowerUsername,
    Guid? AssetId,
    string? AssetCode,
    DateTime IssuedAt,
    DateTime? DueAt,
    DateTime? ClosedAt);

public sealed record LoanLineReturnResponse(
    Guid Id,
    decimal QtyReturned,
    ReturnCondition Condition,
    string? MissingComponentsJson,
    Guid ReceivedByUserId,
    string? ReceivedByUsername,
    DateTime ReturnedAt);

public sealed record LoanLineDetailResponse(
    Guid Id,
    Guid InventoryId,
    string InventoryName,
    string Unit,
    ItemType ItemType,
    decimal QtyIssued,
    decimal QtyReturned,
    IReadOnlyCollection<LoanLineReturnResponse> Returns);

public sealed record ApprovalActionSummaryResponse(
    Guid Id,
    ApprovalDecision Decision,
    string? Remarks,
    Guid ActorUserId,
    string? ActorUsername,
    DateTime CreatedAt,
    int StepOrder);

public sealed record StockLogSummaryResponse(
    Guid Id,
    StockMovementType MovementType,
    decimal Quantity,
    Guid ActorUserId,
    string? ActorUsername,
    DateTime CreatedAt);

public sealed record HistoryEntryResponse(
    string Type,
    string? Actor,
    string? Decision,
    int? Step,
    string? MovementType,
    decimal? Qty,
    string? Condition,
    DateTime Timestamp);

public sealed record StockMovementReportItemResponse(
    Guid InventoryId,
    string InventoryName,
    decimal TotalIn,
    decimal TotalOut,
    decimal TotalBorrow,
    decimal TotalReturn,
    decimal TotalAdjustment);

public sealed record LowStockReportItemResponse(
    Guid InventoryId,
    string InventoryName,
    decimal Quantity,
    decimal ReorderLevel);

public sealed record OpenLoanReportItemResponse(
    Guid LoanId,
    string BorrowerUsername,
    string? AssetCode,
    decimal TotalItemsBorrowed,
    decimal TotalItemsReturned,
    LoanStatus Status,
    DateTime IssuedAt);

public sealed record InventoryValuationReportItemResponse(
    Guid InventoryId,
    string InventoryName,
    decimal Quantity,
    decimal AverageCost,
    decimal TotalValue);

public sealed record AssetMaintenanceCostReportItemResponse(
    Guid AssetId,
    string AssetCode,
    decimal TotalMaintenanceCost,
    decimal TotalItemsConsumed,
    int RequestCount);

public sealed record AssetConsumptionBreakdownItemResponse(
    Guid InventoryId,
    string InventoryName,
    decimal TotalQuantityUsed,
    decimal TotalCost);

public sealed record MaintenanceWithoutAssetIntegrityResponse(int Count);

public sealed record AdjustmentReportItemResponse(
    Guid RequestId,
    string? Reason,
    Guid InventoryId,
    string InventoryName,
    decimal Quantity,
    Guid ActorUserId,
    string ActorUsername,
    DateTime CreatedAt);

public sealed record SupplierSpendReportItemResponse(
    string Basis,
    Guid SupplierId,
    string SupplierName,
    decimal TotalSpend,
    int TotalPurchaseOrders,
    int TotalLines,
    decimal TotalQty,
    decimal AveragePurchaseOrderValue);

public sealed record AuditLogItemResponse(
    Guid Id,
    string Action,
    string EntityType,
    Guid EntityId,
    Guid ActorUserId,
    string? ActorUsername,
    DateTime CreatedAt,
    string? Metadata);

public sealed record AuthEventItemResponse(
    Guid Id,
    string EventType,
    string Outcome,
    string? ReasonCode,
    string? Username,
    Guid? UserId,
    string? RolesSnapshotJson,
    string AuthMethod,
    bool MfaPerformed,
    string? MfaMethod,
    string? TokenJti,
    string? CorrelationId,
    string? IpAddress,
    string? UserAgent,
    string? ClientApp,
    string? Environment,
    DateTime CreatedAt);

public sealed record IntegrityCheckResponse(
    int MaintenanceWithoutAsset,
    int LoansWithNegativeRemaining,
    int PurchaseOrdersOverReceived,
    int InventoryNegativeQuantity,
    int RequestsIssuedWithoutStockLogs,
    int DuplicateIssueLogs,
    int OrphanStockLogs,
    int OrphanApprovalActions,
    int PoWithoutWorkflow,
    int AdjustmentWithoutLogs,
    int SupplierActionsWithoutAudit,
    int PoReceiptsWithoutAudit,
    int LoanReturnsWithoutAudit,
    int AdjustmentsWithoutAudit,
    int InactiveSuppliersReferenced,
    int InactiveInventoryReferenced);

public sealed record IntegritySummaryResponse(
    int MaintenanceWithoutAsset,
    int LoansOverReturned,
    int PoOverReceived,
    int NegativeInventoryCount,
    int RequestsIssuedWithoutStockLogs,
    int DuplicateIssueLogs,
    int OrphanStockLogs,
    int OrphanApprovalActions,
    int PoWithoutWorkflow,
    int AdjustmentWithoutLogs,
    int SupplierActionsWithoutAudit,
    int PoReceiptsWithoutAudit,
    int LoanReturnsWithoutAudit,
    int AdjustmentsWithoutAudit,
    int InactiveSuppliersReferenced,
    int InactiveInventoryReferenced);

public sealed record InventoryAdjustmentLineInputRequest(
    Guid InventoryId,
    decimal QtyDelta,
    string? Remarks);

public sealed record CreateInventoryAdjustmentDraftRequest(
    string Reason,
    IReadOnlyCollection<InventoryAdjustmentLineInputRequest> Lines);

public sealed record CreateInventoryAdjustmentDraftResponse(
    Guid AdjustmentId,
    InventoryAdjustmentStatus Status);

public sealed record SubmitInventoryAdjustmentResponse(
    Guid AdjustmentId,
    InventoryAdjustmentStatus Status);

public sealed record InventoryAdjustmentDecisionRequest(string? Remarks);

public sealed record InventoryAdjustmentDecisionResponse(
    Guid AdjustmentId,
    InventoryAdjustmentStatus Status,
    ApprovalStatus ApprovalStatus);

public sealed record InventoryAdjustmentLineDetailResponse(
    Guid Id,
    Guid InventoryId,
    string InventoryName,
    decimal QtyDelta,
    string? Remarks);

public sealed record InventoryAdjustmentDetailResponse(
    Guid Id,
    InventoryAdjustmentStatus Status,
    string Reason,
    Guid CreatedByUserId,
    string CreatedByUsername,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt,
    string? RejectionReason,
    IReadOnlyCollection<InventoryAdjustmentLineDetailResponse> Lines,
    ApprovalSummaryResponse? Approval,
    IReadOnlyCollection<ApprovalActionSummaryResponse> ApprovalActions);


public sealed record LoanDetailResponse(
    Guid Id,
    Guid RequestId,
    LoanStatus Status,
    Guid BorrowerUserId,
    string BorrowerUsername,
    Guid? AssetId,
    string? AssetCode,
    DateTime IssuedAt,
    DateTime? DueAt,
    DateTime? ClosedAt,
    IReadOnlyCollection<LoanLineDetailResponse> Lines,
    IReadOnlyCollection<StockLogSummaryResponse> StockLogs,
    IReadOnlyCollection<HistoryEntryResponse> History);

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record DispatchCustomerSummaryResponse(Guid Id, string Name);

public sealed record DispatchTripStopRequest(
    TripStopType StopType,
    string LocationText,
    DateTime? ScheduledAt);

public sealed record CreateDispatchTripRequest(
    Guid CustomerId,
    Guid? DriverUserId,
    Guid? TruckAssetId,
    string? Notes,
    IReadOnlyCollection<DispatchTripStopRequest>? Stops);

public sealed record CreateDispatchTripResponse(Guid TripId, TripStatus Status);

public sealed record UpdateDispatchTripRequest(
    Guid CustomerId,
    Guid? DriverUserId,
    Guid? TruckAssetId,
    string? Notes,
    IReadOnlyCollection<DispatchTripStopRequest>? Stops,
    string? Remarks = null,
    string RowVersion = "");

public sealed record DispatchTripActionRequest(
    Guid DriverUserId,
    Guid? TruckAssetId,
    string? Remarks,
    string RowVersion = "");

public sealed record DispatchTripActionResponse(Guid TripId, TripStatus Status);

public sealed record DispatchTripDriverActionRequest(
    DateTime EventAt,
    string RowVersion = "",
    string? Remarks = null);

public sealed record DispatchTripStatusRequest(
    TripStatus ToStatus,
    string? Remarks,
    bool? PodPendingOverride,
    DateTime EventAt,
    string RowVersion = "");

public sealed record DispatchTripStatusResponse(Guid TripId, TripStatus Status);

public sealed record DispatchTripCorrectStatusRequest(
    TripStatus ToStatus,
    DateTime EventAt,
    string Remarks,
    string RowVersion = "");

public sealed record DispatchTripCorrectStatusResponse(Guid TripId, TripStatus Status);

public sealed record DispatchTripStopResponse(
    Guid Id,
    TripStopType StopType,
    string LocationText,
    DateTime? ScheduledAt,
    DateTime? ActualAt);

public sealed record DispatchTripDocumentUploadRequest(
    TripDocumentType Type,
    string StorageKey);

public sealed record DispatchTripDocumentRejectRequest(string Remarks);

public sealed record DispatchTripDocumentResponse(
    Guid Id,
    TripDocumentType Type,
    TripDocumentState State,
    string StorageKey,
    Guid UploadedByUserId,
    string? UploadedByUsername,
    Guid? VerifiedByUserId,
    string? VerifiedByUsername,
    Guid? RejectedByUserId,
    string? RejectedByUsername,
    string? Remarks,
    DateTime UploadedAt,
    DateTime? VerifiedAt,
    DateTime? RejectedAt);

public sealed record DispatchTripDocumentChecklistResponse(
    TripDocumentType Type,
    TripDocumentState State);

public sealed record DispatchTripDocumentVersionResponse(
    Guid Id,
    TripDocumentType Type,
    TripDocumentState State,
    string StorageKey,
    Guid UploadedByUserId,
    string? UploadedByUsername,
    Guid? VerifiedByUserId,
    string? VerifiedByUsername,
    Guid? RejectedByUserId,
    string? RejectedByUsername,
    string? Remarks,
    DateTime UploadedAt,
    DateTime? VerifiedAt,
    DateTime? RejectedAt,
    bool IsActive,
    Guid? SupersedesDocumentId);

public sealed record DispatchTripDocumentLinkResponse(string StorageKey);

public sealed record DispatchTripHistoryResponse(
    Guid Id,
    TripHistoryEventType EventType,
    TripStatus FromStatus,
    TripStatus ToStatus,
    Guid ActorUserId,
    string? ActorUsername,
    string? Remarks,
    DateTime EventAt,
    DateTime RecordedAt);

public sealed record DispatchTripListItemResponse(
    Guid Id,
    TripStatus Status,
    DispatchCustomerSummaryResponse Customer,
    Guid? DriverUserId,
    string? DriverUsername,
    Guid? TruckAssetId,
    string? TruckAssetCode,
    bool PodPending,
    int UploadedDocumentCount,
    int RequiredDocumentCount,
    IReadOnlyCollection<DispatchTripDocumentChecklistResponse> Documents,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? PickupLocation,
    string? DropoffLocation,
    DateTime? PickupScheduledAt,
    DateTime? DropoffScheduledAt,
    bool LatePickup,
    bool LateDelivery,
    int? OnHoldMinutes,
    string RowVersion);

public sealed record DispatchTripSummaryResponse(
    Guid Id,
    TripStatus Status,
    DispatchCustomerSummaryResponse Customer,
    Guid? DriverUserId,
    string? DriverUsername,
    Guid? TruckAssetId,
    string? TruckAssetCode,
    bool PodPending,
    int UploadedDocumentCount,
    int RequiredDocumentCount,
    IReadOnlyCollection<DispatchTripDocumentChecklistResponse> Documents,
    TripDocumentState PodState,
    Guid? CreatedByUserId,
    string? CreatedByUsername,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PickupScheduledAt,
    DateTime? DropoffScheduledAt,
    bool LatePickup,
    bool LateDelivery,
    int? OnHoldMinutes,
    string RowVersion);

public sealed record DispatchTripDetailResponse(
    Guid Id,
    TripStatus Status,
    DispatchCustomerSummaryResponse Customer,
    Guid? DriverUserId,
    string? DriverUsername,
    Guid? TruckAssetId,
    string? TruckAssetCode,
    bool PodPending,
    TripStatus? HoldPreviousStatus,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<DispatchTripStopResponse> Stops,
    IReadOnlyCollection<DispatchTripDocumentResponse> Documents,
    IReadOnlyCollection<DispatchTripHistoryResponse> History,
    bool DocVerificationEnabled,
    string RowVersion);

public sealed record CreateShipmentRequestRequest(
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    string? CargoDescription,
    decimal? CargoWeight,
    string? SpecialInstructions);

public sealed record UpdateShipmentRequestRequest(
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    string? CargoDescription,
    decimal? CargoWeight,
    string? SpecialInstructions);

public sealed record ShipmentRequestStatusResponse(Guid Id, ShipmentRequestStatus Status);

public sealed record ShipmentRequestListItemResponse(
    Guid Id,
    ShipmentRequestStatus Status,
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    int DocumentsCount,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    Guid? ConvertedTripId);

public sealed record ShipmentRequestDetailResponse(
    Guid Id,
    ShipmentRequestStatus Status,
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    string? CargoDescription,
    decimal? CargoWeight,
    string? SpecialInstructions,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    Guid? ConvertedTripId,
    IReadOnlyCollection<ShipmentRequestDocumentResponse> Documents);

public sealed record ShipmentRequestDocumentResponse(
    Guid Id,
    ShipmentRequestDocumentType DocumentType,
    string StorageKey,
    Guid UploadedByUserId,
    string? UploadedByUsername,
    DateTime UploadedAt);

public sealed record ShipmentRequestDocumentUploadRequest(
    ShipmentRequestDocumentType DocumentType,
    string StorageKey);

public sealed record ShipmentRequestRejectRequest(string Remarks);

public sealed record DispatchShipmentRequestQueueItemResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string PickupLocation,
    string DropoffLocation,
    DateTime? RequestedPickupTime,
    int DocumentsCount,
    DateTime CreatedAt);

public sealed record ShipmentRequestConversionResponse(
    Guid RequestId,
    Guid TripId,
    ShipmentRequestStatus Status);

public sealed record CustomerShipmentListItemResponse(
    Guid TripId,
    string PickupLocation,
    string DropoffLocation,
    TripStatus Status,
    DateTime? PickupTime,
    DateTime? DeliveredTime,
    TripDocumentState PodState);

public sealed record CustomerShipmentDetailResponse(
    Guid TripId,
    TripStatus Status,
    string PickupLocation,
    string DropoffLocation,
    DateTime? PickupTime,
    DateTime? DropoffTime,
    DateTime? DeliveredTime,
    TripDocumentState PodState,
    IReadOnlyCollection<CustomerShipmentStopResponse> Stops);

public sealed record CustomerShipmentStopResponse(
    TripStopType StopType,
    string LocationText,
    DateTime? ScheduledAt,
    DateTime? ActualAt);

public sealed record CustomerShipmentTimelineEntryResponse(
    TripStatus FromStatus,
    TripStatus ToStatus,
    DateTime EventAt);

public sealed record CustomerShipmentDocumentResponse(
    TripDocumentType Type,
    TripDocumentState State,
    string StorageKey,
    DateTime UploadedAt);
