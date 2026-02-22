export type RequestListItem = {
  id: string;
  requestType: string;
  status: string;
  assetId?: string | null;
  assetCode?: string | null;
  requesterUserId?: string;
  requesterUsername?: string;
  submittedAt?: string | null;
  createdAt?: string | null;
};

export type RequestLineDetail = {
  id: string;
  inventoryId: string;
  inventoryName: string;
  currentQuantity: number;
  qtyRequested: number;
  qtyApproved?: number | null;
};

export type ApprovalSummary = {
  workflowKey?: string | null;
  currentStep?: number | null;
  status?: string | null;
  nextApproverRole?: string | null;
};

export type ApprovalActionSummary = {
  id: string;
  decision: string;
  remarks?: string | null;
  actorUserId: string;
  actorUsername?: string | null;
  createdAt: string;
  stepOrder: number;
};

export type StockLogSummary = {
  id: string;
  movementType: string;
  quantity: number;
  actorUserId: string;
  actorUsername?: string | null;
  createdAt: string;
};

export type HistoryEntry = {
  type: string;
  actor?: string | null;
  decision?: string | null;
  step?: number | null;
  movementType?: string | null;
  qty?: number | null;
  condition?: string | null;
  timestamp: string;
};

export type RequestDetail = {
  id: string;
  requestType: string;
  status: string;
  assetId?: string | null;
  assetCode?: string | null;
  requesterUserId: string;
  requesterUsername: string;
  loanId?: string | null;
  lines: RequestLineDetail[];
  approval?: ApprovalSummary | null;
  approvalActions?: ApprovalActionSummary[];
  stockLogs?: StockLogSummary[];
  history?: HistoryEntry[];
};
