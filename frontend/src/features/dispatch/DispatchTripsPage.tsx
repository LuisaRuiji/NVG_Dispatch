import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import { getMe } from "@/features/auth/authStore";
import type {
  DispatchTripDetail,
  DispatchTripListItem,
  TripDocumentState,
  TripStatus
} from "./types";
import { statusLabels } from "./types";
import { RefreshCw } from "lucide-react";
import RecommendationPanel from "./components/RecommendationPanel";

type CustomerOption = { id: string; name: string };
type DriverOption = { id: string; username: string };
type TruckOption = { id: string; assetCode: string; assetType: string; status: string };

type FilterState = {
  status: "ALL" | TripStatus;
  driverId: string;
  customerId: string;
  truckId: string;
  pickupFrom: string;
  pickupTo: string;
  deliveredFrom: string;
  deliveredTo: string;
  podStatus: "ALL" | "VERIFIED" | "PENDING";
};

type ActionModal =
  | { type: "HOLD"; trip: DispatchTripListItem; remarks: string; eventAt: string }
  | {
      type: "RESOLVE_FAILED";
      trip: DispatchTripListItem;
      remarks: string;
      eventAt: string;
      loading: boolean;
      previousStatus: TripStatus | null;
    }
  | { type: "CANCEL"; trip: DispatchTripListItem; remarks: string; eventAt: string }
  | null;

const docStateClasses: Record<TripDocumentState, string> = {
  MISSING: "border-slate-200 text-slate-500",
  UPLOADED: "border-amber-200 text-amber-700",
  VERIFIED: "border-emerald-200 text-emerald-700",
  REJECTED: "border-rose-200 text-rose-700"
};

const toLocalInput = (iso?: string | null) => {
  if (!iso) return "";
  const isoStr = iso.endsWith("Z") || iso.includes("+") ? iso : iso + "Z";
  const d = new Date(isoStr);
  if (Number.isNaN(d.getTime())) return "";
  const pad = (n: number) => n.toString().padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

const fromLocalInput = (value: string) => {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
};

const formatWindow = (trip: DispatchTripListItem) => {
  const startRaw = trip.plannedStart ?? trip.pickupScheduledAt;
  const endRaw = trip.plannedEnd ?? trip.dropoffScheduledAt;
  if (!startRaw || !endRaw) return "-";

  const start = new Date(startRaw);
  const end = new Date(endRaw);
  const startDate = start.toLocaleDateString();
  const startTime = start.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  const endTime = end.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  return `${startDate} ${startTime}-${endTime}`;
};

const mobilePrimaryActionLabel = (trip: DispatchTripListItem) => {
  if (trip.status === "DRAFT") return "Open to Prepare Dispatch";
  if (trip.status === "DELIVERED" && !trip.closeDocumentReady) return "Open to Resolve Documents";
  if (trip.status === "DELIVERED" && trip.closeDocumentReady) return "Open to Review Close";
  if (trip.status === "FAILED_ATTEMPT") return "Open to Resolve Failed";
  if (trip.status === "ON_HOLD") return "Open to Review Hold";
  return "Open Trip";
};

function PodBadge({ trip }: { trip: DispatchTripListItem }) {
  const state = trip.podState ?? "MISSING";
  return (
    <span
      className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
        docStateClasses[state]
      }`}
    >
      {state}
    </span>
  );
}

function CloseDocsBadge({ trip }: { trip: DispatchTripListItem }) {
  const countsText = `${trip.missingRequiredDocumentCount} missing / ${trip.rejectedRequiredDocumentCount} rejected`;
  const rawReason = trip.closeDocumentBlockReason ?? "";
  const inlineReason = (() => {
    if (trip.closeDocumentReady) {
      return null;
    }

    if (rawReason.includes("ATW") || rawReason.includes("Waybill")) {
      return "Required docs incomplete";
    }

    return "Document blockers present";
  })();

  if (trip.closeDocumentReady) {
    return (
      <div className="flex flex-col items-start gap-1">
        <span className="rounded-full border border-emerald-200 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-emerald-700">
          Ready
        </span>
        <span className="text-[11px] text-muted-foreground">{countsText}</span>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-start gap-1">
      <span
        className="rounded-full border border-rose-200 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-rose-700"
        title={trip.closeDocumentBlockReason ?? "Document blockers present"}
      >
        Blocked
      </span>
      <span className="text-[11px] font-medium text-rose-700">{inlineReason}</span>
      <span className="text-[11px] text-muted-foreground">{countsText}</span>
    </div>
  );
}

export default function DispatchTripsPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const me = getMe();
  const roles = me?.roles ?? [];
  const canOperate = roles.includes("Manager") || roles.includes("Dispatcher") || roles.includes("Admin") || roles.includes("SuperAdmin");
  const isManager = roles.includes("Manager");

  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);
  const [trips, setTrips] = useState<DispatchTripListItem[]>([]);
  const [customers, setCustomers] = useState<CustomerOption[]>([]);
  const [drivers, setDrivers] = useState<DriverOption[]>([]);
  const [trucks, setTrucks] = useState<TruckOption[]>([]);
  const [filters, setFilters] = useState<FilterState>({
    status: "ALL",
    driverId: "",
    customerId: "",
    truckId: "",
    pickupFrom: "",
    pickupTo: "",
    deliveredFrom: "",
    deliveredTo: "",
    podStatus: "ALL"
  });
  const [menuOpenId, setMenuOpenId] = useState<string | null>(null);
  const [actionModal, setActionModal] = useState<ActionModal>(null);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const loadReferenceData = async () => {
    try {
      const [customerData, userData, assetData] = await Promise.all([
        api<CustomerOption[]>("/api/dispatch/customers", { method: "GET" }),
        api<{ id: string; username: string; roles: string[] }[]>("/api/users", { method: "GET" }),
        api<TruckOption[]>("/api/assets", { method: "GET" })
      ]);
      setCustomers(customerData ?? []);
      setDrivers((userData ?? []).filter((u) => u.roles?.includes("Driver")));
      setTrucks((assetData ?? []).filter((asset) => asset.assetType === "TRUCK" && asset.status === "ACTIVE"));
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load dispatch references.", "error");
    }
  };

  const loadTrips = async (forcePage?: number) => {
    const targetPage = forcePage ?? page;
    try {
      setLoading(true);
      const params = new URLSearchParams();
      params.set("page", targetPage.toString());
      params.set("pageSize", pageSize.toString());
      if (filters.status !== "ALL") params.set("status", filters.status);
      if (filters.driverId) params.set("driverId", filters.driverId);
      if (filters.customerId) params.set("customerId", filters.customerId);
      if (filters.truckId) params.set("truckId", filters.truckId);
      if (filters.pickupFrom) params.set("from", filters.pickupFrom);
      if (filters.pickupTo) params.set("to", filters.pickupTo);
      if (filters.deliveredFrom) params.set("deliveredFrom", filters.deliveredFrom);
      if (filters.deliveredTo) params.set("deliveredTo", filters.deliveredTo);
      if (filters.podStatus !== "ALL") params.set("podStatus", filters.podStatus);

      const result = await api<PagedResult<DispatchTripListItem>>(`/api/dispatch/trips?${params.toString()}`, {
        method: "GET"
      });
      setTrips(result.items ?? []);
      setTotalCount(result.totalCount ?? 0);
      setPage(result.page ?? targetPage);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load trips.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadReferenceData();
  }, []);

  useEffect(() => {
    loadTrips(1);
  }, [
    filters.status,
    filters.driverId,
    filters.customerId,
    filters.truckId,
    filters.pickupFrom,
    filters.pickupTo,
    filters.deliveredFrom,
    filters.deliveredTo,
    filters.podStatus
  ]);

  useEffect(() => {
    loadTrips();
  }, [page]);

  useEffect(() => {
    if (!menuOpenId) return;
    const handler = () => setMenuOpenId(null);
    window.addEventListener("click", handler);
    return () => window.removeEventListener("click", handler);
  }, [menuOpenId]);

  const openTrip = (tripId: string) => {
    nav(`/dispatch/trips/${tripId}`);
  };

  const openTimeline = (tripId: string) => {
    nav(`/dispatch/trips/${tripId}#trip-timeline`);
  };

  const rowClass = (trip: DispatchTripListItem) => {
    if (trip.status === "DELIVERED" && !trip.closeDocumentReady) return "bg-rose-50/60";
    if (trip.status === "FAILED_ATTEMPT") return "bg-rose-50/60";
    if (trip.status === "ON_HOLD") return "bg-amber-50/60";
    if (trip.podPending) return "bg-orange-50/40";
    return "";
  };

  const canHold = (trip: DispatchTripListItem) =>
    canOperate && !["DRAFT", "CLOSED", "CANCELLED", "ON_HOLD", "FAILED_ATTEMPT"].includes(trip.status);

  const canResolveFailed = (trip: DispatchTripListItem) => canOperate && trip.status === "FAILED_ATTEMPT";

  const canCancel = (trip: DispatchTripListItem) =>
    isManager && !["CLOSED", "CANCELLED"].includes(trip.status);

  const handleStatusChange = async (
    trip: DispatchTripListItem,
    toStatus: TripStatus,
    remarks?: string | null,
    eventAt?: string | null
  ) => {
    const eventAtValue = eventAt ?? new Date().toISOString();
    try {
      await api(`/api/dispatch/trips/${trip.id}/status`, {
        method: "POST",
        body: JSON.stringify({
          toStatus,
          remarks: remarks ?? null,
          podPendingOverride: null,
          eventAt: eventAtValue,
          rowVersion: trip.rowVersion
        })
      });
      show("Trip updated.", "success");
      setActionModal(null);
      await loadTrips();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to update trip.", "error");
    }
  };

  const prepareResolveFailed = async (trip: DispatchTripListItem) => {
    setActionModal({
      type: "RESOLVE_FAILED",
      trip,
      remarks: "",
      eventAt: toLocalInput(new Date().toISOString()),
      loading: true,
      previousStatus: null
    });
    try {
      const detail = await api<DispatchTripDetail>(`/api/dispatch/trips/${trip.id}`, { method: "GET" });
      const lastFailed = [...detail.history]
        .reverse()
        .find((entry) => entry.toStatus === "FAILED_ATTEMPT");
      setActionModal((prev) =>
        prev && prev.type === "RESOLVE_FAILED"
          ? {
              ...prev,
              loading: false,
              previousStatus: lastFailed?.fromStatus ?? null
            }
          : prev
      );
      if (!lastFailed?.fromStatus) {
        show("Unable to determine previous status for this trip.", "error");
      }
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load trip history.", "error");
      setActionModal((prev) => (prev && prev.type === "RESOLVE_FAILED" ? { ...prev, loading: false } : prev));
    }
  };

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Trip List"
        description="Filter and manage dispatch trips across the fleet."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/dispatch/board" className="text-muted-foreground hover:text-foreground">
              Dispatch
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">Trips</span>
          </nav>
        }
        actions={
          <Button variant="outline" size="sm" className="gap-2" onClick={() => loadTrips()} disabled={loading}>
            <RefreshCw className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"} />
            Refresh
          </Button>
        }
      />

      {canOperate ? <RecommendationPanel /> : null}

      <div className="surface-card p-6">
        <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-4">
          <div>
            <label className="text-xs uppercase text-muted-foreground">Status</label>
            <select
              value={filters.status}
              onChange={(e) => setFilters((prev) => ({ ...prev, status: e.target.value as FilterState["status"] }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            >
              <option value="ALL">All</option>
              {Object.entries(statusLabels).map(([key, label]) => (
                <option key={key} value={key}>
                  {label}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Driver</label>
            <select
              value={filters.driverId}
              onChange={(e) => setFilters((prev) => ({ ...prev, driverId: e.target.value }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            >
              <option value="">All drivers</option>
              {drivers.map((driver) => (
                <option key={driver.id} value={driver.id}>
                  {driver.username}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Customer</label>
            <select
              value={filters.customerId}
              onChange={(e) => setFilters((prev) => ({ ...prev, customerId: e.target.value }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            >
              <option value="">All customers</option>
              {customers.map((customer) => (
                <option key={customer.id} value={customer.id}>
                  {customer.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Truck</label>
            <select
              value={filters.truckId}
              onChange={(e) => setFilters((prev) => ({ ...prev, truckId: e.target.value }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            >
              <option value="">All trucks</option>
              {trucks.map((truck) => (
                <option key={truck.id} value={truck.id}>
                  {truck.assetCode}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Pickup From</label>
            <input
              type="datetime-local"
              value={filters.pickupFrom}
              onChange={(e) => setFilters((prev) => ({ ...prev, pickupFrom: e.target.value }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Pickup To</label>
            <input
              type="datetime-local"
              value={filters.pickupTo}
              onChange={(e) => setFilters((prev) => ({ ...prev, pickupTo: e.target.value }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Delivered From</label>
            <input
              type="datetime-local"
              value={filters.deliveredFrom}
              onChange={(e) => setFilters((prev) => ({ ...prev, deliveredFrom: e.target.value }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Delivered To</label>
            <input
              type="datetime-local"
              value={filters.deliveredTo}
              onChange={(e) => setFilters((prev) => ({ ...prev, deliveredTo: e.target.value }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">POD Status</label>
            <select
              value={filters.podStatus}
              onChange={(e) => setFilters((prev) => ({ ...prev, podStatus: e.target.value as FilterState["podStatus"] }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            >
              <option value="ALL">All</option>
              <option value="VERIFIED">Verified</option>
              <option value="PENDING">Pending</option>
            </select>
          </div>
        </div>
      </div>

      {loading && trips.length === 0 ? (
        <LoadingSkeleton rows={6} />
      ) : trips.length === 0 ? (
        <EmptyState title="No trips" description="No trips match the current filters." />
      ) : (
        <div className="surface-card p-3 md:p-6">
          <div className="grid gap-3 md:hidden">
            {trips.map((trip) => (
              <button
                key={trip.id}
                type="button"
                className={`rounded-2xl border border-border bg-card p-4 text-left shadow-card transition-all duration-200 active:scale-[0.99] ${rowClass(trip)}`}
                onClick={() => openTrip(trip.id)}
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">Container</p>
                    <p className="mt-1 font-mono text-lg font-semibold text-foreground">
                      {trip.containerNumber ?? "Container pending"}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">Trip {trip.id.slice(0, 8)}</p>
                  </div>
                  <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
                </div>
                <div className="mt-3 rounded-xl border border-border/60 bg-muted/20 px-3 py-2 text-sm">
                  <p className="font-medium text-foreground">{trip.pickupLocation ?? "Pickup not set"}</p>
                  <p className="mt-1 text-muted-foreground">{trip.dropoffLocation ?? "Dropoff not set"}</p>
                  <p className="mt-2 text-xs text-muted-foreground">{formatWindow(trip)}</p>
                </div>
                <div className="mt-3 grid gap-2 text-xs text-muted-foreground">
                  <p>Driver: {trip.driverUsername ?? "Unassigned"}</p>
                  <p>Truck: {trip.truckAssetCode ?? "Unassigned"}</p>
                  <p>Customer: {trip.customer?.name ?? "-"}</p>
                </div>
                <div className="mt-3 flex flex-wrap items-start gap-2">
                  <PodBadge trip={trip} />
                  <CloseDocsBadge trip={trip} />
                </div>
                <div className="mt-4 flex h-12 items-center justify-center rounded-full bg-primary px-4 text-sm font-semibold text-primary-foreground">
                  {mobilePrimaryActionLabel(trip)}
                </div>
              </button>
            ))}
          </div>
          <div className="hidden md:block">
          <DataTable>
            <thead className="bg-muted/30 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
              <tr>
                <th className="px-6 py-4 text-left">Trip</th>
                <th className="px-6 py-4 text-left">Customer</th>
                <th className="px-6 py-4 text-left">Driver</th>
                <th className="px-6 py-4 text-left">Truck</th>
                <th className="px-6 py-4 text-left">Status</th>
                <th className="px-6 py-4 text-left">Window</th>
                <th className="px-6 py-4 text-left">POD Status</th>
                <th className="px-6 py-4 text-left">Close Docs</th>
                <th className="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/50">
              {trips.map((trip) => (
                <tr
                  key={trip.id}
                  className={`cursor-pointer text-sm hover:bg-muted/30 ${rowClass(trip)}`}
                  onClick={() => openTrip(trip.id)}
                >
                  <td className="px-6 py-4 font-medium text-foreground">{trip.id.slice(0, 8)}</td>
                  <td className="px-6 py-4">{trip.customer?.name ?? "-"}</td>
                  <td className="px-6 py-4">{trip.driverUsername ?? "-"}</td>
                  <td className="px-6 py-4">{trip.truckAssetCode ?? "-"}</td>
                  <td className="px-6 py-4">
                    <button
                      className="inline-flex"
                      onClick={(e) => {
                        e.stopPropagation();
                        openTimeline(trip.id);
                      }}
                    >
                      <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
                    </button>
                  </td>
                  <td className="px-6 py-4 text-xs text-muted-foreground">
                    <div>{formatWindow(trip)}</div>
                    {typeof trip.plannedDurationMinutes === "number" ? (
                      <div className="text-[11px] text-muted-foreground/80">{trip.plannedDurationMinutes} min</div>
                    ) : null}
                  </td>
                  <td className="px-6 py-4">
                    <PodBadge trip={trip} />
                  </td>
                  <td className="px-6 py-4">
                    <CloseDocsBadge trip={trip} />
                  </td>
                  <td className="px-6 py-4 text-right">
                    <div className="flex items-center justify-end gap-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={(e) => {
                          e.stopPropagation();
                          openTrip(trip.id);
                        }}
                      >
                        Open
                      </Button>
                      {canHold(trip) ? (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={(e) => {
                            e.stopPropagation();
                            setActionModal({
                              type: "HOLD",
                              trip,
                              remarks: "",
                              eventAt: toLocalInput(new Date().toISOString())
                            });
                          }}
                        >
                          On Hold
                        </Button>
                      ) : null}
                      {canResolveFailed(trip) || canCancel(trip) ? (
                        <div className="relative" onClick={(e) => e.stopPropagation()}>
                          <button
                            className="flex h-9 w-9 items-center justify-center rounded-md border border-border text-sm text-muted-foreground hover:text-foreground"
                            onClick={() => setMenuOpenId((prev) => (prev === trip.id ? null : trip.id))}
                          >
                            ...
                          </button>
                          {menuOpenId === trip.id ? (
                            <div className="absolute right-0 z-20 mt-2 w-44 rounded-md border border-border bg-white p-1 shadow-lg">
                              {canResolveFailed(trip) ? (
                                <button
                                  className="w-full rounded px-3 py-2 text-left text-xs hover:bg-muted"
                                  onClick={() => {
                                    setMenuOpenId(null);
                                    prepareResolveFailed(trip);
                                  }}
                                >
                                  Resolve Failed Attempt
                                </button>
                              ) : null}
                              {canCancel(trip) ? (
                                <button
                                  className="w-full rounded px-3 py-2 text-left text-xs text-rose-600 hover:bg-rose-50"
                                  onClick={() => {
                                    setMenuOpenId(null);
                                    setActionModal({
                                      type: "CANCEL",
                                      trip,
                                      remarks: "",
                                      eventAt: toLocalInput(new Date().toISOString())
                                    });
                                  }}
                                >
                                  Cancel Trip
                                </button>
                              ) : null}
                            </div>
                          ) : null}
                        </div>
                      ) : null}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </DataTable>
          </div>

          <div className="mt-4 flex items-center justify-between text-xs text-muted-foreground">
            <span>
              Page {page} of {totalPages}
            </span>
            <div className="flex items-center gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                Previous
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        </div>
      )}

      {actionModal ? (
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
                  {actionModal.type === "HOLD"
                    ? "Place Trip On Hold"
                    : actionModal.type === "RESOLVE_FAILED"
                    ? "Resolve Failed Attempt"
                    : "Cancel Trip"}
                </h2>
              </div>
              <button
                onClick={() => setActionModal(null)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>

            <div className="mt-5 space-y-4 text-sm">
              <div>
                <label className="text-xs uppercase text-slate-500">Event Time</label>
                <input
                  type="datetime-local"
                  value={actionModal.eventAt}
                  onChange={(e) =>
                    setActionModal((prev) =>
                      prev ? { ...prev, eventAt: e.target.value } : prev
                    )
                  }
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-slate-500">Remarks</label>
                <textarea
                  value={actionModal.remarks}
                  onChange={(e) =>
                    setActionModal((prev) =>
                      prev ? { ...prev, remarks: e.target.value } : prev
                    )
                  }
                  className="mt-2 min-h-[100px] w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm"
                  placeholder={actionModal.type === "CANCEL" ? "Optional remarks" : "Add remarks (required)"}
                />
              </div>
              {actionModal.type === "RESOLVE_FAILED" ? (
                <p className="text-xs text-muted-foreground">
                  {actionModal.loading
                    ? "Loading previous status..."
                    : actionModal.previousStatus
                    ? `Will resume to ${statusLabels[actionModal.previousStatus]}`
                    : "Previous status not found."}
                </p>
              ) : null}
            </div>

            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setActionModal(null)}>
                Cancel
              </Button>
              <Button
                onClick={() => {
                  if (!actionModal) return;
                  const eventAt = fromLocalInput(actionModal.eventAt) ?? new Date().toISOString();
                  if (actionModal.type !== "CANCEL" && !actionModal.remarks.trim()) {
                    show("Remarks are required.", "error");
                    return;
                  }
                  if (actionModal.type === "HOLD") {
                    handleStatusChange(actionModal.trip, "ON_HOLD", actionModal.remarks.trim(), eventAt);
                    return;
                  }
                  if (actionModal.type === "CANCEL") {
                    const remarks = actionModal.remarks.trim() ? actionModal.remarks.trim() : null;
                    handleStatusChange(actionModal.trip, "CANCELLED", remarks, eventAt);
                    return;
                  }
                  if (actionModal.type === "RESOLVE_FAILED") {
                    if (!actionModal.previousStatus) {
                      show("Previous status not available.", "error");
                      return;
                    }
                    handleStatusChange(
                      actionModal.trip,
                      actionModal.previousStatus,
                      actionModal.remarks.trim(),
                      eventAt
                    );
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
