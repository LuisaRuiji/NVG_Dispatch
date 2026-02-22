import { apiGet, apiPost } from "@/lib/api/client";
import type {
  ApprovalDecision,
  AssetResponse,
  CreateAssetRequest,
  CreateInventoryItemRequest,
  CreateRequestDraftRequest,
  CreateRequestDraftResponse,
  CurrentUserResponse,
  InventoryItemResponse,
  InventoryOfficerReviewRequest,
  InventoryOfficerReviewResponse,
  IssueRequestResponse,
  LoanDetailResponse,
  LoanListItemResponse,
  LoanReturnRequest,
  LoanReturnResponse,
  LoginRequest,
  LoginResponse,
  ManagerDecisionRequest,
  RequestDetailResponse,
  RequestListItemResponse,
  RequestStatus,
  RequestType,
  SubmitMaintenanceIssueRequest,
  SubmitMaintenanceIssueResponse,
  SubmitRequestResponse
} from "@/lib/api/types";

export const authApi = {
  login: (payload: LoginRequest) => apiPost<LoginResponse>("/api/auth/login", payload),
  me: (token: string) => apiGet<CurrentUserResponse>("/api/auth/me", token)
};

export const requestApi = {
  createDraft: (payload: CreateRequestDraftRequest, token: string) =>
    apiPost<CreateRequestDraftResponse>("/api/requests", payload, token),
  submit: (requestId: string, token: string) =>
    apiPost<SubmitRequestResponse>(`/api/requests/${requestId}/submit`, undefined, token),
  submitMaintenanceIssue: (payload: SubmitMaintenanceIssueRequest, token: string) =>
    apiPost<SubmitMaintenanceIssueResponse>("/api/requests/maintenance-issue", payload, token),
  ioReview: (requestId: string, payload: InventoryOfficerReviewRequest, token: string) =>
    apiPost<InventoryOfficerReviewResponse>(`/api/requests/${requestId}/io-review`, payload, token),
  managerDecision: (requestId: string, payload: ManagerDecisionRequest, token: string) =>
    apiPost<SubmitRequestResponse>(`/api/requests/${requestId}/manager-decision`, payload, token),
  issueMaintenance: (requestId: string, token: string) =>
    apiPost<IssueRequestResponse>(`/api/requests/${requestId}/issue`, undefined, token),
  issueStock: (requestId: string, token: string) =>
    apiPost<IssueRequestResponse>(`/api/requests/${requestId}/issue-stock`, undefined, token),
  list: (filters: { type?: RequestType; status?: RequestStatus }, token: string) => {
    const params = new URLSearchParams();
    if (filters.type) params.set("type", filters.type);
    if (filters.status) params.set("status", filters.status);
    const suffix = params.toString();
    return apiGet<RequestListItemResponse[]>(`/api/requests${suffix ? `?${suffix}` : ""}`, token);
  },
  detail: (requestId: string, token: string) =>
    apiGet<RequestDetailResponse>(`/api/requests/${requestId}`, token)
};

export const inventoryApi = {
  createItem: (payload: CreateInventoryItemRequest, token: string) =>
    apiPost<InventoryItemResponse>("/api/inventory", payload, token)
};

export const assetApi = {
  createAsset: (payload: CreateAssetRequest, token: string) =>
    apiPost<AssetResponse>("/api/assets", payload, token)
};

export const loanApi = {
  list: (status: string | undefined, token: string) => {
    const suffix = status ? `?status=${encodeURIComponent(status)}` : "";
    return apiGet<LoanListItemResponse[]>(`/api/loans${suffix}`, token);
  },
  detail: (loanId: string, token: string) =>
    apiGet<LoanDetailResponse>(`/api/loans/${loanId}`, token),
  returnLoan: (loanId: string, payload: LoanReturnRequest, token: string) =>
    apiPost<LoanReturnResponse>(`/api/loans/${loanId}/return`, payload, token)
};

export type { ApprovalDecision };
