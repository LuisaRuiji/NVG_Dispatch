import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import TripMap from "@/components/dispatch/TripMap";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api, apiOptional } from "@/lib/api";
import { captureDocumentPhoto } from "@/lib/camera";
import { getBrowserLocationFix } from "@/lib/geolocation";
import type {
  DispatchTripDetail,
  DispatchTripLocationPingRequest,
  DispatchTripLocationPingResponse,
  GeneratedWaybill,
  TripDocumentType,
  TripStatus
} from "./types";
import { operationalFlow, statusLabels } from "./types";
import {
  canDriverUploadDocument,
  formatDocumentLabel,
  formatDocumentStateLabel,
  getDocumentState,
  getDriverTripNextAction,
  getPrimaryDriverUploadType,
  isDocumentAttentionState
} from "./driverTripUi";
import { Capacitor } from "@capacitor/core";
import { AlertTriangle, Camera, FileUp, MapPin } from "lucide-react";

const uploadableDocTypes: TripDocumentType[] = ["ATW", "EIR", "GATE_PASS", "DR", "POD"];
const visibleDocTypes: TripDocumentType[] = ["ATW", "EIR", "GATE_PASS", "DR", "POD", "WAYBILL"];
const driverHoldEligible: TripStatus[] = [
  "ENROUTE_PICKUP",
  "AT_PICKUP",
  "LOADED",
  "ENROUTE_DROPOFF",
  "AT_DROPOFF"
];
const failedAttemptEligible: TripStatus[] = [
  "ENROUTE_PICKUP",
  "AT_PICKUP",
  "ENROUTE_DROPOFF",
  "AT_DROPOFF"
];
const locationSharingStatuses: TripStatus[] = [
  "DISPATCHED",
  "ENROUTE_PICKUP",
  "AT_PICKUP",
  "LOADED",
  "ENROUTE_DROPOFF",
  "AT_DROPOFF"
];
const driverActionMap: Record<TripStatus, { endpoint: string; label: string } | null> = {
  DRAFT: null,
  DISPATCHED: { endpoint: "start", label: "Start pickup" },
  ENROUTE_PICKUP: { endpoint: "arrive-pickup", label: "Arrive pickup" },
  AT_PICKUP: { endpoint: "confirm-loaded", label: "Confirm loaded" },
  LOADED: { endpoint: "depart-pickup", label: "Depart pickup" },
  ENROUTE_DROPOFF: { endpoint: "arrive-dropoff", label: "Arrive dropoff" },
  AT_DROPOFF: { endpoint: "confirm-delivery", label: "Confirm delivery" },
  DELIVERED: null,
  CLOSED: null,
  CANCELLED: null,
  ON_HOLD: null,
  FAILED_ATTEMPT: null
};

type ActionModal =
  | { type: "HOLD"; remarks: string; eventAt: string }
  | { type: "FAILED"; remarks: string; eventAt: string }
  | null;

const toLocalInput = (iso?: string | null) => {
  if (!iso) return "";
  const date = new Date(iso);
  const offset = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
};

const fromLocalInput = (value: string) => {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
};

export default function MyTripDetailPage() {
  const { id } = useParams();
  const nav = useNavigate();
  const { toasts, show } = useToast();

  const [loading, setLoading] = useState(true);
  const [trip, setTrip] = useState<DispatchTripDetail | null>(null);
  const [generatedWaybill, setGeneratedWaybill] = useState<GeneratedWaybill | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [locationSending, setLocationSending] = useState(false);
  const [liveLocationEnabled, setLiveLocationEnabled] = useState(false);
  const [modal, setModal] = useState<ActionModal>(null);
  const fileInputs = useRef<Partial<Record<TripDocumentType, HTMLInputElement | null>>>({});
  const liveLocationManualOptOut = useRef(false);
  const isNative = Capacitor.isNativePlatform();

  const fetchTrip = async () => {
    if (!id) return;
    try {
      setLoading(true);
      const detail = await api<DispatchTripDetail>(`/api/dispatch/my-trips/${id}`, { method: "GET" });
      setTrip(detail);
      const waybill = await apiOptional<GeneratedWaybill>(`/api/dispatch/trips/${id}/waybill`, { method: "GET" });
      setGeneratedWaybill(waybill);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load trip detail.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTrip();
  }, [id]);

  const nextAction = useMemo(() => {
    if (!trip) return null;
    return driverActionMap[trip.status];
  }, [trip]);

  const plannedStops = useMemo(() => {
    if (!trip) return { pickup: null, dropoff: null };
    const pickup = trip.stops.find((stop) => stop.stopType === "PICKUP") ?? null;
    const dropoff = trip.stops.find((stop) => stop.stopType === "DROPOFF") ?? null;
    return { pickup, dropoff };
  }, [trip]);

  const canShareLocation = trip ? locationSharingStatuses.includes(trip.status) : false;

  useEffect(() => {
    liveLocationManualOptOut.current = false;
    setLiveLocationEnabled(false);
  }, [id]);

  useEffect(() => {
    if (!canShareLocation || !trip?.id || liveLocationManualOptOut.current) {
      return;
    }

    setLiveLocationEnabled(true);
  }, [canShareLocation, trip?.id]);

  const sendCurrentLocation = useCallback(
    async (silent = false) => {
      if (!trip?.id) return false;
      if (!canShareLocation) {
        setLiveLocationEnabled(false);
        if (!silent) {
          show("Location sharing is available only for active assigned trips.", "error");
        }
        return false;
      }

      try {
        setLocationSending(true);
        const fix = await getBrowserLocationFix();
        const payload: DispatchTripLocationPingRequest = {
          latitude: fix.latitude,
          longitude: fix.longitude,
          accuracyMeters: fix.accuracyMeters,
          recordedAt: fix.recordedAt
        };
        const ping = await api<DispatchTripLocationPingResponse>(`/api/dispatch/trips/${trip.id}/location`, {
          method: "POST",
          body: JSON.stringify(payload)
        });
        setTrip((current) =>
          current?.id === ping.tripId
            ? {
                ...current,
                latestDriverLocation: {
                  latitude: ping.latitude,
                  longitude: ping.longitude,
                  accuracyMeters: ping.accuracyMeters,
                  recordedAt: ping.recordedAt
                }
              }
            : current
        );
        if (!silent) {
          show("Location sent.", "success");
        }
        return true;
      } catch (e: any) {
        console.error(e);
        setLiveLocationEnabled(false);
        if (!silent) {
          show(e?.message ?? "Failed to send current location.", "error");
        }
        return false;
      } finally {
        setLocationSending(false);
      }
    },
    [canShareLocation, show, trip?.id]
  );

  useEffect(() => {
    if (!liveLocationEnabled || !canShareLocation) return;
    void sendCurrentLocation(true);
    const interval = window.setInterval(() => {
      void sendCurrentLocation(true);
    }, 60000);
    return () => window.clearInterval(interval);
  }, [canShareLocation, liveLocationEnabled, sendCurrentLocation]);

  useEffect(() => {
    if (!canShareLocation) {
      setLiveLocationEnabled(false);
    }
  }, [canShareLocation]);

  const toggleLiveLocation = () => {
    setLiveLocationEnabled((value) => {
      const next = !value;
      liveLocationManualOptOut.current = !next;
      return next;
    });
  };

  const handleDriverAction = async (
    endpoint: string,
    remarks?: string | null,
    eventAt?: string | null
  ) => {
    if (!trip) return;
    try {
      setActionLoading(true);
      const eventAtValue = eventAt ?? new Date().toISOString();
      await api(`/api/dispatch/trips/${trip.id}/${endpoint}`, {
        method: "POST",
        body: JSON.stringify({
          eventAt: eventAtValue,
          rowVersion: trip.rowVersion,
          remarks: remarks ?? null
        })
      });
      show("Trip updated.", "success");
      await fetchTrip();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to update trip.", "error");
    } finally {
      setActionLoading(false);
    }
  };

  const handleUploadDoc = async (docType: TripDocumentType, storageKey: string) => {
    if (!trip) return;
    try {
      setActionLoading(true);
      await api(`/api/dispatch/trips/${trip.id}/documents`, {
        method: "POST",
        body: JSON.stringify({ type: docType, storageKey })
      });
      show("Document uploaded.", "success");
      await fetchTrip();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to upload document.", "error");
    } finally {
      setActionLoading(false);
    }
  };

  const handleFileUpload = (docType: TripDocumentType, files: FileList | null) => {
    const file = files?.[0];
    if (!file) return;
    void handleUploadFile(docType, file);
  };

  const handleUploadFile = async (docType: TripDocumentType, file: File) => {
    await handleUploadDoc(docType, file.name);
  };

  const handleUploadClick = async (docType: TripDocumentType) => {
    if (isNative) {
      const file = await captureDocumentPhoto();
      if (file) {
        await handleUploadFile(docType, file);
        return;
      }
    }

    fileInputs.current[docType]?.click();
  };

  const canUploadDocument = (docType: TripDocumentType) => {
    return trip ? canDriverUploadDocument(docType, trip.status) : false;
  };

  const documentHint = (docType: TripDocumentType) => {
    if (!trip) return null;
    if (docType === "ATW") {
      const atwState = getDocumentState(trip.documents, "ATW");
      if (atwState === "MISSING") return "Upload a clear ATW document for this trip.";
      if (atwState === "REJECTED") return "ATW was rejected. Upload a clearer copy.";
      if (atwState === "UPLOADED") return "Waiting for dispatcher verification.";
      return null;
    }
    if (docType === "WAYBILL") {
      return generatedWaybill ? "Waybill ready" : "Waybill not yet generated";
    }
    if ((docType === "EIR" || docType === "GATE_PASS") && !isStatusAtLeast(trip.status, "AT_PICKUP")) {
      return "Awaiting pickup";
    }
    if (docType === "DR" && !isStatusAtLeast(trip.status, "AT_DROPOFF")) {
      return "Awaiting dropoff";
    }
    if (docType === "POD" && !isStatusAtLeast(trip.status, "DELIVERED")) {
      return "Available after delivery";
    }
    return null;
  };

  if (loading) {
    return (
      <div className="space-y-6">
        <PageHeader title="My Trip" description="Loading trip detail..." />
        <LoadingSkeleton rows={8} />
      </div>
    );
  }

  if (!trip) {
    return <EmptyState title="Trip not found" description="The trip detail could not be loaded." />;
  }

  const isFailedAttempt = trip.status === "FAILED_ATTEMPT";
  const atwState = getDocumentState(trip.documents, "ATW");
  const showAtwWarning = isDocumentAttentionState(atwState);
  const canUploadAtw = canUploadDocument("ATW");
  const primaryUploadType = getPrimaryDriverUploadType(trip);
  const showDocumentCaptureCard = !isFailedAttempt && !(showAtwWarning && canUploadAtw);
  const primaryUploadDoc = primaryUploadType
    ? trip.documents.find((doc) => doc.type === primaryUploadType)
    : null;
  const primaryUploadHint = primaryUploadType
    ? documentHint(primaryUploadType) ?? "Capture a clear photo or upload the document file for this trip."
    : "Document capture unlocks as the trip reaches pickup, dropoff, or delivered status.";
  const latestLocation = trip.latestDriverLocation;

  return (
    <div className="space-y-6 pb-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title={`Trip ${trip.id.slice(0, 8)}`}
        description={`Customer: ${trip.customer?.name ?? "-"}`}
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/dispatch/board" className="text-muted-foreground hover:text-foreground">
              Dispatch
            </Link>
            <span className="text-muted-foreground">/</span>
            <Link to="/dispatch/my-trips" className="text-muted-foreground hover:text-foreground">
              My Trips
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">Trip {trip.id.slice(0, 8)}</span>
          </nav>
        }
        actions={
          <Button variant="outline" onClick={() => nav("/dispatch/my-trips")}>
            Back
          </Button>
        }
      />

      {showAtwWarning ? (
        <div className="space-y-3">
          <div className="flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm font-medium text-amber-900">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              ATW is {formatDocumentStateLabel(atwState).toLowerCase()}.{" "}
              {canUploadAtw
                ? "Upload a clear ATW document before continuing."
                : "A dispatcher or manager must resolve this before the trip can continue."}
            </span>
          </div>
          {canUploadAtw ? (
            <Button
              className="h-12 w-full gap-2 text-base font-semibold"
              disabled={actionLoading}
              onClick={() => void handleUploadClick("ATW")}
            >
              {isNative ? <Camera className="h-5 w-5" /> : <FileUp className="h-5 w-5" />}
              {isNative ? "Capture ATW" : "Upload ATW"}
            </Button>
          ) : null}
        </div>
      ) : null}

      <div className="surface-card border-primary/20 p-4 md:p-6">
        <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-primary">Current Status</p>
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
              {trip.podPending ? (
                <span className="rounded-full border border-amber-200 bg-amber-50 px-2 py-1 text-xs font-semibold text-amber-700">
                  POD pending
                </span>
              ) : null}
            </div>
            <p className="mt-2 text-sm text-muted-foreground">
              {nextAction ? `Next required action: ${nextAction.label}.` : getDriverTripNextAction(trip)}
            </p>
          </div>
          {isFailedAttempt ? (
            <div className="rounded-2xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm font-medium text-rose-900 md:max-w-md">
              This trip is blocked. A dispatcher or manager must resolve the failed attempt before you can continue.
            </div>
          ) : nextAction && operationalFlow.includes(trip.status) ? (
            <Button
              className="h-12 w-full text-base font-semibold md:w-auto"
              onClick={() => handleDriverAction(nextAction.endpoint)}
              disabled={actionLoading}
            >
              {nextAction.label}
            </Button>
          ) : null}
        </div>
        {!isFailedAttempt &&
        (driverHoldEligible.includes(trip.status) || failedAttemptEligible.includes(trip.status)) ? (
          <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-2">
            {driverHoldEligible.includes(trip.status) ? (
              <Button
                variant="outline"
                size="sm"
                className="h-11"
                onClick={() =>
                  setModal({
                    type: "HOLD",
                    remarks: "",
                    eventAt: toLocalInput(new Date().toISOString())
                  })
                }
                disabled={actionLoading}
              >
                Request Hold
              </Button>
            ) : null}
            {failedAttemptEligible.includes(trip.status) ? (
              <Button
                variant="outline"
                size="sm"
                className="h-11"
                onClick={() =>
                  setModal({
                    type: "FAILED",
                    remarks: "",
                    eventAt: toLocalInput(new Date().toISOString())
                  })
                }
                disabled={actionLoading}
              >
                Report Failed Attempt
              </Button>
            ) : null}
          </div>
        ) : null}
      </div>

      <div className="surface-card p-6">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">Trip</p>
            <h2 className="mt-2 text-2xl font-semibold text-foreground">
              {trip.id.slice(0, 8)}
            </h2>
            <p className="mt-2 text-sm text-muted-foreground">
              Customer: {trip.customer?.name ?? "-"}
            </p>
            <p className="text-sm text-muted-foreground">Truck: {trip.truckAssetCode ?? "-"}</p>
          </div>
          <div className="flex items-center gap-3">
            <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
            {trip.podPending ? (
              <span className="rounded-full border border-amber-200 bg-amber-50 px-2 py-1 text-xs font-semibold text-amber-700">
                POD pending
              </span>
            ) : null}
          </div>
        </div>
      </div>

      <div className="surface-card p-4 md:p-6">
        <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-primary">Trip Location</p>
            <h3 className="mt-2 text-base font-semibold text-foreground">Map View</h3>
            <p className="mt-1 text-sm text-muted-foreground">
              Live sharing starts automatically while this trip page is open and the trip is active.
            </p>
          </div>
          <div className="flex flex-col gap-2 sm:flex-row">
            <Button
              variant="outline"
              className="h-11 gap-2"
              disabled={!canShareLocation || locationSending}
              onClick={() => void sendCurrentLocation(false)}
            >
              <MapPin className="h-4 w-4" />
              {locationSending ? "Sending..." : "Send Current Location"}
            </Button>
            <Button
              variant={liveLocationEnabled ? "default" : "outline"}
              className="h-11"
              disabled={!canShareLocation}
              onClick={toggleLiveLocation}
            >
              {liveLocationEnabled ? "Live Sharing On" : "Share While Open"}
            </Button>
          </div>
        </div>
        <div className="mt-4">
          <TripMap
            pickup={{
              latitude: plannedStops.pickup?.latitude,
              longitude: plannedStops.pickup?.longitude,
              label: plannedStops.pickup?.locationText ?? "Pickup",
              detail: plannedStops.pickup?.scheduledAt
                ? new Date(plannedStops.pickup.scheduledAt).toLocaleString()
                : null
            }}
            dropoff={{
              latitude: plannedStops.dropoff?.latitude,
              longitude: plannedStops.dropoff?.longitude,
              label: plannedStops.dropoff?.locationText ?? "Dropoff",
              detail: plannedStops.dropoff?.scheduledAt
                ? new Date(plannedStops.dropoff.scheduledAt).toLocaleString()
                : null
            }}
            driver={
              latestLocation
                ? {
                    latitude: latestLocation.latitude,
                    longitude: latestLocation.longitude,
                    label: "My latest location",
                    detail: trip.truckAssetCode ? `Truck ${trip.truckAssetCode}` : null
                  }
                : null
            }
            driverRecordedAt={latestLocation?.recordedAt}
            driverAccuracyMeters={latestLocation?.accuracyMeters}
            emptyTitle="No trip coordinates yet"
            heightClassName="h-[20rem]"
          />
        </div>
      </div>

      {showDocumentCaptureCard ? (
        <div className="surface-card border-primary/20 p-4 md:p-6">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-primary">
                Driver Document Capture
              </p>
              <h3 className="mt-2 text-base font-semibold text-foreground">
                {primaryUploadType ? formatDocumentLabel(primaryUploadType) : "No document ready to upload"}
              </h3>
              <p className="mt-1 text-sm text-muted-foreground">{primaryUploadHint}</p>
            </div>
            {primaryUploadType ? (
              <Button
                className="h-12 w-full gap-2 sm:w-auto"
                disabled={actionLoading}
                onClick={() => void handleUploadClick(primaryUploadType)}
              >
                {isNative ? <Camera className="h-5 w-5" /> : <FileUp className="h-5 w-5" />}
                {`${isNative ? "Capture" : primaryUploadDoc ? "Replace" : "Upload"} ${formatDocumentLabel(
                  primaryUploadType
                )}`}
              </Button>
            ) : null}
          </div>
        </div>
      ) : null}

      <div className="surface-card p-6">
        <h3 className="text-sm font-semibold">Stops</h3>
        <div className="mt-4 grid gap-4 md:grid-cols-2">
          <div className="rounded-xl border border-border/60 bg-muted/20 px-4 py-4">
            <p className="text-xs uppercase text-muted-foreground">Pickup</p>
            <p className="mt-2 text-sm font-semibold text-foreground">
              {plannedStops.pickup?.locationText ?? "-"}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              Scheduled:{" "}
              {plannedStops.pickup?.scheduledAt
                ? new Date(plannedStops.pickup.scheduledAt).toLocaleString()
                : "Unscheduled"}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              Arrived:{" "}
              {plannedStops.pickup?.actualAt
                ? new Date(plannedStops.pickup.actualAt).toLocaleString()
                : "--"}
            </p>
          </div>
          <div className="rounded-xl border border-border/60 bg-muted/20 px-4 py-4">
            <p className="text-xs uppercase text-muted-foreground">Dropoff</p>
            <p className="mt-2 text-sm font-semibold text-foreground">
              {plannedStops.dropoff?.locationText ?? "-"}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              Scheduled:{" "}
              {plannedStops.dropoff?.scheduledAt
                ? new Date(plannedStops.dropoff.scheduledAt).toLocaleString()
                : "Unscheduled"}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              Arrived:{" "}
              {plannedStops.dropoff?.actualAt
                ? new Date(plannedStops.dropoff.actualAt).toLocaleString()
                : "--"}
            </p>
          </div>
        </div>
      </div>

      <div className="surface-card p-6">
        <div className="flex items-center justify-between gap-4">
          <div>
            <h3 className="text-sm font-semibold">Documents</h3>
            <p className="mt-1 text-xs text-muted-foreground">
              Uploads create new versions. Drivers do not verify documents.
            </p>
          </div>
        </div>
        <div className="mt-4 space-y-3 text-sm">
          {visibleDocTypes.map((type) => {
            const doc = trip.documents.find((d) => d.type === type);
            const state = type === "WAYBILL" ? (generatedWaybill ? "READY" : "PENDING") : doc?.state ?? "MISSING";
            const hint = documentHint(type);
            const canUpload = canUploadDocument(type);
            return (
              <div key={type} className="rounded-2xl border border-border/60 bg-muted/10 px-4 py-3">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <p className="text-sm font-semibold text-foreground">{formatDocumentLabel(type)}</p>
                    <p className="text-xs text-muted-foreground">Status: {formatDocumentStateLabel(state)}</p>
                    {hint ? (
                      <p className="mt-1 text-xs text-amber-600">{hint}</p>
                    ) : null}
                  </div>
                  <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center">
                    {type === "WAYBILL" && generatedWaybill ? (
                      <Button
                        variant="outline"
                        size="sm"
                        className="h-11 w-full sm:h-9 sm:w-auto"
                        onClick={() => window.print()}
                      >
                        View
                      </Button>
                    ) : doc?.storageKey ? (
                      <Button
                        variant="outline"
                        size="sm"
                        className="h-11 w-full sm:h-9 sm:w-auto"
                        onClick={() => window.open(doc.storageKey, "_blank", "noopener,noreferrer")}
                      >
                        View
                      </Button>
                    ) : null}
                    {uploadableDocTypes.includes(type) && canUpload ? (
                      <>
                        <Button
                          variant="outline"
                          size="sm"
                          className="h-12 w-full gap-2 sm:h-9 sm:w-auto"
                          disabled={actionLoading}
                          onClick={() => void handleUploadClick(type)}
                        >
                          {isNative ? <Camera className="h-4 w-4" /> : <FileUp className="h-4 w-4" />}
                          {isNative ? "Photo" : doc ? "Replace" : "Upload"}
                        </Button>
                        <input
                          ref={(el) => {
                            fileInputs.current[type] = el;
                          }}
                          type="file"
                          accept="image/*,.pdf"
                          className="hidden"
                          onChange={(e) => {
                            handleFileUpload(type, e.target.files);
                            e.currentTarget.value = "";
                          }}
                        />
                      </>
                    ) : null}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {modal ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,520px)] rounded-2xl border border-slate-200 bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Action Required</p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">
                  {modal.type === "HOLD" ? "Request Hold" : "Report Failed Attempt"}
                </h2>
              </div>
              <button
                onClick={() => setModal(null)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>

            <div className="mt-5 space-y-2 text-sm">
              <label className="text-xs uppercase text-slate-500">Event Time</label>
              <input
                type="datetime-local"
                value={modal.eventAt}
                onChange={(e) => setModal({ ...modal, eventAt: e.target.value })}
                className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
              />
              <label className="text-xs uppercase text-slate-500">Remarks</label>
              <textarea
                value={modal.remarks}
                onChange={(e) => setModal({ ...modal, remarks: e.target.value })}
                className="mt-2 min-h-[100px] w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm"
                placeholder="Add remarks"
              />
            </div>

            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setModal(null)}>
                Cancel
              </Button>
              <Button
                onClick={() => {
                  if (!modal) return;
                  if (!modal.remarks.trim()) {
                    show("Remarks are required.", "error");
                    return;
                  }
                  const eventAt = fromLocalInput(modal.eventAt) ?? new Date().toISOString();
                  handleDriverAction(
                    modal.type === "HOLD" ? "request-hold" : "report-failure",
                    modal.remarks.trim(),
                    eventAt
                  );
                  setModal(null);
                }}
              >
                Confirm
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function isStatusAtLeast(current: TripStatus, required: TripStatus) {
  const order: TripStatus[] = [
    "DRAFT",
    "DISPATCHED",
    "ENROUTE_PICKUP",
    "AT_PICKUP",
    "LOADED",
    "ENROUTE_DROPOFF",
    "AT_DROPOFF",
    "DELIVERED",
    "CLOSED"
  ];
  return order.indexOf(current) >= order.indexOf(required);
}
