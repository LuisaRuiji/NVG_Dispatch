import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import {
  AlertTriangle,
  ArrowRight,
  CircleDot,
  Clock3,
  LocateFixed,
  RefreshCw,
  Route,
  Send,
  ShieldAlert,
  Truck,
  UserRound,
  Wifi
} from "lucide-react";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import PageHeader from "@/components/PageHeader";
import StatusBadge from "@/components/StatusBadge";
import ToastHost from "@/components/ToastHost";
import { Button } from "@/components/ui/button";
import { getMe } from "@/features/auth/authStore";
import { useDispatchHub } from "@/hooks/useDispatchHub";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import { useToast } from "@/lib/useToast";
import type { DispatchTripListItem, TripStatus } from "./types";

type QueueState = {
  items: DispatchTripListItem[];
  totalCount: number;
  loading: boolean;
};

const readyPageSize = 16;
const activePageSize = 10;
const issuePageSize = 8;

const emptyQueue: QueueState = { items: [], totalCount: 0, loading: true };

const nextActionByStatus: Partial<Record<TripStatus, string>> = {
  DISPATCHED: "Driver to begin pickup approach",
  ENROUTE_PICKUP: "Await port arrival",
  AT_PICKUP: "Await loading confirmation",
  LOADED: "Driver to begin dropoff approach",
  ENROUTE_DROPOFF: "Await dropoff arrival",
  AT_DROPOFF: "Await delivery confirmation",
  DELIVERED: "Verify closeout documents",
  ON_HOLD: "Resolve the operational hold",
  FAILED_ATTEMPT: "Recover the failed attempt"
};

function formatDateTime(value?: string | null) {
  if (!value) return "Schedule pending";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit"
  }).format(date);
}

function formatLocation(value?: string | null) {
  return value?.trim() || "Location pending";
}

function isLocationLive(trip: DispatchTripListItem) {
  const location = trip.latestDriverLocation;
  return Boolean(location && Date.now() - new Date(location.recordedAt).getTime() <= 5 * 60_000);
}

function LocationState({ trip }: { trip: DispatchTripListItem }) {
  const location = trip.latestDriverLocation;
  if (!location) return <span className="text-xs text-muted-foreground">Location unavailable</span>;

  const live = isLocationLive(trip);
  return (
    <span className={`inline-flex items-center gap-1.5 text-xs ${live ? "text-success" : "text-muted-foreground"}`}>
      <span className={`h-1.5 w-1.5 rounded-full ${live ? "bg-success" : "bg-muted-foreground/50"}`} />
      {live ? "Live location" : `Last seen ${formatDateTime(location.recordedAt)}`}
    </span>
  );
}

function RouteSummary({ trip }: { trip: DispatchTripListItem }) {
  return (
    <div className="min-w-0">
      <p className="truncate text-sm font-semibold text-foreground">{formatLocation(trip.pickupLocation)}</p>
      <p className="mt-1 truncate text-xs text-muted-foreground">to {formatLocation(trip.dropoffLocation)}</p>
    </div>
  );
}

function ExecutionTripRow({ trip, onOpen }: { trip: DispatchTripListItem; onOpen: () => void }) {
  const delay = trip.latePickup || trip.lateDelivery;
  const documentSummary = trip.status === "DELIVERED"
    ? trip.closeDocumentReady ? "Closeout documents complete" : `${trip.missingRequiredDocumentCount + trip.rejectedRequiredDocumentCount} closeout document blocker${trip.missingRequiredDocumentCount + trip.rejectedRequiredDocumentCount === 1 ? "" : "s"}`
    : `${trip.uploadedDocumentCount}/${trip.requiredDocumentCount} documents uploaded`;

  return (
    <button
      type="button"
      onClick={onOpen}
      className="group grid w-full gap-4 border-b border-border px-4 py-4 text-left transition-colors hover:bg-muted/40 last:border-b-0 md:grid-cols-[minmax(180px,1.35fr)_minmax(140px,0.9fr)_minmax(150px,0.9fr)_minmax(160px,0.9fr)_auto] md:items-center"
    >
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-mono text-xs font-semibold text-foreground">{trip.containerNumber || `TRIP-${trip.id.slice(0, 8).toUpperCase()}`}</span>
          <StatusBadge status={trip.status} />
        </div>
        <p className="mt-1 truncate text-xs text-muted-foreground">{trip.customer?.name || "Customer pending"}</p>
      </div>
      <RouteSummary trip={trip} />
      <div className="space-y-1 text-xs">
        <p className="inline-flex items-center gap-1.5 text-foreground"><UserRound className="h-3.5 w-3.5 text-muted-foreground" />{trip.driverUsername || "Driver pending"}</p>
        <p className="inline-flex items-center gap-1.5 text-muted-foreground"><Truck className="h-3.5 w-3.5" />{trip.truckAssetCode || "Truck pending"}</p>
      </div>
      <div className="space-y-1 text-xs">
        <p className={`font-medium ${delay ? "text-destructive" : "text-foreground"}`}>{delay ? "Schedule risk" : nextActionByStatus[trip.status] || "Monitor trip progress"}</p>
        <p className="text-muted-foreground">{formatDateTime(trip.pickupScheduledAt)}</p>
        <LocationState trip={trip} />
      </div>
      <div className="flex items-center justify-between gap-3 md:justify-end">
        <span className={`text-xs ${trip.status === "DELIVERED" && !trip.closeDocumentReady ? "text-warning-foreground" : "text-muted-foreground"}`}>{documentSummary}</span>
        <span className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-border text-muted-foreground transition-colors group-hover:border-primary/30 group-hover:text-primary" aria-hidden="true"><ArrowRight className="h-4 w-4" /></span>
      </div>
    </button>
  );
}

function IssueQueue({ title, description, items, loading, onOpen }: { title: string; description: string; items: DispatchTripListItem[]; loading: boolean; onOpen: (id: string) => void }) {
  return (
    <section className="min-w-0" aria-label={title}>
      <div className="mb-3 flex items-center justify-between gap-3">
        <div><p className="text-xs text-muted-foreground">{description}</p><h2 className="mt-1 text-sm font-semibold">{title}</h2></div>
        <span className="rounded-full border border-border bg-muted px-2.5 py-1 text-xs font-semibold tabular-nums">{items.length}</span>
      </div>
      <div className="overflow-hidden rounded-xl border border-border bg-card">
        {loading && items.length === 0 ? <div className="p-4"><LoadingSkeleton rows={3} /></div> : items.length === 0 ? <p className="p-5 text-sm text-muted-foreground">No current exceptions.</p> : items.map((trip) => <button key={trip.id} type="button" onClick={() => onOpen(trip.id)} className="flex w-full items-center justify-between gap-3 border-b border-border px-4 py-3 text-left last:border-b-0 hover:bg-muted/40"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><span className="font-mono text-xs font-semibold">{trip.containerNumber || trip.id.slice(0, 8).toUpperCase()}</span><StatusBadge status={trip.status} /></div><p className="mt-1 truncate text-xs text-muted-foreground">{trip.driverUsername || "Driver pending"} · {formatLocation(trip.pickupLocation)}</p></div><ArrowRight className="h-4 w-4 shrink-0 text-muted-foreground" /></button>)}</div>
    </section>
  );
}

export default function DispatchBoardPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { toasts, show } = useToast();
  const roles = getMe()?.roles ?? [];
  const canDispatch = roles.includes("Dispatcher") || roles.includes("Manager");
  const [readyTrips, setReadyTrips] = useState<QueueState>(emptyQueue);
  const [activeTrips, setActiveTrips] = useState<QueueState>(emptyQueue);
  const [onHoldTrips, setOnHoldTrips] = useState<QueueState>(emptyQueue);
  const [failedTrips, setFailedTrips] = useState<QueueState>(emptyQueue);
  const [dispatchingTripId, setDispatchingTripId] = useState<string | null>(null);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);
  const activePageParam = Number(searchParams.get("executionPage"));
  const activePage = Number.isInteger(activePageParam) && activePageParam > 0 ? activePageParam : 1;
  const activeTotalPages = Math.max(1, Math.ceil(activeTrips.totalCount / activePageSize));

  const setActivePage = (nextPage: number) => {
    const next = new URLSearchParams(searchParams);
    const resolvedPage = Math.max(1, Math.min(nextPage, activeTotalPages));
    if (resolvedPage === 1) next.delete("executionPage");
    else next.set("executionPage", String(resolvedPage));
    setSearchParams(next, { replace: true });
  };

  const loadQueue = async (endpoint: string, page: number, pageSize: number, setter: React.Dispatch<React.SetStateAction<QueueState>>) => {
    setter((current) => ({ ...current, loading: true }));
    try {
      const result = await api<PagedResult<DispatchTripListItem>>(`${endpoint}?page=${page}&pageSize=${pageSize}`, { method: "GET" });
      setter({ items: result.items ?? [], totalCount: result.totalCount ?? 0, loading: false });
    } catch (error: any) {
      setter((current) => ({ ...current, loading: false }));
      show(error?.message ?? "Unable to load the execution workspace. Refresh and try again.", "error");
    }
  };

  const refreshAll = async (targetActivePage = activePage) => {
    await Promise.all([
      loadQueue("/api/dispatch/trips/ready", 1, readyPageSize, setReadyTrips),
      loadQueue("/api/dispatch/trips/active", targetActivePage, activePageSize, setActiveTrips),
      loadQueue("/api/dispatch/trips/on-hold", 1, issuePageSize, setOnHoldTrips),
      loadQueue("/api/dispatch/trips/failed-attempts", 1, issuePageSize, setFailedTrips)
    ]);
    setLastUpdatedAt(new Date());
  };

  useEffect(() => { void refreshAll(activePage); }, [activePage]);

  useEffect(() => {
    if (!activeTrips.loading && activePage > activeTotalPages) setActivePage(activeTotalPages);
  }, [activePage, activeTotalPages, activeTrips.loading]);

  useDispatchHub({
    onTripStatusChanged: () => { void refreshAll(); },
    onDriverLocationUpdated: () => { void refreshAll(); },
    onPlanningInvalidated: () => { void refreshAll(); }
  });

  const dispatchTrip = async (trip: DispatchTripListItem) => {
    if (!canDispatch) return;
    if (!trip.driverUserId || !trip.truckAssetId) {
      show("This ready plan is missing an assignment. Return it to Planning before dispatching.", "error");
      return;
    }
    setDispatchingTripId(trip.id);
    try {
      await api(`/api/dispatch/trips/${trip.id}/dispatch`, {
        method: "POST",
        body: JSON.stringify({ driverUserId: trip.driverUserId, truckAssetId: trip.truckAssetId, rowVersion: trip.rowVersion, remarks: null })
      });
      show("Trip dispatched. The driver can now begin the pickup workflow.", "success");
      await refreshAll();
    } catch (error: any) {
      show(error?.message ?? "Dispatch failed because the trip changed. Refresh and try again.", "error");
      await refreshAll();
    } finally {
      setDispatchingTripId(null);
    }
  };

  const metrics = useMemo(() => [
    { label: "Ready to dispatch", value: readyTrips.totalCount, icon: Send, tone: "text-primary", detail: "Validated trip plans" },
    { label: "In execution", value: activeTrips.totalCount, icon: CircleDot, tone: "text-info", detail: "Trips moving now" },
    { label: "Operational issues", value: onHoldTrips.totalCount + failedTrips.totalCount, icon: AlertTriangle, tone: "text-destructive", detail: "Require resolution" },
    { label: "Live drivers", value: activeTrips.items.filter(isLocationLive).length, icon: Wifi, tone: "text-success", detail: "Location reporting" }
  ], [activeTrips.items, activeTrips.totalCount, failedTrips.totalCount, onHoldTrips.totalCount, readyTrips.totalCount]);

  const issues = [...onHoldTrips.items, ...failedTrips.items];
  const isLoading = readyTrips.loading || activeTrips.loading || onHoldTrips.loading || failedTrips.loading;

  return (
    <div className="space-y-5">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Dispatch execution"
        description="Release validated trips, monitor live work, and resolve operational exceptions."
        breadcrumbs={<nav className="flex items-center gap-2" aria-label="Breadcrumb"><Link to="/dispatch/board" className="text-muted-foreground hover:text-foreground">Dispatch</Link><span className="text-muted-foreground">/</span><span>Execution</span></nav>}
        actions={<Button variant="outline" size="sm" onClick={() => void refreshAll()} disabled={isLoading}><RefreshCw className={`h-4 w-4 ${isLoading ? "animate-spin" : ""}`} /> Refresh</Button>}
      />

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4" aria-label="Execution summary">
        {metrics.map(({ label, value, icon: Icon, tone, detail }) => <div key={label} className="operations-kpi"><span className={`operations-kpi__icon ${tone}`}><Icon className="h-5 w-5" /></span><div><p className="operations-kpi__value tabular-nums">{isLoading ? "…" : value}</p><p className="operations-kpi__label">{label}</p><p className="mt-1 text-xs text-muted-foreground">{detail}</p></div></div>)}
      </section>

      <section className="surface-card overflow-hidden" aria-labelledby="ready-queue-heading">
        <div className="flex flex-col gap-3 border-b border-border p-4 sm:flex-row sm:items-center sm:justify-between sm:p-5">
          <div><p className="text-xs font-semibold uppercase tracking-[0.18em] text-primary">Execution handoff</p><h2 id="ready-queue-heading" className="mt-1 text-lg font-semibold">Ready queue</h2><p className="mt-1 text-sm text-muted-foreground">These trips passed Planning validation and are ready to release to their assigned driver.</p></div>
          <Link to="/dispatch/planning"><Button variant="outline" size="sm">Open Planning <ArrowRight className="h-4 w-4" /></Button></Link>
        </div>
        {readyTrips.loading && readyTrips.items.length === 0 ? <div className="p-5"><LoadingSkeleton rows={4} /></div> : readyTrips.items.length === 0 ? <div className="p-5"><EmptyState title="No trips ready to dispatch" description="Complete final validation in Planning to create the next execution handoff." /></div> : <div className="divide-y divide-border">{readyTrips.items.map((trip) => <article key={trip.id} className="grid gap-4 p-4 md:grid-cols-[minmax(0,1.25fr)_minmax(0,1fr)_auto] md:items-center md:px-5"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><span className="font-mono text-sm font-semibold">{trip.containerNumber || `TRIP-${trip.id.slice(0, 8).toUpperCase()}`}</span><StatusBadge status={trip.status} /></div><p className="mt-1 text-xs text-muted-foreground">{trip.customer?.name || "Customer pending"} · Pickup {formatDateTime(trip.pickupScheduledAt)}</p><div className="mt-3"><RouteSummary trip={trip} /></div></div><div className="grid gap-2 text-sm sm:grid-cols-2 md:grid-cols-1"><p className="inline-flex items-center gap-2"><UserRound className="h-4 w-4 text-muted-foreground" />{trip.driverUsername || "Driver missing"}</p><p className="inline-flex items-center gap-2"><Truck className="h-4 w-4 text-muted-foreground" />{trip.truckAssetCode || "Truck missing"}</p></div><div className="flex flex-col gap-2 sm:flex-row md:flex-col md:items-stretch"><Button variant="outline" size="sm" onClick={() => navigate(`/dispatch/trips/${trip.id}`)}>Review</Button>{canDispatch ? <Button size="sm" onClick={() => void dispatchTrip(trip)} disabled={dispatchingTripId === trip.id}>{dispatchingTripId === trip.id ? "Dispatching…" : "Dispatch trip"}<Send className="h-4 w-4" /></Button> : <p className="max-w-[180px] text-xs text-muted-foreground">View-only access. A dispatcher or manager releases the trip.</p>}</div></article>)}</div>}
      </section>

      <section className="grid gap-5 xl:grid-cols-[minmax(0,1.65fr)_minmax(320px,0.8fr)]">
        <div className="surface-card overflow-hidden" aria-labelledby="active-trips-heading">
          <div className="flex items-center justify-between gap-3 border-b border-border p-4 sm:p-5"><div><p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">Live operations</p><h2 id="active-trips-heading" className="mt-1 text-lg font-semibold">Trips in execution</h2></div><span className="inline-flex items-center gap-2 text-xs text-muted-foreground"><LocateFixed className="h-3.5 w-3.5" />{lastUpdatedAt ? `Updated ${formatDateTime(lastUpdatedAt.toISOString())}` : "Loading live state"}</span></div>
          {activeTrips.loading && activeTrips.items.length === 0 ? <div className="p-5"><LoadingSkeleton rows={activePageSize} /></div> : activeTrips.items.length === 0 ? <div className="p-5"><EmptyState title="No trips in execution" description="Dispatch a ready trip when the next movement is due." /></div> : <><div>{activeTrips.items.map((trip) => <ExecutionTripRow key={trip.id} trip={trip} onOpen={() => navigate(`/dispatch/trips/${trip.id}`)} />)}</div><div className="flex flex-wrap items-center justify-between gap-3 border-t border-border px-4 py-3 text-xs text-muted-foreground sm:px-5"><span>{(activePage - 1) * activePageSize + 1}-{Math.min(activePage * activePageSize, activeTrips.totalCount)} of {activeTrips.totalCount} active trips</span><div className="flex items-center gap-2"><Button variant="outline" size="sm" disabled={activePage <= 1 || activeTrips.loading} onClick={() => setActivePage(activePage - 1)}>Previous</Button><span className="whitespace-nowrap">Page {activePage} of {activeTotalPages}</span><Button variant="outline" size="sm" disabled={activePage >= activeTotalPages || activeTrips.loading} onClick={() => setActivePage(activePage + 1)}>Next</Button></div></div></>}
        </div>

        <aside className="surface-card p-4 sm:p-5" aria-labelledby="issues-heading">
          <div className="flex items-start justify-between gap-3"><div><p className="text-xs font-semibold uppercase tracking-[0.18em] text-destructive">Needs attention</p><h2 id="issues-heading" className="mt-1 text-lg font-semibold">Resolve exceptions</h2><p className="mt-1 text-sm text-muted-foreground">Holds and failed attempts are kept separate from the normal execution flow.</p></div><ShieldAlert className="h-5 w-5 text-destructive" /></div>
          <div className="mt-5 space-y-6"><IssueQueue title="On hold" description="Paused work" items={onHoldTrips.items} loading={onHoldTrips.loading} onOpen={(id) => navigate(`/dispatch/trips/${id}`)} /><IssueQueue title="Failed attempts" description="Recovery required" items={failedTrips.items} loading={failedTrips.loading} onOpen={(id) => navigate(`/dispatch/trips/${id}`)} /></div>
          {issues.length === 0 && !onHoldTrips.loading && !failedTrips.loading ? <p className="mt-5 border-t border-border pt-4 text-xs text-muted-foreground"><Route className="mr-1.5 inline h-3.5 w-3.5" />No exceptional trips require dispatcher action right now.</p> : null}
        </aside>
      </section>

      <div className="flex flex-col gap-2 rounded-xl border border-border bg-muted/20 px-4 py-3 text-sm sm:flex-row sm:items-center sm:justify-between"><p className="text-muted-foreground"><Clock3 className="mr-2 inline h-4 w-4" />Planning owns schedules and resource recommendations. Execution owns dispatch release, live progress, and recovery.</p><Link to="/dispatch/trips" className="inline-flex items-center gap-2 font-semibold text-primary hover:text-primary/80">Browse trip records <ArrowRight className="h-4 w-4" /></Link></div>
    </div>
  );
}
