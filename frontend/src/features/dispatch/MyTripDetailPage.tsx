import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type { DispatchTripDetail, TripDocumentType, TripStatus } from "./types";
import { operationalFlow, statusLabels } from "./types";

const docTypes: TripDocumentType[] = ["WAYBILL", "POD", "ATW"];
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
  DISPATCHED: { endpoint: "start", label: "Start Trip" },
  ENROUTE_PICKUP: { endpoint: "arrive-pickup", label: "Arrive Pickup" },
  AT_PICKUP: { endpoint: "confirm-loaded", label: "Confirm Loaded" },
  LOADED: { endpoint: "depart-pickup", label: "Depart Pickup" },
  ENROUTE_DROPOFF: { endpoint: "arrive-dropoff", label: "Arrive Dropoff" },
  AT_DROPOFF: { endpoint: "confirm-delivery", label: "Confirm Delivery" },
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
  const [actionLoading, setActionLoading] = useState(false);
  const [modal, setModal] = useState<ActionModal>(null);
  const fileInputs = useRef<Record<TripDocumentType, HTMLInputElement | null>>({
    WAYBILL: null,
    POD: null,
    ATW: null
  });

  const fetchTrip = async () => {
    if (!id) return;
    try {
      setLoading(true);
      const detail = await api<DispatchTripDetail>(`/api/dispatch/my-trips/${id}`, { method: "GET" });
      setTrip(detail);
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
    void handleUploadDoc(docType, file.name);
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

  return (
    <div className="space-y-6 pb-24 md:pb-6">
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
              <span className="text-xs rounded-full border border-amber-200 bg-amber-50 px-2 py-1 text-amber-700">
                POD Pending
              </span>
            ) : null}
          </div>
        </div>
      </div>

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
          {docTypes.map((type) => {
            const doc = trip.documents.find((d) => d.type === type);
            const state = doc?.state ?? "MISSING";
            const podLocked = type === "POD" && trip.status !== "DELIVERED";
            return (
              <div key={type} className="rounded-lg border border-border/60 bg-muted/10 px-4 py-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <p className="text-sm font-semibold text-foreground">{type}</p>
                    <p className="text-xs text-muted-foreground">Status: {state}</p>
                    {podLocked ? (
                      <p className="mt-1 text-xs text-amber-600">POD upload is available after delivery.</p>
                    ) : null}
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    {doc?.storageKey ? (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => window.open(doc.storageKey, "_blank", "noopener,noreferrer")}
                      >
                        View
                      </Button>
                    ) : null}
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={actionLoading || podLocked}
                      onClick={() => fileInputs.current[type]?.click()}
                    >
                      {doc ? "Replace" : "Upload"}
                    </Button>
                    <input
                      ref={(el) => {
                        fileInputs.current[type] = el;
                      }}
                      type="file"
                      className="hidden"
                      onChange={(e) => {
                        handleFileUpload(type, e.target.files);
                        e.currentTarget.value = "";
                      }}
                    />
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      <div className="fixed bottom-0 left-0 right-0 z-40 border-t border-border bg-white/95 p-4 backdrop-blur md:static md:border-0 md:bg-transparent md:p-0">
        <div className="space-y-3">
          {nextAction && operationalFlow.includes(trip.status) ? (
            <Button
              className="h-12 w-full text-base font-semibold"
              onClick={() => handleDriverAction(nextAction.endpoint)}
              disabled={actionLoading}
            >
              {nextAction.label?.toUpperCase()}
            </Button>
          ) : (
            <Button className="h-12 w-full text-base font-semibold" variant="outline" disabled>
              No pending actions
            </Button>
          )}
          <div className="grid grid-cols-2 gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() =>
                setModal({
                  type: "HOLD",
                  remarks: "",
                  eventAt: toLocalInput(new Date().toISOString())
                })
              }
              disabled={actionLoading || !driverHoldEligible.includes(trip.status)}
            >
              Request Hold
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() =>
                setModal({
                  type: "FAILED",
                  remarks: "",
                  eventAt: toLocalInput(new Date().toISOString())
                })
              }
              disabled={actionLoading || !failedAttemptEligible.includes(trip.status)}
            >
              Report Failed Attempt
            </Button>
          </div>
        </div>
      </div>

      {modal ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          onClick={() => setModal(null)}
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
