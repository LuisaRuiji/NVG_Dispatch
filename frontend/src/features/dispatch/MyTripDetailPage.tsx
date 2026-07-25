import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api, apiOptional } from "@/lib/api";
import { captureDocumentPhoto } from "@/lib/camera";
import type { DispatchTripDetail, GeneratedWaybill, TripDocumentType, TripStatus } from "./types";
import { operationalFlow, statusLabels } from "./types";
import {
  canDriverUploadDocument,
  formatDocumentLabel,
  formatDocumentStateLabel,
  getDocumentState,
  getDriverTripNextAction,
  isDocumentAttentionState
} from "./driverTripUi";
import { Capacitor } from "@capacitor/core";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  AlertTriangle,
  Camera,
  FileUp,
  MapPin,
  Clock,
  ArrowLeft,
  Navigation,
  Eye,
  CheckCircle,
  AlertCircle
} from "lucide-react";

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
  const [modal, setModal] = useState<ActionModal>(null);
  const [mapOpen, setMapOpen] = useState(false);
  
  const fileInputs = useRef<Partial<Record<TripDocumentType, HTMLInputElement | null>>>({});
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
      void fetchTrip(); // Automatically pull down latest database state and rowVersion
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
      show("Document uploaded successfully.", "success");
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
      <div className="space-y-6 max-w-4xl mx-auto">
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

  return (
    <div className="space-y-6 pb-12 max-w-4xl mx-auto">
      <ToastHost toasts={toasts} />
      <PageHeader
        title={`Trip Details`}
        description={`Active scheduling, stops, and document uploads.`}
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <span className="text-muted-foreground">Driver Portal</span>
            <span className="text-muted-foreground">/</span>
            <Link to="/dispatch/my-trips" className="text-muted-foreground hover:text-foreground font-medium">
              My Trips
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground font-semibold">Trip {trip.id.slice(0, 8).toUpperCase()}</span>
          </nav>
        }
        actions={
          <div className="flex gap-2">
            <Button
              variant="outline"
              onClick={() => nav("/dispatch/my-route-map")}
              className="gap-1.5 font-semibold text-primary hover:bg-slate-50 rounded-xl"
            >
              <Navigation className="h-4 w-4" />
              My Route Map
            </Button>
            <Button
              variant="outline"
              onClick={() => nav("/dispatch/my-trips")}
              className="gap-1.5 rounded-xl font-medium"
            >
              <ArrowLeft className="h-4 w-4" />
              Back
            </Button>
          </div>
        }
      />

      {showAtwWarning && (
        <div className="space-y-3">
          <div className="flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50/70 px-4 py-3.5 text-sm font-medium text-amber-900">
            <AlertTriangle className="mt-0.5 h-4.5 w-4.5 shrink-0 text-amber-600" />
            <div className="space-y-0.5">
              <p className="font-semibold text-amber-800">ATW Attention Needed</p>
              <p className="text-xs text-amber-700">
                ATW is {formatDocumentStateLabel(atwState).toLowerCase()}.{" "}
                {canUploadAtw
                  ? "Upload a clear ATW document before continuing."
                  : "A dispatcher or manager must resolve this before the trip can continue."}
              </p>
            </div>
          </div>
          {canUploadAtw && (
            <Button
              className="h-12 w-full gap-2 text-base font-semibold rounded-2xl"
              disabled={actionLoading}
              onClick={() => void handleUploadClick("ATW")}
            >
              {isNative ? <Camera className="h-5 w-5" /> : <FileUp className="h-5 w-5" />}
              {isNative ? "Capture ATW" : "Upload ATW"}
            </Button>
          )}
        </div>
      )}

      {/* Main Status & Controls Card */}
      <div className="grid gap-6 md:grid-cols-[1fr_260px]">
        <div className="space-y-6">
          <div className="rounded-2xl border bg-card p-6 shadow-sm flex flex-col md:flex-row md:items-center md:justify-between gap-4">
            <div className="space-y-1">
              <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Current Lifecycle State</p>
              <div className="flex items-center gap-2 pt-1">
                <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
                {trip.podPending && (
                  <span className="rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-xs font-semibold text-amber-700">
                    POD Pending Verification
                  </span>
                )}
              </div>
              <p className="text-xs text-slate-500 pt-1">
                {nextAction ? `Required action: ${nextAction.label}.` : getDriverTripNextAction(trip)}
              </p>
            </div>

            <div className="flex flex-col gap-2 shrink-0">
              {isFailedAttempt ? (
                <div className="rounded-xl border border-rose-200 bg-rose-50/70 px-4 py-2.5 text-xs font-medium text-rose-900 max-w-[240px]">
                  This trip is blocked due to a failed attempt. Dispatch team has been notified.
                </div>
              ) : nextAction && operationalFlow.includes(trip.status) ? (
                <Button
                  className="h-11 px-6 font-semibold bg-primary hover:bg-primary/95 text-primary-foreground rounded-xl shadow-md shadow-primary/10"
                  onClick={() => handleDriverAction(nextAction.endpoint)}
                  disabled={actionLoading}
                >
                  {nextAction.label}
                </Button>
              ) : null}

              {!isFailedAttempt && (driverHoldEligible.includes(trip.status) || failedAttemptEligible.includes(trip.status)) && (
                <div className="flex gap-2">
                  {driverHoldEligible.includes(trip.status) && (
                    <Button
                      variant="outline"
                      size="sm"
                      className="rounded-xl font-semibold flex-1 border-slate-200 hover:bg-slate-50"
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
                  )}
                  {failedAttemptEligible.includes(trip.status) && (
                    <Button
                      variant="outline"
                      size="sm"
                      className="rounded-xl font-semibold flex-1 border-slate-200 text-rose-600 hover:bg-rose-50/30 hover:border-rose-200"
                      onClick={() =>
                        setModal({
                          type: "FAILED",
                          remarks: "",
                          eventAt: toLocalInput(new Date().toISOString())
                        })
                      }
                      disabled={actionLoading}
                    >
                      Fail Attempt
                    </Button>
                  )}
                </div>
              )}
            </div>
          </div>

          {/* Visual Vertical Stops Timeline */}
          <div className="rounded-2xl border bg-card p-6 shadow-sm">
            <h3 className="text-sm font-bold text-slate-800 uppercase tracking-wider mb-5">Scheduled Stops & Sequence</h3>
            
            <div className="relative pl-6 space-y-8 border-l border-slate-200 ml-3">
              {/* Pickup Stop */}
              <div className="relative">
                <div className="absolute -left-9 top-0.5 bg-blue-500 text-white rounded-full p-1.5 shadow-sm">
                  <MapPin className="h-4 w-4" />
                </div>
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="text-xs uppercase font-semibold text-blue-600 bg-blue-50 px-2 py-0.5 rounded">Stop 1: Pickup</span>
                    {plannedStops.pickup?.actualAt && (
                      <span className="text-[10px] font-bold text-emerald-600 bg-emerald-50 px-1.5 py-0.5 rounded flex items-center gap-0.5">
                        <CheckCircle className="h-2.5 w-2.5" /> Arrived
                      </span>
                    )}
                  </div>
                  <h4 className="text-base font-bold text-slate-800 pt-0.5">{plannedStops.pickup?.locationText ?? "-"}</h4>
                  <div className="grid grid-cols-2 gap-4 pt-1.5 text-xs text-slate-500">
                    <div className="flex items-center gap-1.5">
                      <Clock className="h-3.5 w-3.5 text-slate-400" />
                      <span>Scheduled: {plannedStops.pickup?.scheduledAt ? new Date(plannedStops.pickup.scheduledAt).toLocaleString() : "Unscheduled"}</span>
                    </div>
                    {plannedStops.pickup?.actualAt && (
                      <div className="flex items-center gap-1.5">
                        <CheckCircle className="h-3.5 w-3.5 text-emerald-500" />
                        <span>Arrived at: {new Date(plannedStops.pickup.actualAt).toLocaleString()}</span>
                      </div>
                    )}
                  </div>
                </div>
              </div>

              {/* Dropoff Stop */}
              <div className="relative">
                <div className="absolute -left-9 top-0.5 bg-rose-500 text-white rounded-full p-1.5 shadow-sm">
                  <MapPin className="h-4 w-4" />
                </div>
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="text-xs uppercase font-semibold text-rose-600 bg-rose-50 px-2 py-0.5 rounded">Stop 2: Dropoff</span>
                    {plannedStops.dropoff?.actualAt && (
                      <span className="text-[10px] font-bold text-emerald-600 bg-emerald-50 px-1.5 py-0.5 rounded flex items-center gap-0.5">
                        <CheckCircle className="h-2.5 w-2.5" /> Arrived
                      </span>
                    )}
                  </div>
                  <h4 className="text-base font-bold text-slate-800 pt-0.5">{plannedStops.dropoff?.locationText ?? "-"}</h4>
                  <div className="grid grid-cols-2 gap-4 pt-1.5 text-xs text-slate-500">
                    <div className="flex items-center gap-1.5">
                      <Clock className="h-3.5 w-3.5 text-slate-400" />
                      <span>Scheduled: {plannedStops.dropoff?.scheduledAt ? new Date(plannedStops.dropoff.scheduledAt).toLocaleString() : "Unscheduled"}</span>
                    </div>
                    {plannedStops.dropoff?.actualAt && (
                      <div className="flex items-center gap-1.5">
                        <CheckCircle className="h-3.5 w-3.5 text-emerald-500" />
                        <span>Arrived at: {new Date(plannedStops.dropoff.actualAt).toLocaleString()}</span>
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Document Checklist Card */}
          <div className="rounded-2xl border bg-card p-6 shadow-sm">
            <div className="space-y-1 pb-4 mb-4 border-b">
              <h3 className="text-sm font-bold text-slate-800 uppercase tracking-wider">Required Document Workflow</h3>
              <p className="text-xs text-muted-foreground">Upload files or capture photos. Uploading replaces the previous copy.</p>
            </div>
            
            <div className="space-y-3">
              {visibleDocTypes.map((type) => {
                const doc = trip.documents.find((d) => d.type === type);
                const state = type === "WAYBILL" ? (generatedWaybill ? "READY" : "PENDING") : doc?.state ?? "MISSING";
                const hint = documentHint(type);
                const canUpload = canUploadDocument(type);
                const isPOD = type === "POD";

                return (
                  <div
                    key={type}
                    className={`rounded-xl border p-4 transition-all duration-300 ${
                      isPOD && trip.status === "DELIVERED"
                        ? "border-emerald-200 bg-emerald-50/40"
                        : "bg-slate-50/50"
                    }`}
                  >
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                      <div className="space-y-0.5">
                        <p className="text-sm font-bold text-slate-800 flex items-center gap-1.5">
                          {formatDocumentLabel(type)}
                          {isPOD && trip.status === "DELIVERED" && (
                            <span className="text-[10px] font-semibold text-emerald-700 bg-emerald-100/70 px-1.5 py-0.5 rounded">Action Required</span>
                          )}
                        </p>
                        <p className="text-xs text-muted-foreground flex items-center gap-1">
                          State: 
                          <span className={`font-semibold ${
                            state === "VERIFIED" ? "text-emerald-600" :
                            state === "REJECTED" ? "text-rose-600" :
                            state === "UPLOADED" ? "text-indigo-600" : "text-amber-600"
                          }`}>
                            {formatDocumentStateLabel(state)}
                          </span>
                        </p>
                        {hint && <p className="text-xs text-amber-600 font-medium pt-0.5">{hint}</p>}
                      </div>

                      <div className="flex gap-2 shrink-0">
                        {type === "WAYBILL" && generatedWaybill ? (
                          <Button
                            variant="outline"
                            size="sm"
                            className="rounded-lg h-9 font-medium"
                            onClick={() => window.print()}
                          >
                            <Eye className="h-4 w-4 mr-1" /> Print / View
                          </Button>
                        ) : doc?.storageKey ? (
                          <Button
                            variant="outline"
                            size="sm"
                            className="rounded-lg h-9 font-medium"
                            onClick={() => window.open(doc.storageKey, "_blank", "noopener,noreferrer")}
                          >
                            <Eye className="h-4 w-4 mr-1" /> View Copy
                          </Button>
                        ) : null}

                        {uploadableDocTypes.includes(type) && canUpload ? (
                          <>
                            <Button
                              variant="outline"
                              size="sm"
                              disabled={actionLoading}
                              onClick={() => void handleUploadClick(type)}
                              className={`rounded-lg h-9 font-semibold ${
                                isPOD && trip.status === "DELIVERED"
                                  ? "border-emerald-300 text-emerald-700 hover:bg-emerald-50"
                                  : "border-slate-200 hover:bg-slate-50"
                              }`}
                            >
                              {isNative ? <Camera className="h-4 w-4 mr-1" /> : <FileUp className="h-4 w-4 mr-1" />}
                              {isNative ? "Camera" : doc ? "Replace" : "Upload"}
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
        </div>

        {/* Sidebar Info Panel */}
        <div className="space-y-6">
          <Card className="rounded-2xl border bg-card shadow-sm">
            <CardHeader className="p-4 pb-2 border-b">
              <CardTitle className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">General Info</CardTitle>
            </CardHeader>
            <CardContent className="p-4 space-y-4 text-sm">
              <div className="space-y-0.5">
                <p className="text-xs text-muted-foreground uppercase font-semibold">Container Number</p>
                <p className="font-bold text-slate-800">{trip.containerNumber ?? "-"}</p>
              </div>

              <div className="space-y-0.5">
                <p className="text-xs text-muted-foreground uppercase font-semibold">EIR Number</p>
                <p className="font-bold text-slate-800">{trip.eirNumber ?? "-"}</p>
              </div>

              <div className="space-y-0.5">
                <p className="text-xs text-muted-foreground uppercase font-semibold">Booking Reference</p>
                <p className="font-bold text-slate-800">{trip.bookingNumber ?? "-"}</p>
              </div>

              <div className="space-y-0.5">
                <p className="text-xs text-muted-foreground uppercase font-semibold">Shipping Line</p>
                <p className="font-bold text-slate-800">{trip.shippingLine ?? "-"}</p>
              </div>

              <div className="space-y-0.5">
                <p className="text-xs text-muted-foreground uppercase font-semibold">Truck Details</p>
                <p className="font-bold text-slate-800">{trip.truckAssetCode ?? "-"}</p>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>

      {/* hold / failure remark modals */}
      {modal && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,520px)] rounded-2xl border bg-card p-6 shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4 pb-4 border-b">
              <div className="space-y-1">
                <p className="text-xs uppercase tracking-wider text-muted-foreground">Remarks Required</p>
                <h2 className="text-lg font-bold text-slate-900">
                  {modal.type === "HOLD" ? "Request Trip Hold" : "Report Failed Attempt"}
                </h2>
              </div>
              <button
                onClick={() => setModal(null)}
                className="text-xs text-slate-500 hover:text-slate-900 border px-2.5 py-1 rounded-lg"
              >
                Close
              </button>
            </div>

            <div className="mt-5 space-y-4 text-sm">
              <div className="space-y-1">
                <label className="text-xs font-semibold uppercase text-slate-500">Event Occurred Time</label>
                <input
                  type="datetime-local"
                  value={modal.eventAt}
                  onChange={(e) => setModal({ ...modal, eventAt: e.target.value })}
                  className="h-10 w-full rounded-xl border bg-background px-3 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                />
              </div>

              <div className="space-y-1">
                <label className="text-xs font-semibold uppercase text-slate-500">Remarks & Explanation</label>
                <textarea
                  value={modal.remarks}
                  onChange={(e) => setModal({ ...modal, remarks: e.target.value })}
                  className="min-h-[100px] w-full rounded-xl border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                  placeholder="Provide details about the hold or failure..."
                />
              </div>
            </div>

            <div className="mt-6 flex justify-end gap-3 pt-4 border-t">
              <Button variant="outline" className="rounded-lg" onClick={() => setModal(null)}>
                Cancel
              </Button>
              <Button
                className="rounded-lg"
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
                Confirm Submit
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* My Route Map Modal (Premium Mock GPS Tracker) */}
      {mapOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,600px)] rounded-2xl border bg-card p-6 shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4 pb-4 border-b">
              <div className="space-y-1">
                <p className="text-xs uppercase tracking-wider text-indigo-600 font-bold flex items-center gap-1">
                  <Navigation className="h-3.5 w-3.5 text-indigo-500 animate-spin" />
                  My Route Map
                </p>
                <h2 className="text-lg font-bold text-slate-900">
                  Live Dispatch Navigation
                </h2>
              </div>
              <button
                onClick={() => setMapOpen(false)}
                className="text-xs font-semibold text-slate-500 hover:text-slate-900 border px-2.5 py-1 rounded-lg"
              >
                Close
              </button>
            </div>

            <div className="mt-5 space-y-4">
              <div className="relative w-full h-80 rounded-2xl overflow-hidden bg-slate-950 flex items-center justify-center border shadow-inner">
                {/* SVG Mock Map Layout */}
                <svg className="w-full h-full" viewBox="0 0 400 300">
                  <defs>
                    <linearGradient id="routeGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stopColor="#4f46e5" />
                      <stop offset="100%" stopColor="#818cf8" />
                    </linearGradient>
                  </defs>

                  {/* Street grid pattern simulation */}
                  <path d="M 0 40 L 400 40 M 0 100 L 400 100 M 0 160 L 400 160 M 0 220 L 400 220 M 0 280 L 400 280" stroke="#1e293b" strokeWidth="1" opacity="0.4" />
                  <path d="M 40 0 L 40 300 M 110 0 L 110 300 M 180 0 L 180 300 M 250 0 L 250 300 M 320 0 L 320 300" stroke="#1e293b" strokeWidth="1" opacity="0.4" />
                  <path d="M 0 0 L 400 300" stroke="#0f172a" strokeWidth="6" opacity="0.3" />

                  {/* Highlighted Route Path (Dashed with gliding dash offset animation) */}
                  <path
                    d="M 60 220 Q 180 60, 240 240 T 340 80"
                    fill="none"
                    stroke="url(#routeGrad)"
                    strokeWidth="5"
                    strokeLinecap="round"
                    className="opacity-90"
                  />
                  <path
                    d="M 60 220 Q 180 60, 240 240 T 340 80"
                    fill="none"
                    stroke="#ffffff"
                    strokeWidth="5"
                    strokeLinecap="round"
                    strokeDasharray="10, 15"
                    className="opacity-40"
                    style={{
                      strokeDashoffset: 100,
                      animation: "dash 8s linear infinite"
                    }}
                  />

                  {/* Visual key points */}
                  {/* Start Point (Pickup) */}
                  <g transform="translate(60, 220)">
                    <circle cx="0" cy="0" r="16" fill="#3b82f6" opacity="0.2" className="animate-ping" />
                    <circle cx="0" cy="0" r="7" fill="#3b82f6" />
                    <circle cx="0" cy="0" r="3" fill="#ffffff" />
                    <text x="12" y="4" fill="#60a5fa" fontSize="9" fontWeight="bold" fontFamily="monospace">START (PICKUP)</text>
                  </g>

                  {/* End Point (Dropoff) */}
                  <g transform="translate(340, 80)">
                    <circle cx="0" cy="0" r="7" fill="#ef4444" />
                    <circle cx="0" cy="0" r="3" fill="#ffffff" />
                    <text x="-95" y="4" fill="#f87171" fontSize="9" fontWeight="bold" fontFamily="monospace">END (DROPOFF)</text>
                  </g>

                  {/* Live Bouncing Transit Truck Marker */}
                  <g transform="translate(200, 140)">
                    <circle cx="0" cy="0" r="15" fill="#10b981" opacity="0.2" className="animate-ping" />
                    <circle cx="0" cy="0" r="8" fill="#10b981" />
                    <polygon points="-5,3 -2,-4 2,-4 5,3" fill="#ffffff" />
                    <rect x="-3" y="-1" width="6" height="3" fill="#047857" />
                  </g>
                </svg>

                {/* Telemetry panel */}
                <div className="absolute bottom-4 left-4 right-4 bg-slate-950/80 backdrop-blur-md border border-slate-800 rounded-xl p-3.5 text-xs text-slate-300 flex justify-between items-center shadow-lg">
                  <div className="space-y-0.5">
                    <p className="font-semibold text-white flex items-center gap-1.5">
                      <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
                      GPS Telemetry (Simulation)
                    </p>
                    <p className="text-slate-400 font-mono text-[10px]">Stops: {trip.stops.length} registered</p>
                  </div>
                  <div className="text-right">
                    <p className="font-bold text-indigo-400">ETA: ~18 mins</p>
                    <p className="text-slate-400">Truck: {trip.truckAssetCode ?? "N/A"}</p>
                  </div>
                </div>
              </div>

              {/* Informational tip */}
              <div className="flex items-start gap-2.5 rounded-xl bg-slate-50 border p-3.5 text-xs text-slate-600">
                <AlertCircle className="h-4.5 w-4.5 text-slate-400 shrink-0 mt-0.5" />
                <p>
                  Real-time mapping tracking is working in Simulation mode. The truck marker indicates the mock GPS location in relation to Stop 1 ({plannedStops.pickup?.locationText || "Pickup"}) and Stop 2 ({plannedStops.dropoff?.locationText || "Dropoff"}).
                </p>
              </div>
            </div>

            <div className="mt-6 flex justify-end pt-4 border-t">
              <Button onClick={() => setMapOpen(false)} className="rounded-xl">
                Close Map
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Styled animation helper for dash offset */}
      <style>{`
        @keyframes dash {
          to {
            stroke-dashoffset: 0;
          }
        }
      `}</style>

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
