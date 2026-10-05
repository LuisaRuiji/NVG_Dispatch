export type TripStatus =
  | "DRAFT"
  | "PLANNING"
  | "ASSIGNED"
  | "READY_FOR_DISPATCH"
  | "DISPATCHED"
  | "ENROUTE_PICKUP"
  | "AT_PICKUP"
  | "LOADED"
  | "ENROUTE_DROPOFF"
  | "AT_DROPOFF"
  | "DELIVERY_COMPLETED"
  | "DOCUMENTS_PENDING"
  | "OPERATIONALLY_CLOSED"
  // Accepted while older API responses and archived exports are upgraded.
  | "DELIVERED"
  | "CLOSED"
  | "CANCELLED"
  | "ON_HOLD"
  | "FAILED_ATTEMPT";

export type TripStopType = "PICKUP" | "DROPOFF";
export type TripDocumentType =
  | "WAYBILL"
  | "POD"
  | "ATW"
  | "EIR"
  | "GATE_PASS"
  | "DR"
  | "DTR"
  | "GATE_EVIDENCE"
  | "RETURN_EVIDENCE"
  | "CONTAINER_INSPECTION_PHOTO";
export type TripDocumentState = "MISSING" | "UPLOADED" | "VERIFIED" | "REJECTED";
export type TripHistoryEventType = "STATUS_CHANGE" | "SCHEDULE_UPDATED" | "STATUS_CORRECTED";
export type ContainerSize = "TWENTY_FT" | "FORTY_FT" | "FORTY_HC";
export type TripType =
  | "EXPORT_EMPTY_PICKUP"
  | "EXPORT_LADEN_TO_TERMINAL"
  | "IMPORT_LADEN_DELIVERY"
  | "EMPTY_RETURN"
  | "PORT_PICKUP"
  | "PORT_DROPOFF"
  | "YARD_TRANSFER"
  | "LONG_HAUL";

export type DispatchLifecycleReadiness = {
  preDispatchReady: boolean;
  preDispatchBlockers: string[];
  operationalCloseReady: boolean;
  operationalCloseBlockers: string[];
};

export type TripReceiptCharge = {
  description: string;
  amount: number;
};

export type GenerateTripReceiptPayload = {
  receiptNumber: string;
  baseCharge: number;
  additionalCharges: TripReceiptCharge[];
  discountAmount?: number | null;
  discountPercent?: number | null;
  discountReason?: string | null;
  taxAmount?: number | null;
  paymentMethod?: string | null;
  paymentReference?: string | null;
  notes?: string | null;
};

export type TripReceipt = {
  tripId: string;
  receiptNumber: string;
  generatedAt: string;
  currency: "PHP";
  customerName: string;
  containerNumber?: string | null;
  bookingNumber?: string | null;
  pickupLocation?: string | null;
  dropoffLocation?: string | null;
  baseCharge: number;
  additionalCharges: TripReceiptCharge[];
  subtotal: number;
  discountAmount: number;
  discountPercent?: number | null;
  discountReason?: string | null;
  taxAmount: number;
  total: number;
  paymentMethod?: string | null;
  paymentReference?: string | null;
  notes?: string | null;
};

export type DispatchCustomerSummary = {
  id: string;
  name: string;
};

export type DispatchTripListItem = {
  id: string;
  status: TripStatus;
  customer: DispatchCustomerSummary;
  containerNumber?: string | null;
  driverUserId?: string | null;
  driverUsername?: string | null;
  truckAssetId?: string | null;
  truckAssetCode?: string | null;
  podPending: boolean;
  uploadedDocumentCount: number;
  requiredDocumentCount: number;
  documents: DispatchTripDocumentChecklist[];
  podState: TripDocumentState;
  closeDocumentReady: boolean;
  missingRequiredDocumentCount: number;
  rejectedRequiredDocumentCount: number;
  closeDocumentBlockReason?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  pickupLocation?: string | null;
  pickupLatitude?: number | null;
  pickupLongitude?: number | null;
  dropoffLocation?: string | null;
  dropoffLatitude?: number | null;
  dropoffLongitude?: number | null;
  pickupScheduledAt?: string | null;
  dropoffScheduledAt?: string | null;
  plannedStart?: string | null;
  plannedEnd?: string | null;
  plannedDurationMinutes?: number | null;
  latePickup?: boolean;
  lateDelivery?: boolean;
  onHoldMinutes?: number | null;
  latestDriverLocation?: DispatchTripLatestDriverLocation | null;
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
  closeDocumentReady: boolean;
  missingRequiredDocumentCount: number;
  rejectedRequiredDocumentCount: number;
  closeDocumentBlockReason?: string | null;
  createdByUserId?: string | null;
  createdByUsername?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  pickupScheduledAt?: string | null;
  dropoffScheduledAt?: string | null;
  plannedStart?: string | null;
  plannedEnd?: string | null;
  plannedDurationMinutes?: number | null;
  latePickup?: boolean;
  lateDelivery?: boolean;
  onHoldMinutes?: number | null;
  rowVersion: string;
};

export type DispatchAssignmentGroupBy = "driver" | "truck";

export type DispatchAssignmentDayTripItem = {
  tripId: string;
  tripReference: string;
  status: TripStatus;
  customer: DispatchCustomerSummary;
  driverUserId?: string | null;
  driverUsername?: string | null;
  truckAssetId?: string | null;
  truckAssetCode?: string | null;
  plannedStart: string;
  plannedEnd: string;
  plannedDurationMinutes: number;
  hasOverlap: boolean;
};

export type DispatchAssignmentDayGroup = {
  groupKey: string;
  groupLabel: string;
  hasOverlap: boolean;
  trips: DispatchAssignmentDayTripItem[];
};

export type DispatchAssignmentDayView = {
  day: string;
  groupBy: DispatchAssignmentGroupBy;
  groups: DispatchAssignmentDayGroup[];
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
  latitude?: number | null;
  longitude?: number | null;
};

export type DispatchTripLatestDriverLocation = {
  latitude: number;
  longitude: number;
  accuracyMeters?: number | null;
  recordedAt: string;
  isStale?: boolean;
};

export type DispatchTripFinancials = {
  rate?: number | null;
  payroll?: number | null;
  allowance?: number | null;
  fuelAmount?: number | null;
  fuelPricePerLiter?: number | null;
  officialReceiptNumber?: string | null;
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
  originalFileName?: string | null;
  contentType?: string | null;
  sizeBytes?: number | null;
  referenceNumber?: string | null;
  expiryDate?: string | null;
  carrier?: string | null;
  terminalOrDepot?: string | null;
  direction?: "NOT_APPLICABLE" | "GATE_IN" | "GATE_OUT" | null;
  documentEventAt?: string | null;
  containerCondition?: string | null;
  isProofOfDelivery?: boolean;
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

export type GeneratedWaybill = {
  id: string;
  tripId: string;
  waybillNumber: string;
  version: number;
  generatedAt: string;
  generatedByUserId: string;
  waybillDataJson: string;
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
  trailerAssetId?: string | null;
  trailerAssetCode?: string | null;
  containerNumber?: string | null;
  eirNumber?: string | null;
  bookingNumber?: string | null;
  shippingLine?: string | null;
  containerSize?: string | null;
  tripType?: string | null;
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
  financials?: DispatchTripFinancials | null;
  latestDriverLocation?: DispatchTripLatestDriverLocation | null;
};

export type DispatchTripLocationPingRequest = {
  latitude: number;
  longitude: number;
  accuracyMeters?: number | null;
  recordedAt?: string | null;
};

export type DispatchTripLocationPingResponse = DispatchTripLatestDriverLocation & {
  id: string;
  tripId: string;
  driverId: string;
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
  "DELIVERY_COMPLETED"
];

export const statusLabels: Record<TripStatus, string> = {
  DRAFT: "Draft",
  PLANNING: "Planning",
  ASSIGNED: "Assigned",
  READY_FOR_DISPATCH: "Ready for dispatch",
  DISPATCHED: "Dispatched",
  ENROUTE_PICKUP: "Enroute pickup",
  AT_PICKUP: "At pickup",
  LOADED: "Loaded",
  ENROUTE_DROPOFF: "Enroute dropoff",
  AT_DROPOFF: "At dropoff",
  DELIVERY_COMPLETED: "Delivery completed",
  DOCUMENTS_PENDING: "Documents pending",
  OPERATIONALLY_CLOSED: "Operationally closed",
  DELIVERED: "Delivered",
  CLOSED: "Closed",
  CANCELLED: "Cancelled",
  ON_HOLD: "On hold",
  FAILED_ATTEMPT: "Failed attempt"
};

export function nextOperationalStatus(current: TripStatus): TripStatus | null {
  const idx = operationalFlow.indexOf(current);
  if (idx < 0 || idx + 1 >= operationalFlow.length) return null;
  return operationalFlow[idx + 1];
}
