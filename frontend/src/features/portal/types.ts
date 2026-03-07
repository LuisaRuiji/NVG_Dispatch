import type { TripDocumentState, TripStatus, TripStopType } from "@/features/dispatch/types";

export type ShipmentRequestStatus =
  | "DRAFT"
  | "SUBMITTED"
  | "APPROVED"
  | "REJECTED"
  | "CONVERTED_TO_TRIP";

export type ShipmentRequestDocumentType =
  | "INVOICE"
  | "CARGO_MANIFEST"
  | "DELIVERY_INSTRUCTIONS"
  | "OTHER";

export type ShipmentRequestListItem = {
  id: string;
  status: ShipmentRequestStatus;
  pickupLocation: string;
  dropoffLocation: string;
  requestedPickupTime?: string | null;
  documentsCount: number;
  createdAt: string;
  approvedAt?: string | null;
  convertedTripId?: string | null;
};

export type ShipmentRequestDocument = {
  id: string;
  documentType: ShipmentRequestDocumentType;
  storageKey: string;
  uploadedByUserId: string;
  uploadedByUsername?: string | null;
  uploadedAt: string;
};

export type ShipmentRequestDetail = {
  id: string;
  status: ShipmentRequestStatus;
  pickupLocation: string;
  dropoffLocation: string;
  requestedPickupTime?: string | null;
  cargoDescription?: string | null;
  cargoWeight?: number | null;
  specialInstructions?: string | null;
  createdAt: string;
  approvedAt?: string | null;
  convertedTripId?: string | null;
  documents: ShipmentRequestDocument[];
};

export type ShipmentRequestStatusResponse = {
  id: string;
  status: ShipmentRequestStatus;
};

export type CustomerShipmentListItem = {
  tripId: string;
  pickupLocation: string;
  dropoffLocation: string;
  status: TripStatus;
  pickupTime?: string | null;
  deliveredTime?: string | null;
  podState: TripDocumentState;
};

export type CustomerShipmentDetail = {
  tripId: string;
  status: TripStatus;
  pickupLocation: string;
  dropoffLocation: string;
  pickupTime?: string | null;
  dropoffTime?: string | null;
  deliveredTime?: string | null;
  podState: TripDocumentState;
  stops: CustomerShipmentStop[];
};

export type CustomerShipmentStop = {
  stopType: TripStopType;
  locationText: string;
  scheduledAt?: string | null;
  actualAt?: string | null;
};

export type CustomerShipmentTimelineEntry = {
  fromStatus: TripStatus;
  toStatus: TripStatus;
  eventAt: string;
};

export type CustomerShipmentDocument = {
  type: "POD";
  state: TripDocumentState;
  storageKey: string;
  uploadedAt: string;
};
