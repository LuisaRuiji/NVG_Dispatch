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
  DispatchAssignmentDayView,
  DispatchAssignmentGroupBy,
  DispatchTripDocument,
  DispatchTripListItem,
  TripDocumentState,
  TripStatus
} from "./types";
import { statusLabels } from "./types";
import { RefreshCw } from "lucide-react";

type PanelState = {
  items: DispatchTripListItem[];
  totalCount: number;
  loading: boolean;
};

type DocQueueItem = {
  trip: DispatchTripListItem;
  doc: DispatchTripDocument;
};

const docStateClasses: Record<TripDocumentState, string> = {
  MISSING: "border-slate-200 text-slate-500",
  UPLOADED: "border-amber-200 text-amber-700",
  VERIFIED: "border-emerald-200 text-emerald-700",
  REJECTED: "border-rose-200 text-rose-700"
};

const upcomingHours = 4;
const exceptionPageSize = 5;
const activePageSize = 12;
const upcomingPageSize = 8;

function PodBadge({ trip }: { trip: DispatchTripListItem }) {
  const state = trip.podState ?? "MISSING";
  return (
    <span
      className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
        docStateClasses[state]
      }`}
    >
      POD {state}
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

    if (rawReason === "POD must be verified.") {
      return "POD must be verified";
    }

    if (rawReason === "POD must be uploaded.") {
      return "POD must be uploaded";
    }

    if (rawReason === "POD must be uploaded or POD pending override must be set.") {
      return "POD required before close";
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

function formatDateTime(value?: string | null) {
  if (!value) return "-";
  return new Date(value).toLocaleString();
}

function toDateInputValue(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function formatWindowRange(startRaw?: string | null, endRaw?: string | null) {
  if (!startRaw || !endRaw) return "-";
  const start = new Date(startRaw);
  const end = new Date(endRaw);
  return `${start.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}-${end.toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit"
  })}`;
}

function formatDurationMinutes(totalMinutes?: number | null) {
  if (typeof totalMinutes !== "number" || Number.isNaN(totalMinutes) || totalMinutes < 0) return "-";
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  if (hours > 0 && minutes > 0) return `${hours}h ${minutes}m`;
  if (hours > 0) return `${hours}h`;
  return `${minutes}m`;
}

function formatWindow(trip: DispatchTripListItem) {
  const startRaw = trip.plannedStart ?? trip.pickupScheduledAt;
  const endRaw = trip.plannedEnd ?? trip.dropoffScheduledAt;
  if (!startRaw || !endRaw) return "-";

  const start = new Date(startRaw);
  const end = new Date(endRaw);
  const startDate = start.toLocaleDateString();
  const startTime = start.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  const endTime = end.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  return `${startDate} ${startTime}-${endTime}`;
}

function getStopLabel(status: TripStatus) {
  if (["DISPATCHED", "ENROUTE_PICKUP", "AT_PICKUP"].includes(status)) return "Pickup";
  if (["LOADED", "ENROUTE_DROPOFF", "AT_DROPOFF", "DELIVERED"].includes(status)) return "Dropoff";
  return "-";
}

function startOfToday() {
  const now = new Date();
  now.setHours(0, 0, 0, 0);
  return now;
}

function endOfToday() {
  const now = new Date();
  now.setHours(23, 59, 59, 999);
  return now;
}

export default function DispatchBoardPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const me = getMe();
  const roles = me?.roles ?? [];
  const canVerifyDocs = roles.includes("Manager") || roles.includes("HeadOfFinance");

  const [activeTrips, setActiveTrips] = useState<PanelState>({
    items: [],
    totalCount: 0,
    loading: true
  });
  const [onHoldTrips, setOnHoldTrips] = useState<PanelState>({
    items: [],
    totalCount: 0,
    loading: true
  });
  const [failedTrips, setFailedTrips] = useState<PanelState>({
    items: [],
    totalCount: 0,
    loading: true
  });
  const [podPendingTrips, setPodPendingTrips] = useState<PanelState>({
    items: [],
    totalCount: 0,
    loading: true
  });
  const [upcomingTrips, setUpcomingTrips] = useState<PanelState>({
    items: [],
    totalCount: 0,
    loading: true
  });
  const [deliveredTodayCount, setDeliveredTodayCount] = useState(0);
  const [deliveredTodayLoading, setDeliveredTodayLoading] = useState(true);
  const [docQueue, setDocQueue] = useState<{ items: DocQueueItem[]; loading: boolean }>(
    {
      items: [],
      loading: true
    }
  );
  const [assignmentGroupBy, setAssignmentGroupBy] = useState<DispatchAssignmentGroupBy>("driver");
  const [assignmentDay, setAssignmentDay] = useState(() => toDateInputValue(new Date()));
  const [assignmentView, setAssignmentView] = useState<{
    groups: DispatchAssignmentDayView["groups"];
    loading: boolean;
  }>({
    groups: [],
    loading: true
  });

  const loadPanel = async (
    endpoint: string,
    setter: React.Dispatch<React.SetStateAction<PanelState>>,
    pageSize: number
  ) => {
    setter((prev) => ({ ...prev, loading: true }));
    try {
      const params = new URLSearchParams();
      params.set("page", "1");
      params.set("pageSize", pageSize.toString());
      const result = await api<PagedResult<DispatchTripListItem>>(`${endpoint}?${params.toString()}`, {
        method: "GET"
      });
      const items = result.items ?? [];
      setter({
        items,
        totalCount: result.totalCount ?? 0,
        loading: false
      });
      return items;
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load dispatch data.", "error");
      setter((prev) => ({ ...prev, loading: false }));
      return [] as DispatchTripListItem[];
    }
  };

  const loadActive = () => loadPanel("/api/dispatch/trips/active", setActiveTrips, activePageSize);
  const loadOnHold = () => loadPanel("/api/dispatch/trips/on-hold", setOnHoldTrips, exceptionPageSize);
  const loadFailed = () =>
    loadPanel("/api/dispatch/trips/failed-attempts", setFailedTrips, exceptionPageSize);
  const loadPodPending = () =>
    loadPanel("/api/dispatch/trips/pod-pending", setPodPendingTrips, exceptionPageSize);

  const loadUpcoming = async () => {
    setUpcomingTrips((prev) => ({ ...prev, loading: true }));
    try {
      const from = new Date();
      const to = new Date(from.getTime() + upcomingHours * 60 * 60 * 1000);
      const params = new URLSearchParams();
      params.set("page", "1");
      params.set("pageSize", upcomingPageSize.toString());
      params.set("from", from.toISOString());
      params.set("to", to.toISOString());
      const result = await api<PagedResult<DispatchTripListItem>>(`/api/dispatch/trips?${params.toString()}`,
        { method: "GET" }
      );
      setUpcomingTrips({
        items: result.items ?? [],
        totalCount: result.totalCount ?? 0,
        loading: false
      });
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load upcoming schedule.", "error");
      setUpcomingTrips((prev) => ({ ...prev, loading: false }));
    }
  };

  const loadDeliveredToday = async () => {
    setDeliveredTodayLoading(true);
    try {
      const params = new URLSearchParams();
      params.set("page", "1");
      params.set("pageSize", "1");
      params.set("deliveredFrom", startOfToday().toISOString());
      params.set("deliveredTo", endOfToday().toISOString());
      const result = await api<PagedResult<DispatchTripListItem>>(`/api/dispatch/trips?${params.toString()}`,
        { method: "GET" }
      );
      setDeliveredTodayCount(result.totalCount ?? 0);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load delivered count.", "error");
    } finally {
      setDeliveredTodayLoading(false);
    }
  };

  const loadDocQueue = async (trips: DispatchTripListItem[]) => {
    setDocQueue((prev) => ({ ...prev, loading: true }));
    try {
      if (trips.length === 0) {
        setDocQueue({ items: [], loading: false });
        return;
      }
      const candidates = trips.slice(0, exceptionPageSize);
      const results = await Promise.all(
        candidates.map(async (trip) => {
          try {
            const docs = await api<DispatchTripDocument[]>(`/api/dispatch/trips/${trip.id}/documents`, {
              method: "GET"
            });
            return { trip, docs: docs ?? [] };
          } catch (e) {
            console.error(e);
            return { trip, docs: [] };
          }
        })
      );
      const items: DocQueueItem[] = [];
      results.forEach(({ trip, docs }) => {
        docs
          .filter((doc) => doc.state === "UPLOADED")
          .forEach((doc) => items.push({ trip, doc }));
      });
      setDocQueue({ items, loading: false });
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load verification queue.", "error");
      setDocQueue((prev) => ({ ...prev, loading: false }));
    }
  };

  const loadAssignmentDayView = async (dayValue = assignmentDay, groupByValue = assignmentGroupBy) => {
    setAssignmentView((prev) => ({ ...prev, loading: true }));
    try {
      const params = new URLSearchParams();
      params.set("day", dayValue);
      params.set("groupBy", groupByValue);
      const result = await api<DispatchAssignmentDayView>(
        `/api/dispatch/trips/assignment-day?${params.toString()}`,
        { method: "GET" }
      );
      setAssignmentView({
        groups: result.groups ?? [],
        loading: false
      });
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load assignment day view.", "error");
      setAssignmentView((prev) => ({ ...prev, loading: false }));
    }
  };

  const refreshAll = async () => {
    const [activeItems, onHoldItems, failedItems, podPendingItems] = await Promise.all([
      loadActive(),
      loadOnHold(),
      loadFailed(),
      loadPodPending()
    ]);
    void loadUpcoming();
    void loadDeliveredToday();
    void loadDocQueue(podPendingItems.length ? podPendingItems : [...onHoldItems, ...failedItems]);
    void loadAssignmentDayView();
    return activeItems;
  };

  useEffect(() => {
    refreshAll();
  }, []);

  useEffect(() => {
    void loadAssignmentDayView();
  }, [assignmentDay, assignmentGroupBy]);

  const metrics = useMemo(
    () => [
      {
        label: "Active Trips",
        value: activeTrips.totalCount,
        loading: activeTrips.loading
      },
      {
        label: "Trips On Hold",
        value: onHoldTrips.totalCount,
        loading: onHoldTrips.loading
      },
      {
        label: "Failed Attempts",
        value: failedTrips.totalCount,
        loading: failedTrips.loading
      },
      {
        label: "Delivered Today",
        value: deliveredTodayCount,
        loading: deliveredTodayLoading
      },
      {
        label: "POD Pending",
        value: podPendingTrips.totalCount,
        loading: podPendingTrips.loading
      }
    ],
    [
      activeTrips.totalCount,
      activeTrips.loading,
      onHoldTrips.totalCount,
      onHoldTrips.loading,
      failedTrips.totalCount,
      failedTrips.loading,
      deliveredTodayCount,
      deliveredTodayLoading,
      podPendingTrips.totalCount,
      podPendingTrips.loading
    ]
  );

  const handleVerifyDoc = async (tripId: string, docId: string) => {
    try {
      await api(`/api/dispatch/trips/${tripId}/documents/${docId}/verify`, { method: "POST" });
      show("Document verified.", "success");
      refreshAll();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to verify document.", "error");
    }
  };

  const handleRejectDoc = async (tripId: string, docId: string) => {
    const remarks = window.prompt("Reason for rejection?");
    if (!remarks) return;
    try {
      await api(`/api/dispatch/trips/${tripId}/documents/${docId}/reject`, {
        method: "POST",
        body: JSON.stringify({ remarks })
      });
      show("Document rejected.", "success");
      refreshAll();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to reject document.", "error");
    }
  };

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Dispatch Dashboard"
        description="Operational overview of active trips and exceptions."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/dispatch/board" className="text-muted-foreground hover:text-foreground">
              Dispatch
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">Dashboard</span>
          </nav>
        }
        actions={
          <Button variant="outline" size="sm" className="gap-2" onClick={refreshAll}>
            <RefreshCw className="h-4 w-4" />
            Refresh
          </Button>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {metrics.map((metric) => (
          <div key={metric.label} className="surface-card p-4">
            <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">{metric.label}</p>
            <p className="mt-2 text-2xl font-semibold text-foreground">
              {metric.loading ? "..." : metric.value}
            </p>
          </div>
        ))}
      </div>

      <div className="surface-card p-6">
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">Planning visibility</p>
            <h3 className="text-sm font-semibold text-foreground">Daily Assignment View</h3>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <select
              value={assignmentGroupBy}
              onChange={(e) => setAssignmentGroupBy(e.target.value as DispatchAssignmentGroupBy)}
              className="h-9 rounded-lg border border-border bg-background px-3 text-sm"
            >
              <option value="driver">Driver</option>
              <option value="truck">Truck</option>
            </select>
            <input
              type="date"
              value={assignmentDay}
              onChange={(e) => setAssignmentDay(e.target.value)}
              className="h-9 rounded-lg border border-border bg-background px-3 text-sm"
            />
            <Button variant="outline" size="sm" onClick={() => loadAssignmentDayView()}>
              Refresh
            </Button>
          </div>
        </div>

        {assignmentView.loading ? (
          <LoadingSkeleton rows={3} />
        ) : assignmentView.groups.length === 0 ? (
          <EmptyState title="No assignments" description="No scheduled trip windows for the selected day." />
        ) : (
          <div className="space-y-4">
            {assignmentView.groups.map((group) => (
              <div key={group.groupKey} className="rounded-lg border border-border/60">
                <div className="flex items-center justify-between border-b border-border/60 bg-muted/20 px-4 py-3">
                  <div className="text-sm font-semibold text-foreground">{group.groupLabel}</div>
                  {group.hasOverlap ? (
                    <span className="rounded-full border border-rose-200 bg-rose-50 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-rose-700">
                      Conflict
                    </span>
                  ) : (
                    <span className="rounded-full border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-emerald-700">
                      OK
                    </span>
                  )}
                </div>
                <DataTable>
                  <thead className="bg-muted/10 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
                    <tr>
                      <th className="px-4 py-3 text-left">Trip</th>
                      <th className="px-4 py-3 text-left">Customer</th>
                      <th className="px-4 py-3 text-left">Window</th>
                      <th className="px-4 py-3 text-left">Duration</th>
                      <th className="px-4 py-3 text-left">Status</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border/50">
                    {group.trips.map((trip) => (
                      <tr
                        key={trip.tripId}
                        className={`cursor-pointer text-sm hover:bg-muted/30 ${
                          trip.hasOverlap ? "bg-rose-50/40" : ""
                        }`}
                        onClick={() => nav(`/dispatch/trips/${trip.tripId}`)}
                      >
                        <td className="px-4 py-3 font-medium text-foreground">{trip.tripReference}</td>
                        <td className="px-4 py-3">{trip.customer?.name ?? "-"}</td>
                        <td className="px-4 py-3 text-xs text-muted-foreground">
                          {formatWindowRange(trip.plannedStart, trip.plannedEnd)}
                        </td>
                        <td className="px-4 py-3 text-xs text-muted-foreground">
                          {formatDurationMinutes(trip.plannedDurationMinutes)}
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex flex-wrap items-center gap-2">
                            <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
                            {trip.hasOverlap ? (
                              <span className="rounded-full border border-rose-200 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-rose-700">
                                Overlap
                              </span>
                            ) : (
                              <span className="rounded-full border border-emerald-200 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-emerald-700">
                                OK
                              </span>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </DataTable>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        {[
          {
            title: "Trips On Hold",
            subtitle: "Paused trips requiring attention",
            state: onHoldTrips,
            emptyTitle: "No trips on hold",
            emptyDescription: "Operational holds will appear here."
          },
          {
            title: "Failed Attempts",
            subtitle: "Trips needing recovery action",
            state: failedTrips,
            emptyTitle: "No failed attempts",
            emptyDescription: "All trips are moving smoothly."
          },
          {
            title: "POD Pending",
            subtitle: "Delivered but not closable",
            state: podPendingTrips,
            emptyTitle: "No POD pending trips",
            emptyDescription: "All delivered trips are closable."
          }
        ].map((panel) => (
          <div key={panel.title} className="surface-card p-5">
            <div className="mb-4 flex items-center justify-between">
              <div>
                <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">{panel.subtitle}</p>
                <h3 className="text-sm font-semibold text-foreground">{panel.title}</h3>
              </div>
              <Button variant="ghost" size="sm" onClick={() => nav("/dispatch/trips")}>View all</Button>
            </div>
            {panel.state.loading && panel.state.items.length === 0 ? (
              <LoadingSkeleton rows={3} />
            ) : panel.state.items.length === 0 ? (
              <EmptyState title={panel.emptyTitle} description={panel.emptyDescription} />
            ) : (
              <DataTable>
                <thead className="bg-muted/30 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
                  <tr>
                    <th className="px-4 py-3 text-left">Trip</th>
                    <th className="px-4 py-3 text-left">Customer</th>
                    <th className="px-4 py-3 text-left">Driver</th>
                    <th className="px-4 py-3 text-left">Stop</th>
                    <th className="px-4 py-3 text-left">Updated</th>
                    <th className="px-4 py-3 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/50">
                  {panel.state.items.map((trip) => (
                    <tr
                      key={trip.id}
                      className="cursor-pointer text-sm hover:bg-muted/30"
                      onClick={() => nav(`/dispatch/trips/${trip.id}`)}
                    >
                      <td className="px-4 py-3 font-medium text-foreground">{trip.id.slice(0, 8)}</td>
                      <td className="px-4 py-3">{trip.customer?.name ?? "-"}</td>
                      <td className="px-4 py-3">{trip.driverUsername ?? "-"}</td>
                      <td className="px-4 py-3 text-xs text-muted-foreground">{getStopLabel(trip.status)}</td>
                      <td className="px-4 py-3 text-xs text-muted-foreground">
                        {formatDateTime(trip.updatedAt ?? trip.createdAt)}
                      </td>
                      <td className="px-4 py-3 text-right">
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={(e) => {
                            e.stopPropagation();
                            nav(`/dispatch/trips/${trip.id}`);
                          }}
                        >
                          Open
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </DataTable>
            )}
          </div>
        ))}
      </div>

      <div className="surface-card p-6">
        <div className="mb-4 flex items-center justify-between">
          <div>
            <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">Trips currently in progress</p>
            <h3 className="text-sm font-semibold text-foreground">Active Trips</h3>
          </div>
          <Button variant="ghost" size="sm" onClick={() => nav("/dispatch/trips")}>View all</Button>
        </div>

        {activeTrips.loading && activeTrips.items.length === 0 ? (
          <LoadingSkeleton rows={4} />
        ) : activeTrips.items.length === 0 ? (
          <EmptyState title="No active trips" description="All trips are closed or cancelled." />
        ) : (
          <DataTable>
            <thead className="bg-muted/30 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
              <tr>
                <th className="px-6 py-4 text-left">Trip</th>
                <th className="px-6 py-4 text-left">Customer</th>
                <th className="px-6 py-4 text-left">Driver</th>
                <th className="px-6 py-4 text-left">Truck</th>
                <th className="px-6 py-4 text-left">Status</th>
                <th className="px-6 py-4 text-left">Window</th>
                <th className="px-6 py-4 text-left">POD</th>
                <th className="px-6 py-4 text-left">Close Docs</th>
                <th className="px-6 py-4 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/50">
              {activeTrips.items.map((trip) => (
                <tr
                  key={trip.id}
                  className="cursor-pointer text-sm hover:bg-muted/30"
                  onClick={() => nav(`/dispatch/trips/${trip.id}`)}
                >
                  <td className="px-6 py-4 font-medium text-foreground">{trip.id.slice(0, 8)}</td>
                  <td className="px-6 py-4">{trip.customer?.name ?? "-"}</td>
                  <td className="px-6 py-4">{trip.driverUsername ?? "-"}</td>
                  <td className="px-6 py-4">{trip.truckAssetCode ?? "-"}</td>
                  <td className="px-6 py-4">
                    <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
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
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={(e) => {
                        e.stopPropagation();
                        nav(`/dispatch/trips/${trip.id}`);
                      }}
                    >
                      View
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </DataTable>
        )}
      </div>

      <div className="grid gap-6 lg:grid-cols-[1.1fr_1fr]">
        <div className="surface-card p-6">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">
                Pickups in the next {upcomingHours} hours
              </p>
              <h3 className="text-sm font-semibold text-foreground">Upcoming Schedule</h3>
            </div>
            <Button variant="ghost" size="sm" onClick={() => nav("/dispatch/trips")}>View all</Button>
          </div>

          {upcomingTrips.loading && upcomingTrips.items.length === 0 ? (
            <LoadingSkeleton rows={3} />
          ) : upcomingTrips.items.length === 0 ? (
            <EmptyState title="No upcoming pickups" description="No trips scheduled in the next few hours." />
          ) : (
            <DataTable>
              <thead className="bg-muted/30 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
                <tr>
                  <th className="px-4 py-3 text-left">Trip</th>
                  <th className="px-4 py-3 text-left">Customer</th>
                  <th className="px-4 py-3 text-left">Driver</th>
                  <th className="px-4 py-3 text-left">Truck</th>
                  <th className="px-4 py-3 text-left">Pickup</th>
                  <th className="px-4 py-3 text-left">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/50">
                {upcomingTrips.items.map((trip) => (
                  <tr
                    key={trip.id}
                    className="cursor-pointer text-sm hover:bg-muted/30"
                    onClick={() => nav(`/dispatch/trips/${trip.id}`)}
                  >
                    <td className="px-4 py-3 font-medium text-foreground">{trip.id.slice(0, 8)}</td>
                    <td className="px-4 py-3">{trip.customer?.name ?? "-"}</td>
                    <td className="px-4 py-3">{trip.driverUsername ?? "-"}</td>
                    <td className="px-4 py-3">{trip.truckAssetCode ?? "-"}</td>
                    <td className="px-4 py-3 text-xs text-muted-foreground">
                      {formatDateTime(trip.pickupScheduledAt)}
                    </td>
                    <td className="px-4 py-3">
                      <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </DataTable>
          )}
        </div>

        <div className="surface-card p-6">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">
                Documents awaiting verification
              </p>
              <h3 className="text-sm font-semibold text-foreground">Verification Queue</h3>
            </div>
          </div>

          {docQueue.loading && docQueue.items.length === 0 ? (
            <LoadingSkeleton rows={3} />
          ) : docQueue.items.length === 0 ? (
            <EmptyState title="No documents pending" description="No uploaded documents require verification." />
          ) : (
            <DataTable>
              <thead className="bg-muted/30 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
                <tr>
                  <th className="px-4 py-3 text-left">Trip</th>
                  <th className="px-4 py-3 text-left">Driver</th>
                  <th className="px-4 py-3 text-left">Document</th>
                  <th className="px-4 py-3 text-left">Uploaded</th>
                  <th className="px-4 py-3 text-left">Status</th>
                  <th className="px-4 py-3 text-right">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/50">
                {docQueue.items.map(({ trip, doc }) => (
                  <tr
                    key={`${trip.id}-${doc.id}`}
                    className="cursor-pointer text-sm hover:bg-muted/30"
                    onClick={() => nav(`/dispatch/trips/${trip.id}`)}
                  >
                    <td className="px-4 py-3 font-medium text-foreground">{trip.id.slice(0, 8)}</td>
                    <td className="px-4 py-3">{trip.driverUsername ?? "-"}</td>
                    <td className="px-4 py-3">{doc.type}</td>
                    <td className="px-4 py-3 text-xs text-muted-foreground">{formatDateTime(doc.uploadedAt)}</td>
                    <td className="px-4 py-3">
                      <span className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
                        docStateClasses[doc.state]
                      }`}>
                        {doc.state}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      {canVerifyDocs ? (
                        <div className="flex items-center justify-end gap-2">
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={(e) => {
                              e.stopPropagation();
                              handleVerifyDoc(trip.id, doc.id);
                            }}
                          >
                            Verify
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={(e) => {
                              e.stopPropagation();
                              handleRejectDoc(trip.id, doc.id);
                            }}
                          >
                            Reject
                          </Button>
                        </div>
                      ) : (
                        <span className="text-xs text-muted-foreground">Read-only</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </DataTable>
          )}
        </div>
      </div>
    </div>
  );
}
