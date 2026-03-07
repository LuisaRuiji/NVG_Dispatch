export type TripStatus =
  | "DRAFT"
  | "DISPATCHED"
  | "ENROUTE_PICKUP"
  | "AT_PICKUP"
  | "LOADED"
  | "ENROUTE_DROPOFF"
  | "AT_DROPOFF"
  | "DELIVERED"
  | "CLOSED"
  | "CANCELLED"
  | "ON_HOLD"
  | "FAILED_ATTEMPT";

export type TripStopType = "PICKUP" | "DROPOFF";
export type TripDocumentType = "WAYBILL" | "POD" | "ATW";
export type TripDocumentState = "MISSING" | "UPLOADED" | "VERIFIED" | "REJECTED";
export type TripHistoryEventType = "STATUS_CHANGE" | "SCHEDULE_UPDATED" | "STATUS_CORRECTED";

export type DispatchCustomerSummary = {
  id: string;
  name: string;
};

export type DispatchTripListItem = {
  id: string;
  status: TripStatus;
  customer: DispatchCustomerSummary;
  driverUserId?: string | null;
  driverUsername?: string | null;
  truckAssetId?: string | null;
  truckAssetCode?: string | null;
  podPending: boolean;
  uploadedDocumentCount: number;
  requiredDocumentCount: number;
  documents: DispatchTripDocumentChecklist[];
  createdAt: string;
  updatedAt?: string | null;
  pickupLocation?: string | null;
  dropoffLocation?: string | null;
  pickupScheduledAt?: string | null;
  dropoffScheduledAt?: string | null;
  latePickup?: boolean;
  lateDelivery?: boolean;
  onHoldMinutes?: number | null;
  rowVersion: string;
};

export type DispatchTripSummary = {
  id: string;
  status: TripStatus;
  customer: DispatchCustomerSummary;
  driverUserId?: string | null;
  driverUsername?: string | null;
  truckAssetId?: string | null;
  truckAssetCode?: string | null;
  podPending: boolean;
  uploadedDocumentCount: number;
  requiredDocumentCount: number;
  documents: DispatchTripDocumentChecklist[];
  podState: TripDocumentState;
  createdByUserId?: string | null;
  createdByUsername?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  pickupScheduledAt?: string | null;
  dropoffScheduledAt?: string | null;
  latePickup?: boolean;
  lateDelivery?: boolean;
  onHoldMinutes?: number | null;
  rowVersion: string;
};

export type DispatchTripDocumentChecklist = {
  type: TripDocumentType;
  state: TripDocumentState;
};

export type DispatchTripStop = {
  id: string;
  stopType: TripStopType;
  locationText: string;
  scheduledAt?: string | null;
  actualAt?: string | null;
};

export type DispatchTripDocument = {
  id: string;
  type: TripDocumentType;
  state: TripDocumentState;
  storageKey: string;
  uploadedByUserId: string;
  uploadedByUsername?: string | null;
  verifiedByUserId?: string | null;
  verifiedByUsername?: string | null;
  rejectedByUserId?: string | null;
  rejectedByUsername?: string | null;
  remarks?: string | null;
  uploadedAt: string;
  verifiedAt?: string | null;
  rejectedAt?: string | null;
};

export type DispatchTripDocumentVersion = {
  id: string;
  type: TripDocumentType;
  state: TripDocumentState;
  storageKey: string;
  uploadedByUserId: string;
  uploadedByUsername?: string | null;
  verifiedByUserId?: string | null;
  verifiedByUsername?: string | null;
  rejectedByUserId?: string | null;
  rejectedByUsername?: string | null;
  remarks?: string | null;
  uploadedAt: string;
  verifiedAt?: string | null;
  rejectedAt?: string | null;
  isActive: boolean;
  supersedesDocumentId?: string | null;
};

export type DispatchTripDocumentLink = {
  storageKey: string;
};

export type DispatchTripHistory = {
  id: string;
  eventType: TripHistoryEventType;
  fromStatus: TripStatus;
  toStatus: TripStatus;
  actorUserId: string;
  actorUsername?: string | null;
  remarks?: string | null;
  eventAt: string;
  recordedAt: string;
};

export type DispatchTripDetail = {
  id: string;
  status: TripStatus;
  customer: DispatchCustomerSummary;
  driverUserId?: string | null;
  driverUsername?: string | null;
  truckAssetId?: string | null;
  truckAssetCode?: string | null;
  podPending: boolean;
  holdPreviousStatus?: TripStatus | null;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  stops: DispatchTripStop[];
  documents: DispatchTripDocument[];
  history: DispatchTripHistory[];
  docVerificationEnabled: boolean;
  rowVersion: string;
};

export type DispatchTripStatusRequest = {
  toStatus: TripStatus;
  remarks?: string | null;
  podPendingOverride?: boolean | null;
  eventAt: string;
  rowVersion: string;
};

export const operationalFlow: TripStatus[] = [
  "DISPATCHED",
  "ENROUTE_PICKUP",
  "AT_PICKUP",
  "LOADED",
  "ENROUTE_DROPOFF",
  "AT_DROPOFF",
  "DELIVERED"
];

export const statusLabels: Record<TripStatus, string> = {
  DRAFT: "Draft",
  DISPATCHED: "Dispatched",
  ENROUTE_PICKUP: "Enroute Pickup",
  AT_PICKUP: "At Pickup",
  LOADED: "Loaded",
  ENROUTE_DROPOFF: "Enroute Dropoff",
  AT_DROPOFF: "At Dropoff",
  DELIVERED: "Delivered",
  CLOSED: "Closed",
  CANCELLED: "Cancelled",
  ON_HOLD: "On Hold",
  FAILED_ATTEMPT: "Failed Attempt"
};

export function nextOperationalStatus(current: TripStatus): TripStatus | null {
  const idx = operationalFlow.indexOf(current);
  if (idx < 0 || idx + 1 >= operationalFlow.length) return null;
  return operationalFlow[idx + 1];
}
