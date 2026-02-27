export type RequestType = "MAINTENANCE_ISSUE" | "BORROW" | "ADJUSTMENT_DAMAGE_LOSS";
export type RequestStatus =
  | "DRAFT"
  | "SUBMITTED"
  | "PENDING_IO"
  | "PENDING_MANAGER"
  | "APPROVED"
  | "ISSUED"
  | "CLOSED"
  | "REJECTED";
export type ApprovalStatus = "PENDING" | "APPROVED" | "REJECTED";
export type ApprovalDecision = "APPROVE" | "REJECT";
export type ItemType = "CONSUMABLE" | "NON_CONSUMABLE";
export type ReturnCondition = "GOOD" | "DAMAGED" | "LOST";
export type LoanStatus = "OPEN" | "PARTIALLY_RETURNED" | "CLOSED";
export type AssetType = "TRUCK" | "TRAILER";
export type AssetStatus = "ACTIVE" | "INACTIVE";

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  userId: string;
  roles: string[];
}

export interface CurrentUserResponse {
  userId: string;
  username: string;
  roles: string[];
}

export interface RequestLineInputDto {
  inventoryId: string;
  quantity: number;
  remarks?: string | null;
}

export interface CreateRequestDraftRequest {
  requestType: RequestType;
  assetId?: string | null;
  purpose?: string | null;
  lines: RequestLineInputDto[];
}

export interface CreateRequestDraftResponse {
  requestId: string;
  status: RequestStatus;
}

export interface RequestListItemResponse {
  id: string;
  requestType: RequestType;
  status: RequestStatus;
  requesterUserId: string;
  requesterUsername: string;
  assetId?: string | null;
  assetCode?: string | null;
  purpose?: string | null;
  createdAt: string;
  submittedAt?: string | null;
  approvedAt?: string | null;
  issuedAt?: string | null;
  closedAt?: string | null;
}

export interface RequestLineDetailResponse {
  id: string;
  inventoryId: string;
  inventoryName: string;
  unit: string;
  itemType: ItemType;
  currentQuantity: number;
  qtyRequested: number;
  qtyApproved?: number | null;
  remarks?: string | null;
}

export interface ApprovalSummaryResponse {
  approvalId: string;
  status: ApprovalStatus;
  currentStep: number;
  nextApproverRole?: string | null;
  workflowKey: string;
}

export interface RequestDetailResponse {
  id: string;
  requestType: RequestType;
  status: RequestStatus;
  requesterUserId: string;
  requesterUsername: string;
  assetId?: string | null;
  assetCode?: string | null;
  purpose?: string | null;
  createdAt: string;
  submittedAt?: string | null;
  approvedAt?: string | null;
  issuedAt?: string | null;
  closedAt?: string | null;
  loanId?: string | null;
  lines: RequestLineDetailResponse[];
  approval?: ApprovalSummaryResponse | null;
}

export interface InventoryOfficerReviewRequest {
  decision?: ApprovalDecision;
  remarks?: string | null;
  ioRemarks?: string | null;
  lines: { requestLineId: string; qtyApproved: number; remarks?: string | null }[];
}

export interface InventoryOfficerReviewResponse {
  requestId: string;
  status: RequestStatus;
}

export interface ManagerDecisionRequest {
  decision: ApprovalDecision;
  remarks?: string | null;
}

export interface IssueRequestResponse {
  requestId: string;
  status: RequestStatus;
}

export interface SubmitRequestResponse {
  requestId: string;
  status: RequestStatus;
}

export interface SubmitMaintenanceIssueRequest {
  assetId: string;
  purpose?: string | null;
  lines: RequestLineInputDto[];
}

export interface SubmitMaintenanceIssueResponse {
  requestId: string;
  approvalId: string;
  status: RequestStatus;
}

export interface CreateInventoryItemRequest {
  name: string;
  unit: string;
  itemType: ItemType;
  quantity: number;
  reorderLevel?: number | null;
  location?: string | null;
  unitValue?: number | null;
  isKit?: boolean | null;
}

export interface InventoryItemResponse {
  id: string;
  name: string;
  unit: string;
  itemType: ItemType;
  isKit: boolean;
  quantity: number;
  reorderLevel?: number | null;
  location?: string | null;
  unitValue?: number | null;
}

export interface CreateAssetRequest {
  assetCode: string;
  assetType: AssetType;
  plateNo?: string | null;
  status?: AssetStatus | null;
}

export interface AssetResponse {
  id: string;
  assetCode: string;
  assetType: AssetType;
  status: AssetStatus;
  plateNo?: string | null;
}

export interface LoanListItemResponse {
  id: string;
  requestId: string;
  status: LoanStatus;
  borrowerUserId: string;
  borrowerUsername: string;
  assetId?: string | null;
  assetCode?: string | null;
  issuedAt: string;
  dueAt?: string | null;
  closedAt?: string | null;
}

export interface LoanLineReturnResponse {
  id: string;
  qtyReturned: number;
  condition: ReturnCondition;
  missingComponentsJson?: string | null;
  receivedByUserId: string;
  receivedByUsername?: string | null;
  returnedAt: string;
}

export interface LoanLineDetailResponse {
  id: string;
  inventoryId: string;
  inventoryName: string;
  unit: string;
  itemType: ItemType;
  qtyIssued: number;
  qtyReturned: number;
  returns: LoanLineReturnResponse[];
}

export interface LoanDetailResponse {
  id: string;
  requestId: string;
  status: LoanStatus;
  borrowerUserId: string;
  borrowerUsername: string;
  assetId?: string | null;
  assetCode?: string | null;
  issuedAt: string;
  dueAt?: string | null;
  closedAt?: string | null;
  lines: LoanLineDetailResponse[];
}

export interface LoanReturnRequest {
  lines: {
    loanLineId: string;
    qtyReturnedIncrement: number;
    condition: ReturnCondition;
    missingComponentsJson?: string | null;
  }[];
}

export interface LoanReturnResponse {
  loanId: string;
  status: LoanStatus;
}

export interface ModuleSettingResponse {
  moduleKey: string;
  displayName: string;
  isEnabled: boolean;
  createdAt: string;
  updatedAt?: string | null;
  updatedByUserId?: string | null;
  updatedByUsername?: string | null;
  notes?: string | null;
}

export interface UpdateModuleSettingRequest {
  isEnabled: boolean;
  notes?: string | null;
}
