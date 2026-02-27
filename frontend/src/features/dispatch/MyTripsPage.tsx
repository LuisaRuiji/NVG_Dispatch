import { useEffect, useMemo, useState } from "react";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import type { PagedResult } from "@/lib/paging";
import type { DispatchTripListItem, TripStatus } from "./types";
import { nextOperationalStatus, operationalFlow, statusLabels } from "./types";
import { Button } from "@/components/ui/button";

type ModalState =
  | { type: "HOLD"; trip: DispatchTripListItem; remarks: string }
  | { type: "FAILED"; trip: DispatchTripListItem; remarks: string }
  | { type: "POD"; trip: DispatchTripListItem; storageKey: string }
  | null;

const failedAttemptEligible: TripStatus[] = [
  "ENROUTE_PICKUP",
  "AT_PICKUP",
  "ENROUTE_DROPOFF",
  "AT_DROPOFF"
];

export default function MyTripsPage() {
  const { toasts, show } = useToast();
  const me = getMe();
  const [loading, setLoading] = useState(false);
  const [trips, setTrips] = useState<DispatchTripListItem[]>([]);
  const [includeClosed, setIncludeClosed] = useState(false);
  const [actionModal, setActionModal] = useState<ModalState>(null);
  const [actionLoadingId, setActionLoadingId] = useState<string | null>(null);

  const statusFilter = useMemo(() => {
    if (includeClosed) return null;
    const activeStatuses = [
      "DISPATCHED",
      "ENROUTE_PICKUP",
      "AT_PICKUP",
      "LOADED",
      "ENROUTE_DROPOFF",
      "AT_DROPOFF",
      "DELIVERED",
      "ON_HOLD",
      "FAILED_ATTEMPT"
    ];
    return activeStatuses.join(",");
  }, [includeClosed]);

  const loadTrips = async () => {
    if (!me) return;
    try {
      setLoading(true);
      const params = new URLSearchParams();
      params.set("page", "1");
      params.set("pageSize", "50");
      if (statusFilter) params.set("status", statusFilter);
      params.set("driverId", me.userId);
      const result = await api<PagedResult<DispatchTripListItem>>(
        `/api/dispatch/trips?${params.toString()}`,
        { method: "GET" }
      );
      setTrips(result.items ?? []);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load trips.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTrips();
  }, [statusFilter, me?.userId]);

  const handleStatusChange = async (trip: DispatchTripListItem, toStatus: TripStatus, remarks?: string | null, podPendingOverride?: boolean | null) => {
    try {
      setActionLoadingId(trip.id);
      await api(`/api/dispatch/trips/${trip.id}/status`, {
        method: "POST",
        body: JSON.stringify({ toStatus, remarks: remarks ?? null, podPendingOverride: podPendingOverride ?? null })
      });
      show("Trip status updated.", "success");
      await loadTrips();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to update status.", "error");
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleUploadPod = async (trip: DispatchTripListItem, storageKey: string) => {
    try {
      setActionLoadingId(trip.id);
      await api(`/api/dispatch/trips/${trip.id}/documents`, {
        method: "POST",
        body: JSON.stringify({ type: "POD", storageKey })
      });
      show("POD uploaded.", "success");
      await loadTrips();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to upload POD.", "error");
    } finally {
      setActionLoadingId(null);
    }
  };

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader title="My Trips" description="Your assigned trips and next actions." />

      <div className="surface-card p-6 flex items-center justify-between">
        <div>
          <p className="text-xs uppercase text-muted-foreground">Filters</p>
          <label className="mt-2 inline-flex items-center gap-2 text-sm text-muted-foreground">
            <input
              type="checkbox"
              checked={includeClosed}
              onChange={(e) => setIncludeClosed(e.target.checked)}
            />
            Include closed/cancelled
          </label>
        </div>
        <Button variant="outline" size="sm" onClick={loadTrips} disabled={loading}>
          Refresh
        </Button>
      </div>

      <div className="surface-card p-6">
        {loading && trips.length === 0 ? (
          <LoadingSkeleton rows={6} />
        ) : trips.length === 0 ? (
          <EmptyState title="No trips assigned" description="You currently have no trips." />
        ) : (
          <DataTable>
            <thead className="bg-muted/30 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
              <tr>
                <th className="px-6 py-4 text-left">Trip</th>
                <th className="px-6 py-4 text-left">Customer</th>
                <th className="px-6 py-4 text-left">Status</th>
                <th className="px-6 py-4 text-left">Docs</th>
                <th className="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/50">
              {trips.map((trip) => {
                const next = nextOperationalStatus(trip.status);
                const isOperational = operationalFlow.includes(trip.status);
                const isWaitingManager =
                  trip.status === "ON_HOLD" || trip.status === "FAILED_ATTEMPT";
                return (
                  <tr key={trip.id} className="text-sm">
                    <td className="px-6 py-4 font-medium text-foreground">{trip.id.slice(0, 8)}</td>
                    <td className="px-6 py-4">{trip.customer?.name ?? "-"}</td>
                    <td className="px-6 py-4">
                      <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
                    </td>
                    <td className="px-6 py-4 text-muted-foreground">
                      {trip.uploadedDocumentCount}/{trip.requiredDocumentCount}
                      {trip.podPending ? " • POD pending" : ""}
                    </td>
                    <td className="px-6 py-4 text-right">
                      <div className="flex flex-wrap items-center justify-end gap-2">
                        {next && isOperational ? (
                          <Button
                            variant="default"
                            size="sm"
                            disabled={actionLoadingId === trip.id}
                            onClick={() => handleStatusChange(trip, next)}
                          >
                            Advance to {statusLabels[next]}
                          </Button>
                        ) : (
                          <span className="text-xs text-muted-foreground">
                            {isWaitingManager ? "Awaiting manager action" : "No next step"}
                          </span>
                        )}
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={actionLoadingId === trip.id || trip.status === "CLOSED" || trip.status === "CANCELLED"}
                          onClick={() =>
                            setActionModal({ type: "HOLD", trip, remarks: "" })
                          }
                        >
                          Put on Hold
                        </Button>
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={
                            actionLoadingId === trip.id ||
                            !failedAttemptEligible.includes(trip.status)
                          }
                          onClick={() =>
                            setActionModal({ type: "FAILED", trip, remarks: "" })
                          }
                        >
                          Failed Attempt
                        </Button>
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={actionLoadingId === trip.id}
                          onClick={() =>
                            setActionModal({ type: "POD", trip, storageKey: "" })
                          }
                        >
                          Upload POD
                        </Button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </DataTable>
        )}
      </div>

      {actionModal ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          onClick={() => setActionModal(null)}
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
                <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Trip Action</p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">
                  {actionModal.type === "POD"
                    ? "Upload POD"
                    : actionModal.type === "HOLD"
                    ? "Place on Hold"
                    : "Report Failed Attempt"}
                </h2>
              </div>
              <button
                onClick={() => setActionModal(null)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>

            {actionModal.type === "POD" ? (
              <div className="mt-5 space-y-2 text-sm">
                <label className="text-xs uppercase text-slate-500">Storage Key / URL</label>
                <input
                  value={actionModal.storageKey}
                  onChange={(e) =>
                    setActionModal({ ...actionModal, storageKey: e.target.value })
                  }
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                  placeholder="pod/2026/manifest.pdf"
                />
              </div>
            ) : (
              <div className="mt-5 space-y-2 text-sm">
                <label className="text-xs uppercase text-slate-500">Remarks</label>
                <textarea
                  value={actionModal.remarks}
                  onChange={(e) =>
                    setActionModal({ ...actionModal, remarks: e.target.value })
                  }
                  className="mt-2 min-h-[100px] w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm"
                  placeholder="Add remarks"
                />
              </div>
            )}

            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setActionModal(null)}>
                Cancel
              </Button>
              <Button
                onClick={() => {
                  if (!actionModal) return;
                  if (actionModal.type === "POD") {
                    if (!actionModal.storageKey.trim()) {
                      show("Storage key is required.", "error");
                      return;
                    }
                    handleUploadPod(actionModal.trip, actionModal.storageKey.trim());
                    setActionModal(null);
                  } else {
                    if (!actionModal.remarks.trim()) {
                      show("Remarks are required.", "error");
                      return;
                    }
                    handleStatusChange(
                      actionModal.trip,
                      actionModal.type === "HOLD" ? "ON_HOLD" : "FAILED_ATTEMPT",
                      actionModal.remarks.trim()
                    );
                    setActionModal(null);
                  }
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

