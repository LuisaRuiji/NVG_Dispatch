import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import { getMe } from "@/features/auth/authStore";
import type {
  DispatchTripDetail,
  DispatchTripListItem,
  TripStatus
} from "./types";
import { statusLabels } from "./types";
import { AlertTriangle, ArrowRight, Check, ChevronDown, ChevronLeft, ChevronRight, CircleDot, FileWarning, Filter, MoreHorizontal, RefreshCw, ShieldAlert, Truck, UserRound, X } from "lucide-react";
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

type FilterOption = { value: string; label: string };

function FilterSelect({
  label,
  value,
  options,
  onChange
}: {
  label: string;
  value: string;
  options: FilterOption[];
  onChange: (value: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const selectRef = useRef<HTMLDivElement | null>(null);
  const selected = options.find((option) => option.value === value) ?? options[0];

  useEffect(() => {
    if (!open) return;
    const closeOnOutsidePress = (event: MouseEvent) => {
      if (!selectRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    window.addEventListener("mousedown", closeOnOutsidePress);
    window.addEventListener("keydown", closeOnEscape);
    return () => {
      window.removeEventListener("mousedown", closeOnOutsidePress);
      window.removeEventListener("keydown", closeOnEscape);
    };
  }, [open]);

  return (
    <div ref={selectRef} className="relative">
      <p className="text-xs uppercase text-muted-foreground">{label}</p>
      <button
        type="button"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-label={`${label}: ${selected.label}`}
        onClick={() => setOpen((value) => !value)}
        className="mt-1.5 flex h-10 w-full items-center justify-between rounded-lg border border-border bg-card px-3 text-left text-sm text-foreground transition-colors hover:border-primary/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <span className="truncate">{selected.label}</span>
        <ChevronDown className={`h-4 w-4 shrink-0 text-muted-foreground transition-transform duration-150 ${open ? "rotate-180" : ""}`} aria-hidden="true" />
      </button>
      {open ? (
        <div role="listbox" aria-label={label} className="filter-select-menu absolute z-30 mt-1 max-h-64 w-full overflow-y-auto rounded-lg border border-border bg-card p-1">
          {options.map((option) => {
            const isSelected = option.value === value;
            return (
              <button
                key={option.value}
                type="button"
                role="option"
                aria-selected={isSelected}
                onClick={() => {
                  onChange(option.value);
                  setOpen(false);
                }}
                className={`flex min-h-9 w-full items-center justify-between rounded-md px-2.5 py-2 text-left text-sm transition-colors ${isSelected ? "bg-accent font-semibold text-foreground" : "text-foreground hover:bg-muted"}`}
              >
                <span className="truncate">{option.label}</span>
                {isSelected ? <Check className="h-4 w-4 shrink-0 text-primary" aria-hidden="true" /> : null}
              </button>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}

type PaginationToken = number | "leading-ellipsis" | "trailing-ellipsis";

function getPaginationTokens(page: number, totalPages: number): PaginationToken[] {
  if (totalPages <= 7) return Array.from({ length: totalPages }, (_, index) => index + 1);

  const tokens: PaginationToken[] = [1];
  const start = Math.max(2, page - 1);
  const end = Math.min(totalPages - 1, page + 1);

  if (start > 2) tokens.push("leading-ellipsis");
  for (let current = start; current <= end; current += 1) tokens.push(current);
  if (end < totalPages - 1) tokens.push("trailing-ellipsis");
  tokens.push(totalPages);
  return tokens;
}

function TripListPagination({
  page,
  totalPages,
  totalCount,
  pageSize,
  loading,
  onPageChange,
  onPageSizeChange
}: {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  loading: boolean;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}) {
  const [jumpPage, setJumpPage] = useState(String(page));
  const tokens = getPaginationTokens(page, totalPages);

  useEffect(() => setJumpPage(String(page)), [page]);

  const submitJump = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const requestedPage = Number(jumpPage);
    if (!Number.isInteger(requestedPage)) return;
    onPageChange(Math.max(1, Math.min(requestedPage, totalPages)));
  };
  const firstResult = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const lastResult = Math.min(page * pageSize, totalCount);

  return (
    <div className="flex flex-col gap-3 border-t border-border px-4 py-3 text-xs text-muted-foreground sm:px-5 lg:flex-row lg:items-center lg:justify-between">
      <span aria-live="polite">{loading ? "Loading page..." : `${firstResult}-${lastResult} of ${totalCount.toLocaleString()}`}</span>
      <div className="flex flex-wrap items-center gap-1.5">
        <Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => onPageChange(page - 1)} aria-label="Previous page"><ChevronLeft className="h-4 w-4" />Previous</Button>
        <div className="flex items-center gap-1" aria-label="Trip list pages">
          {tokens.map((token) => typeof token === "number" ? (
            <button
              key={token}
              type="button"
              disabled={loading}
              aria-label={`Page ${token}`}
              aria-current={token === page ? "page" : undefined}
              onClick={() => onPageChange(token)}
              className={`grid h-9 min-w-9 place-items-center rounded-lg border px-2 text-xs font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50 ${token === page ? "border-primary bg-accent text-foreground" : "border-border bg-card text-muted-foreground hover:bg-muted hover:text-foreground"}`}
            >
              {token}
            </button>
          ) : <span key={token} className="grid h-9 w-5 place-items-center text-muted-foreground" aria-hidden="true">...</span>)}
        </div>
        <Button variant="outline" size="sm" disabled={page >= totalPages || loading} onClick={() => onPageChange(page + 1)} aria-label="Next page">Next<ChevronRight className="h-4 w-4" /></Button>
        <form className="ml-1 flex items-center gap-1.5" onSubmit={submitJump}>
          <label htmlFor="trip-list-page" className="sr-only">Go to page</label>
          <input id="trip-list-page" type="number" min={1} max={totalPages} inputMode="numeric" value={jumpPage} onChange={(event) => setJumpPage(event.target.value)} disabled={loading} className="h-9 w-16 rounded-lg border border-border bg-card px-2 text-center text-xs text-foreground focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50" />
          <Button variant="outline" size="sm" type="submit" disabled={loading}>Go to</Button>
        </form>
        <label className="ml-1 flex h-9 items-center gap-1.5 rounded-lg border border-border bg-card px-2 text-xs text-muted-foreground">
          <span className="sr-only">Rows per page</span>
          Rows
          <select value={pageSize} onChange={(event) => onPageSizeChange(Number(event.target.value))} disabled={loading} className="bg-transparent text-xs font-semibold text-foreground focus:outline-none disabled:opacity-50">
            <option value={25}>25</option><option value={50}>50</option><option value={100}>100</option>
          </select>
        </label>
      </div>
    </div>
  );
}

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

type ReadinessTone = "alert" | "warning" | "success" | "info" | "neutral";

type TripReadiness = {
  label: string;
  detail?: string;
  tone: ReadinessTone;
  needsAttention: boolean;
};

function getTripReadiness(trip: DispatchTripListItem): TripReadiness {
  if (!trip.driverUsername && !trip.truckAssetCode) {
    return { label: "Blocked", detail: "Driver and truck not assigned", tone: "alert", needsAttention: true };
  }
  if (!trip.driverUsername) return { label: "Blocked", detail: "Driver not assigned", tone: "alert", needsAttention: true };
  if (!trip.truckAssetCode) return { label: "Blocked", detail: "Truck not assigned", tone: "alert", needsAttention: true };

  if (trip.status === "ON_HOLD" || trip.status === "FAILED_ATTEMPT") {
    return { label: statusLabels[trip.status], detail: "Operational recovery required", tone: "warning", needsAttention: true };
  }

  const atwState = trip.documents.find((document) => document.type === "ATW")?.state ?? "MISSING";
  if (["DRAFT", "READY_FOR_DISPATCH", "DISPATCHED", "ENROUTE_PICKUP"].includes(trip.status) && atwState !== "VERIFIED") {
    return {
      label: "Blocked",
      detail: atwState === "REJECTED" ? "ATW was rejected" : "Verified ATW required before dispatch",
      tone: "alert",
      needsAttention: true
    };
  }

  if (trip.status === "READY_FOR_DISPATCH") {
    return { label: "Ready", detail: "ATW verified — pickup documents follow after collection", tone: "success", needsAttention: false };
  }

  if (trip.status === "DELIVERED") {
    const closeoutBlockers = trip.missingRequiredDocumentCount + trip.rejectedRequiredDocumentCount;
    if (closeoutBlockers > 0) {
      return {
        label: "Closeout due",
        detail: `${closeoutBlockers} closeout document${closeoutBlockers === 1 ? "" : "s"} require attention`,
        tone: "warning",
        needsAttention: true
      };
    }
    return { label: "Ready to close", detail: "All closeout documents verified", tone: "success", needsAttention: false };
  }

  if (trip.status === "CLOSED") {
    return { label: "Closed", detail: "Trip documentation complete", tone: "success", needsAttention: false };
  }
  if (["DISPATCHED", "ENROUTE_PICKUP", "AT_PICKUP", "LOADED", "ENROUTE_DROPOFF", "AT_DROPOFF"].includes(trip.status)) {
    return { label: "In transit", detail: statusLabels[trip.status], tone: "info", needsAttention: false };
  }
  return { label: statusLabels[trip.status], detail: "Review current trip state", tone: "neutral", needsAttention: false };
}

function getTripActionLabel(trip: DispatchTripListItem, canOperate: boolean) {
  if (!canOperate) return "Open trip";
  const readiness = getTripReadiness(trip);
  if (!trip.driverUsername || !trip.truckAssetCode) return "Assign assets";
  if (readiness.label === "Blocked" && readiness.detail?.includes("ATW")) return "Review ATW";
  if (readiness.label === "Closeout due") return "Complete closeout";
  if (readiness.label === "At risk") return "Review trip";
  return "Open trip";
}

const readinessStyles: Record<ReadinessTone, string> = {
  alert: "border-destructive/30 bg-destructive/10 text-destructive",
  warning: "border-warning/40 bg-warning/15 text-warning-foreground",
  success: "border-success/35 bg-success/10 text-success",
  info: "border-info/30 bg-info/10 text-info",
  neutral: "border-border bg-muted text-muted-foreground"
};

function ReadinessCell({ trip }: { trip: DispatchTripListItem }) {
  const readiness = getTripReadiness(trip);
  return <div className="min-w-0"><span className={`inline-flex max-w-full items-center gap-1 truncate rounded-md border px-2 py-1 text-[10px] font-bold uppercase tracking-[0.08em] ${readinessStyles[readiness.tone]}`}>{readiness.needsAttention ? <AlertTriangle className="h-3 w-3 shrink-0" aria-hidden="true" /> : null}<span className="truncate">{readiness.label}</span></span><p className="mt-1.5 line-clamp-2 text-xs leading-4 text-muted-foreground">{readiness.detail}</p></div>;
}

function AssignmentCell({ trip }: { trip: DispatchTripListItem }) {
  const missingDriver = !trip.driverUsername;
  const missingTruck = !trip.truckAssetCode;
  return <div className="min-w-0 space-y-1.5 text-xs"><p className={`flex min-w-0 items-center gap-1.5 ${missingDriver ? "font-semibold text-destructive" : "text-foreground"}`}>{missingDriver ? <AlertTriangle className="h-3.5 w-3.5 shrink-0" aria-hidden="true" /> : <UserRound className="h-3.5 w-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />}<span className="truncate">Driver: {trip.driverUsername ?? "Unassigned"}</span></p><p className={`flex min-w-0 items-center gap-1.5 ${missingTruck ? "font-semibold text-destructive" : "text-foreground"}`}>{missingTruck ? <AlertTriangle className="h-3.5 w-3.5 shrink-0" aria-hidden="true" /> : <Truck className="h-3.5 w-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />}<span className="truncate">Truck: {trip.truckAssetCode ?? "Unassigned"}</span></p></div>;
}

function getRouteLabel(trip: DispatchTripListItem) {
  const origin = trip.pickupLocation?.trim();
  const destination = trip.dropoffLocation?.trim();
  if (origin && destination) return `${origin} to ${destination}`;
  return origin ?? destination ?? "Route not scheduled";
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
  const [pageSize, setPageSize] = useState(25);
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
  const [advancedFiltersOpen, setAdvancedFiltersOpen] = useState(false);
  const tripRequestSequence = useRef(0);

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
    const requestSequence = ++tripRequestSequence.current;
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
      if (requestSequence !== tripRequestSequence.current) return;
      setTrips(result.items ?? []);
      setTotalCount(result.totalCount ?? 0);
      setPage(result.page ?? targetPage);
    } catch (e: any) {
      if (requestSequence !== tripRequestSequence.current) return;
      console.error(e);
      show(e?.message ?? "Failed to load trips.", "error");
    } finally {
      if (requestSequence === tripRequestSequence.current) setLoading(false);
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
  }, [page, pageSize]);

  useEffect(() => {
    if (!menuOpenId) return;
    const handler = () => setMenuOpenId(null);
    window.addEventListener("click", handler);
    return () => window.removeEventListener("click", handler);
  }, [menuOpenId]);

  const openTrip = (tripId: string) => {
    nav(`/dispatch/trips/${tripId}`);
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

  const activeTripCount = trips.filter((trip) => ["DISPATCHED", "ENROUTE_PICKUP", "AT_PICKUP", "LOADED", "ENROUTE_DROPOFF", "AT_DROPOFF"].includes(trip.status)).length;
  const documentAttentionCount = trips.filter((trip) => !trip.closeDocumentReady || trip.podState === "MISSING" || trip.podState === "REJECTED").length;
  const exceptionCount = trips.filter((trip) => trip.status === "ON_HOLD" || trip.status === "FAILED_ATTEMPT").length;
  const activeFilterCount = [filters.status !== "ALL", Boolean(filters.driverId), Boolean(filters.customerId), Boolean(filters.truckId), Boolean(filters.pickupFrom), Boolean(filters.pickupTo), Boolean(filters.deliveredFrom), Boolean(filters.deliveredTo), filters.podStatus !== "ALL"].filter(Boolean).length;
  const clearFilters = () => setFilters({ status: "ALL", driverId: "", customerId: "", truckId: "", pickupFrom: "", pickupTo: "", deliveredFrom: "", deliveredTo: "", podStatus: "ALL" });
  return (
    <div className="space-y-5">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Trip records"
        description="Review dispatch status, document readiness, and operational exceptions across the fleet."
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

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4" aria-label="Trip record summary">
        {[
          { label: "Loaded trips", value: totalCount, detail: "Matching current filters", icon: Truck, tone: "text-primary", onClick: clearFilters },
          { label: "In execution", value: activeTripCount, detail: "On this page", icon: CircleDot, tone: "text-info", onClick: () => setFilters((current) => ({ ...current, status: "DISPATCHED" })) },
          { label: "Need documents", value: documentAttentionCount, detail: "On this page", icon: FileWarning, tone: "text-warning-foreground", onClick: () => setFilters((current) => ({ ...current, podStatus: "PENDING" })) },
          { label: "Exceptions", value: exceptionCount, detail: "On this page", icon: ShieldAlert, tone: "text-destructive", onClick: () => setFilters((current) => ({ ...current, status: "ON_HOLD" })) }
        ].map(({ label, value, detail, icon: Icon, tone, onClick }) => (
          <button key={label} type="button" onClick={onClick} className="operations-kpi text-left transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
            <span className={`operations-kpi__icon ${tone}`}><Icon className="h-5 w-5" /></span><span><span className="operations-kpi__value block tabular-nums">{value}</span><span className="operations-kpi__label block">{label}</span><span className="mt-1 block text-xs text-muted-foreground">{detail}</span></span>
          </button>
        ))}
      </section>

      <section className="surface-card relative z-20 overflow-visible" aria-labelledby="trip-filter-title">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-4 py-3 sm:px-5">
          <div><h2 id="trip-filter-title" className="text-sm font-semibold text-foreground">Filter trip records</h2><p className="mt-0.5 text-xs text-muted-foreground">Narrow the list by assignment, status, and time window.</p></div>
          <div className="flex items-center gap-2">
            {activeFilterCount > 0 ? <span className="rounded-md bg-accent px-2 py-1 text-xs font-semibold text-foreground">{activeFilterCount} active</span> : null}
            {activeFilterCount > 0 ? <Button variant="ghost" size="sm" onClick={clearFilters}><X className="h-4 w-4" />Clear</Button> : null}
            <Button variant="outline" size="sm" onClick={() => setAdvancedFiltersOpen((open) => !open)}><Filter className="h-4 w-4" />{advancedFiltersOpen ? "Fewer filters" : "More filters"}</Button>
          </div>
        </div>
        <div className="grid gap-3 p-4 sm:grid-cols-2 xl:grid-cols-4 sm:p-5">
          <FilterSelect label="Status" value={filters.status} onChange={(value) => setFilters((prev) => ({ ...prev, status: value as FilterState["status"] }))} options={[{ value: "ALL", label: "All" }, ...Object.entries(statusLabels).map(([value, label]) => ({ value, label }))]} />
          <FilterSelect label="Driver" value={filters.driverId} onChange={(value) => setFilters((prev) => ({ ...prev, driverId: value }))} options={[{ value: "", label: "All drivers" }, ...drivers.map((driver) => ({ value: driver.id, label: driver.username }))]} />
          <FilterSelect label="Customer" value={filters.customerId} onChange={(value) => setFilters((prev) => ({ ...prev, customerId: value }))} options={[{ value: "", label: "All customers" }, ...customers.map((customer) => ({ value: customer.id, label: customer.name }))]} />
          <FilterSelect label="Truck" value={filters.truckId} onChange={(value) => setFilters((prev) => ({ ...prev, truckId: value }))} options={[{ value: "", label: "All trucks" }, ...trucks.map((truck) => ({ value: truck.id, label: truck.assetCode }))]} />
          {advancedFiltersOpen ? <>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Pickup From</label>
            <input
              type="datetime-local"
              value={filters.pickupFrom}
              onChange={(e) => setFilters((prev) => ({ ...prev, pickupFrom: e.target.value }))}
              className="mt-1.5 h-10 w-full rounded-lg border border-border bg-card px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Pickup To</label>
            <input
              type="datetime-local"
              value={filters.pickupTo}
              onChange={(e) => setFilters((prev) => ({ ...prev, pickupTo: e.target.value }))}
              className="mt-1.5 h-10 w-full rounded-lg border border-border bg-card px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Delivered From</label>
            <input
              type="datetime-local"
              value={filters.deliveredFrom}
              onChange={(e) => setFilters((prev) => ({ ...prev, deliveredFrom: e.target.value }))}
              className="mt-1.5 h-10 w-full rounded-lg border border-border bg-card px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Delivered To</label>
            <input
              type="datetime-local"
              value={filters.deliveredTo}
              onChange={(e) => setFilters((prev) => ({ ...prev, deliveredTo: e.target.value }))}
              className="mt-1.5 h-10 w-full rounded-lg border border-border bg-card px-3 text-sm"
            />
          </div>
          <FilterSelect label="POD status" value={filters.podStatus} onChange={(value) => setFilters((prev) => ({ ...prev, podStatus: value as FilterState["podStatus"] }))} options={[{ value: "ALL", label: "All" }, { value: "VERIFIED", label: "Verified" }, { value: "PENDING", label: "Pending" }]} />
          </> : null}
        </div>
      </section>

      {loading && trips.length === 0 ? (
        <LoadingSkeleton rows={6} />
      ) : trips.length === 0 ? (
        <EmptyState title="No trips" description="No trips match the current filters." />
      ) : (
        <section className="surface-card overflow-hidden" aria-labelledby="trip-records-title">
          <header className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-4 py-3 sm:px-5">
            <div><h2 id="trip-records-title" className="text-lg font-bold text-foreground">Trip list</h2><p className="mt-0.5 text-xs text-muted-foreground">{totalCount.toLocaleString()} matching trip{totalCount === 1 ? "" : "s"}</p></div>
            <div className="flex items-center gap-3"><span className="text-xs text-muted-foreground">Page {page} of {totalPages}</span></div>
          </header>
          <div className="grid gap-3 p-4 md:hidden">
            {trips.map((trip) => (
              <article
                key={trip.id}
                className={`rounded-lg border border-border bg-card p-4 ${rowClass(trip)}`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                      <p className="text-xs uppercase tracking-[0.08em] text-muted-foreground">Trip</p>
                      <p className="mt-1 font-mono text-base font-bold text-foreground">{trip.containerNumber ?? trip.id.slice(0, 8).toUpperCase()}</p>
                      <p className="mt-1 text-xs text-muted-foreground">{trip.customer?.name ?? "Customer pending"}</p>
                  </div>
                  <ReadinessCell trip={trip} />
                </div>
                <div className="mt-3 rounded-lg border border-border bg-muted/20 px-3 py-2 text-sm">
                  <p className="font-medium text-foreground">{getRouteLabel(trip)}</p>
                  <p className="mt-1 text-xs text-muted-foreground">{formatWindow(trip)}</p>
                </div>
                <div className="mt-3"><AssignmentCell trip={trip} /></div>
                <Button className="mt-4 w-full" onClick={() => openTrip(trip.id)}>{getTripActionLabel(trip, canOperate)}<ArrowRight className="h-4 w-4" /></Button>
              </article>
            ))}
          </div>
          <div className="hidden md:block">
          <DataTable className="rounded-none border-0" tableClassName="table-fixed">
            <thead className="sticky top-0 z-10 bg-muted text-[10px] font-bold uppercase tracking-[0.12em] text-muted-foreground">
              <tr>
                <th className="w-[12%] px-3 py-3 text-left">Trip</th>
                <th className="w-[25%] px-3 py-3 text-left">Customer and route</th>
                <th className="w-[16%] px-3 py-3 text-left">Schedule</th>
                <th className="w-[19%] px-3 py-3 text-left">Assignments</th>
                <th className="w-[14%] px-3 py-3 text-left">Readiness</th>
                <th className="w-[14%] px-3 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {trips.map((trip) => {
                const readiness = getTripReadiness(trip);
                const hasSecondaryActions = canHold(trip) || canResolveFailed(trip) || canCancel(trip);
                return (
                <tr
                  key={trip.id}
                  className={`cursor-pointer text-sm transition-colors hover:bg-muted/70 ${readiness.needsAttention ? "border-l-[3px] border-l-destructive" : ""}`}
                  onClick={() => openTrip(trip.id)}
                >
                  <td className="px-3 py-3.5"><p className="truncate font-mono text-sm font-bold text-foreground">{trip.containerNumber ?? trip.id.slice(0, 8).toUpperCase()}</p><p className="mt-1 truncate text-xs text-muted-foreground">{statusLabels[trip.status]}</p></td>
                  <td className="px-3 py-3.5"><p className="truncate font-semibold text-foreground">{trip.customer?.name ?? "Customer pending"}</p><p className="mt-1 truncate text-xs text-muted-foreground" title={getRouteLabel(trip)}>{getRouteLabel(trip)}</p></td>
                  <td className="px-3 py-3.5"><p className={`line-clamp-2 text-xs font-medium ${trip.latePickup || trip.lateDelivery ? "text-destructive" : "text-foreground"}`}>{formatWindow(trip)}</p><p className="mt-1 truncate text-[11px] text-muted-foreground">{trip.latePickup || trip.lateDelivery ? "Schedule risk" : typeof trip.plannedDurationMinutes === "number" ? `${trip.plannedDurationMinutes} min window` : "Schedule pending"}</p></td>
                  <td className="px-3 py-3.5"><AssignmentCell trip={trip} /></td>
                  <td className="px-3 py-3.5"><ReadinessCell trip={trip} /></td>
                  <td className="px-3 py-3.5 text-right">
                    <div className="flex items-center justify-end gap-1.5" onClick={(event) => event.stopPropagation()}>
                      <Button size="sm" className="min-w-0 flex-1 px-2 text-xs" onClick={() => openTrip(trip.id)}><span className="truncate">{getTripActionLabel(trip, canOperate)}</span><ArrowRight className="h-3.5 w-3.5 shrink-0" /></Button>
                      {hasSecondaryActions ? (
                      <div className="relative shrink-0">
                          <button
                            type="button"
                            className="grid h-9 w-8 place-items-center rounded-lg border border-border text-muted-foreground hover:bg-muted hover:text-foreground"
                            onClick={() => setMenuOpenId((prev) => (prev === trip.id ? null : trip.id))}
                            aria-label={`More actions for trip ${trip.containerNumber ?? trip.id.slice(0, 8)}`}
                          ><MoreHorizontal className="h-4 w-4" /></button>
                          {menuOpenId === trip.id ? (
                            <div className="absolute right-0 z-30 mt-2 w-48 rounded-lg border border-border bg-card p-1">
                              {canHold(trip) ? <button className="w-full rounded-md px-3 py-2 text-left text-xs font-medium text-foreground hover:bg-muted" onClick={() => { setMenuOpenId(null); setActionModal({ type: "HOLD", trip, remarks: "", eventAt: toLocalInput(new Date().toISOString()) }); }}>Place on hold</button> : null}
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
              )})}
            </tbody>
          </DataTable>
          </div>

          <TripListPagination page={page} totalPages={totalPages} totalCount={totalCount} pageSize={pageSize} loading={loading} onPageChange={setPage} onPageSizeChange={(nextPageSize) => { setPageSize(nextPageSize); setPage(1); }} />
        </section>
      )}

      {canOperate ? <RecommendationPanel /> : null}

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
