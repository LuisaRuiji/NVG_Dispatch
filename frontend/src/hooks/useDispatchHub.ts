import { useEffect, useRef } from "react";
import { getDispatchHubConnection, startDispatchHub, stopDispatchHub } from "@/lib/signalr";

export type TripStatusChangedEvent = {
  tripId: string;
  containerNumber: string;
  newStatus: string;
  previousStatus: string;
  driverName: string;
  changedAt: string;
};

export type DocumentUploadedEvent = {
  tripId: string;
  containerNumber: string;
  documentType: string;
  uploaderName: string;
  uploadedAt: string;
};

export type DocumentVerifiedEvent = {
  tripId: string;
  containerNumber: string;
  documentType: string;
  isVerified: boolean;
  verifiedBy: string;
  verifiedAt: string;
};

export type RecommendationGeneratedEvent = {
  completedTripId: string;
  driverName: string;
  truckPlate: string;
  deliveredAt: string;
  recommendationCount: number;
};

export type ShipmentRequestSubmittedEvent = {
  requestId: string;
  customerName: string;
  pickupLocation: string;
  dropoffLocation: string;
  submittedAt: string;
};

interface DispatchHubHandlers {
  onTripStatusChanged?: (e: TripStatusChangedEvent) => void;
  onDocumentUploaded?: (e: DocumentUploadedEvent) => void;
  onDocumentVerified?: (e: DocumentVerifiedEvent) => void;
  onRecommendationGenerated?: (e: RecommendationGeneratedEvent) => void;
  onShipmentRequestSubmitted?: (e: ShipmentRequestSubmittedEvent) => void;
}

export function useDispatchHub(handlers: DispatchHubHandlers): void {
  const handlersRef = useRef(handlers);

  useEffect(() => {
    handlersRef.current = handlers;
  }, [handlers]);

  useEffect(() => {
    const hub = getDispatchHubConnection();
    const tripStatusChanged = (e: TripStatusChangedEvent) => handlersRef.current.onTripStatusChanged?.(e);
    const documentUploaded = (e: DocumentUploadedEvent) => handlersRef.current.onDocumentUploaded?.(e);
    const documentVerified = (e: DocumentVerifiedEvent) => handlersRef.current.onDocumentVerified?.(e);
    const recommendationGenerated = (e: RecommendationGeneratedEvent) =>
      handlersRef.current.onRecommendationGenerated?.(e);
    const shipmentRequestSubmitted = (e: ShipmentRequestSubmittedEvent) =>
      handlersRef.current.onShipmentRequestSubmitted?.(e);

    hub.on("TripStatusChanged", tripStatusChanged);
    hub.on("DocumentUploaded", documentUploaded);
    hub.on("DocumentVerified", documentVerified);
    hub.on("RecommendationGenerated", recommendationGenerated);
    hub.on("ShipmentRequestSubmitted", shipmentRequestSubmitted);
    void startDispatchHub().catch((error) => {
      console.warn("Dispatch hub connection failed.", error);
    });

    return () => {
      hub.off("TripStatusChanged", tripStatusChanged);
      hub.off("DocumentUploaded", documentUploaded);
      hub.off("DocumentVerified", documentVerified);
      hub.off("RecommendationGenerated", recommendationGenerated);
      hub.off("ShipmentRequestSubmitted", shipmentRequestSubmitted);
      void stopDispatchHub().catch(() => undefined);
    };
  }, []);
}
