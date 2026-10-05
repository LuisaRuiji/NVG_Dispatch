import type {
  DispatchTripLatestDriverLocation,
  TripDocumentState,
  TripStatus,
  TripStopType
} from "@/features/dispatch/types";

export type ShipmentRequestStatus =
  | "DRAFT"
  | "SUBMITTED"
  | "UNDER_REVIEW"
  | "APPROVED"
  | "REJECTED"
  | "CANCELLED"
  | "AWAITING_FINANCE_CLEARANCE"
  | "CLEARED_FOR_PLANNING"
  | "NEEDS_REVISION"
  | "CONVERTED_TO_TRIP";

export type ShipmentRequestDocumentType =
  | "ATW"
  | "INVOICE"
  | "CARGO_MANIFEST"
  | "DELIVERY_INSTRUCTIONS"
  | "BOOKING_CONFIRMATION"
  | "RELEASE_CONFIRMATION"
  | "TERMINAL_AUTHORIZATION"
  | "BILL_OF_LADING"
  | "SEA_WAYBILL"
  | "DELIVERY_ORDER"
  | "CRO"
  | "WEB_CRO"
  | "RETURN_DEPOT_AUTHORIZATION"
  | "RETURN_INSTRUCTION"
  | "OTHER";

export type ContainerSize = "TWENTY_FT" | "FORTY_FT" | "FORTY_HC";
export type TripType = "EXPORT_EMPTY_PICKUP" | "EXPORT_LADEN_TO_TERMINAL" | "IMPORT_LADEN_DELIVERY" | "EMPTY_RETURN" | "PORT_PICKUP" | "PORT_DROPOFF" | "YARD_TRANSFER" | "LONG_HAUL";

export const containerSizeLabels: Record<ContainerSize, string> = {
  TWENTY_FT: "20 ft",
  FORTY_FT: "40 ft",
  FORTY_HC: "40 HC"
};

export const tripTypeLabels: Record<TripType, string> = {
  EXPORT_EMPTY_PICKUP: "Export empty pickup",
  EXPORT_LADEN_TO_TERMINAL: "Export laden to terminal",
  IMPORT_LADEN_DELIVERY: "Import laden delivery",
  EMPTY_RETURN: "Empty return",
  PORT_PICKUP: "Port Pickup",
  PORT_DROPOFF: "Port Dropoff",
  YARD_TRANSFER: "Yard Transfer",
  LONG_HAUL: "Long Haul"
};

export type ShipmentRequestListItem = {
  id: string;
  status: ShipmentRequestStatus;
  pickupLocation: string;
  pickupLatitude?: number | null;
  pickupLongitude?: number | null;
  dropoffLocation: string;
  dropoffLatitude?: number | null;
  dropoffLongitude?: number | null;
  requestedPickupTime?: string | null;
  containerSize: ContainerSize;
  tripType: TripType;
  containerNumber?: string | null;
  shippingLine?: string | null;
  bookingNumber?: string | null;
  documentsCount: number;
  createdAt: string;
  approvedAt?: string | null;
  convertedTripId?: string | null;
  financeClearanceStatus?: string | null;
};

export type ShipmentRequestDocument = {
  id: string;
  documentType: ShipmentRequestDocumentType;
  storageKey: string;
  uploadedByUserId: string;
  uploadedByUsername?: string | null;
  uploadedAt: string;
  verificationState?: TripDocumentState;
  originalFileName?: string | null;
  contentType?: string | null;
  sizeBytes?: number | null;
  expiryDate?: string | null;
  carrier?: string | null;
  terminalOrDepot?: string | null;
  referenceNumber?: string | null;
  rejectionReason?: string | null;
};

export type ShipmentRequestDetail = {
  id: string;
  status: ShipmentRequestStatus;
  pickupLocation: string;
  pickupLatitude?: number | null;
  pickupLongitude?: number | null;
  dropoffLocation: string;
  dropoffLatitude?: number | null;
  dropoffLongitude?: number | null;
  requestedPickupTime?: string | null;
  containerSize: ContainerSize;
  tripType: TripType;
  containerNumber?: string | null;
  shippingLine?: string | null;
  bookingNumber?: string | null;
  cargoDescription?: string | null;
  cargoWeight?: number | null;
  specialInstructions?: string | null;
  reviewRemarks?: string | null;
  createdAt: string;
  approvedAt?: string | null;
  convertedTripId?: string | null;
  financeClearanceStatus?: string | null;
  documents: ShipmentRequestDocument[];
};

export type ShipmentRequestStatusResponse = {
  id: string;
  status: ShipmentRequestStatus;
};

export type CustomerShipmentListItem = {
  tripId: string;
  containerNumber?: string | null;
  pickupLocation: string;
  dropoffLocation: string;
  status: TripStatus;
  pickupTime?: string | null;
  deliveredTime?: string | null;
  podState: TripDocumentState;
};

export type CustomerShipmentDetail = {
  tripId: string;
  containerNumber?: string | null;
  status: TripStatus;
  pickupLocation: string;
  dropoffLocation: string;
  pickupTime?: string | null;
  dropoffTime?: string | null;
  deliveredTime?: string | null;
  podState: TripDocumentState;
  atwState: TripDocumentState;
  waybillGenerated: boolean;
  stops: CustomerShipmentStop[];
  latestDriverLocation?: DispatchTripLatestDriverLocation | null;
};

export type CustomerShipmentStop = {
  stopType: TripStopType;
  locationText: string;
  latitude?: number | null;
  longitude?: number | null;
  scheduledAt?: string | null;
  actualAt?: string | null;
};

export type CustomerShipmentTimelineEntry = {
  fromStatus: TripStatus;
  toStatus: TripStatus;
  eventAt: string;
};

export type CustomerShipmentDocument = {
  type: "ATW" | "POD";
  state: TripDocumentState;
  storageKey: string;
  uploadedAt: string;
};
