export type PurchaseOrderListItem = {
  id: string;
  status: string;
  supplierName: string;
  createdByUsername: string;
  createdAt: string;
  submittedAt?: string | null;
  approvedAt?: string | null;
  receivedAt?: string | null;
  closedAt?: string | null;
};

export type PurchaseOrderLine = {
  id: string;
  inventoryId: string;
  inventoryName: string;
  unit: string;
  itemType: string;
  qtyOrdered: number;
  qtyReceived: number;
  unitPrice?: number | null;
  remarks?: string | null;
};

export type PurchaseOrderReceipt = {
  id: string;
  purchaseOrderLineId: string;
  qtyReceivedIncrement: number;
  receivedByUserId: string;
  receivedByUsername?: string | null;
  receivedAt: string;
};

export type ApprovalSummary = {
  approvalId: string;
  status?: string | null;
  currentStep?: number | null;
  nextApproverRole?: string | null;
  workflowKey?: string | null;
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

export type PurchaseOrderDetail = {
  id: string;
  status: string;
  supplierName: string;
  supplierId: string;
  createdByUsername: string;
  createdByUserId: string;
  notes?: string | null;
  createdAt: string;
  submittedAt?: string | null;
  approvedAt?: string | null;
  rejectedAt?: string | null;
  rejectionReason?: string | null;
  receivedAt?: string | null;
  closedAt?: string | null;
  lines: PurchaseOrderLine[];
  receipts: PurchaseOrderReceipt[];
  approval?: ApprovalSummary | null;
  approvalActions: ApprovalActionSummary[];
};
