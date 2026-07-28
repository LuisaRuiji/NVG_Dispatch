import { useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import {
  ArrowRight,
  Check,
  CheckCircle2,
  Clock3,
  FileSearch,
  FileWarning,
  MapPin,
  RefreshCw,
  Search,
  SlidersHorizontal,
  X,
} from "lucide-react";
import PageHeader from "@/components/PageHeader";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import StatusBadge from "@/components/StatusBadge";
import ToastHost from "@/components/ToastHost";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { LocationMap } from "@/components/ui/LocationMap";
import { Textarea } from "@/components/ui/textarea";
import { api, previewFile } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import { useToast } from "@/lib/useToast";

type RequestStatus = "SUBMITTED" | "NEEDS_REVISION" | "REJECTED" | "APPROVED";
type RequestPriority = "CRITICAL" | "HIGH" | "NORMAL";
type ContainerSize = "TWENTY_FT" | "FORTY_FT" | "FORTY_HC";
type TripType = "PORT_PICKUP" | "PORT_DROPOFF" | "YARD_TRANSFER" | "LONG_HAUL";
type AtwFilter = "ALL" | "UPLOADED" | "MISSING";
type QueueSort = "PRIORITY" | "REQUESTED_TIME" | "REQUESTED_TIME_DESC" | "NEWEST" | "OLDEST" | "CUSTOMER";
type ReviewAction = "REQUEST_CHANGES" | "REJECT";

type DispatchRequestItem = {
  id: string;
  customerId: string;
  customerName: string;
  pickupLocation: string;
  pickupLatitude?: number | null;
  pickupLongitude?: number | null;
  dropoffLocation: string;
  dropoffLatitude?: number | null;
  dropoffLongitude?: number | null;
  requestedPickupTime?: string | null;
  containerSize: ContainerSize;
  tripType: TripType;
  containerNumber?: string | null;
  shippingLine?: string | null;
  bookingNumber?: string | null;
  documentsCount: number;
  atwDocumentId?: string | null;
  atwOriginalFileName?: string | null;
  atwAnalysisStatus?: string | null;
  atwUploadedAt?: string | null;
  createdAt: string;
  status: RequestStatus;
  priority: RequestPriority;
  reviewRemarks?: string | null;
};

type RequestDocument = {
  id: string;
  documentType: string;
  originalFileName?: string | null;
  contentType?: string | null;
  sizeBytes?: number | null;
  analysisStatus: string;
  analysisError?: string | null;
  extractionConfidence?: number | null;
  uploadedByUsername?: string | null;
  uploadedAt: string;
};

type RequestDetail = Omit<DispatchRequestItem, "documentsCount" | "priority"> & {
  cargoDescription?: string | null;
  cargoWeight?: number | null;
  specialInstructions?: string | null;
  approvedAt?: string | null;
  convertedTripId?: string | null;
  documents: RequestDocument[];
  activity: Array<{ action: string; actorUsername?: string | null; createdAt: string }>;
  assignment?: {
    tripId: string;
    driverUsername?: string | null;
    truckAssetCode?: string | null;
    trailerAssetCode?: string | null;
  } | null;
};

const containerLabels: Record<ContainerSize, string> = {
  TWENTY_FT: "20 ft",
  FORTY_FT: "40 ft",
  FORTY_HC: "40 HC"
};

const tripTypeLabels: Record<TripType, string> = {
  PORT_PICKUP: "Port pickup",
  PORT_DROPOFF: "Port dropoff",
  YARD_TRANSFER: "Yard transfer",
  LONG_HAUL: "Long haul"
};

const priorityStyles: Record<RequestPriority, string> = {
  CRITICAL: "border-destructive/30 bg-destructive/10 text-destructive",
  HIGH: "border-warning/30 bg-warning/10 text-warning-foreground",
  NORMAL: "border-border bg-muted/50 text-muted-foreground"
};

const formatDateTime = (value?: string | null) => {
  if (!value) return "Not scheduled";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value.replace("T", " ").slice(0, 16);
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit"
  }).format(date);
};

const formatAction = (value: string) =>
  value
    .replace(/^SHIPMENT_REQUEST_/, "")
    .toLowerCase()
    .replace(/_/g, " ")
    .replace(/^./, (character: string) => character.toUpperCase());

const formatBytes = (value?: number | null) => {
  if (!value) return "Size unavailable";
  if (value < 1024 * 1024) return `${Math.max(1, Math.round(value / 1024))} KB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
};

function SlaCountdown({ dueAt, now }: { dueAt?: string | null; now: number }) {
  if (!dueAt) return <span className="text-muted-foreground">No SLA time</span>;
  const due = new Date(dueAt).getTime();
  if (Number.isNaN(due)) return <span className="text-muted-foreground">Time unavailable</span>;
  const minutes = Math.round((due - now) / 60_000);
  if (minutes <= 0) return <span className="font-semibold text-destructive">Overdue by {Math.abs(minutes)}m</span>;
  if (minutes < 60) return <span className="font-semibold text-warning-foreground">{minutes}m remaining</span>;
  const hours = Math.floor(minutes / 60);
  const remainder = minutes % 60;
  return <span>{hours}h {remainder}m remaining</span>;
}

function RequestDetailsDrawer({
  requestId,
  onClose,
  onApprove,
  onReview,
  onPreview,
  busy
}: {
  requestId: string | null;
  onClose: () => void;
  onApprove: (id: string) => void;
  onReview: (action: ReviewAction, ids: string[]) => void;
  onPreview: (requestId: string, documentId: string) => void;
  busy: boolean;
}) {
  const [detail, setDetail] = useState<RequestDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const closeRef = useRef<HTMLButtonElement | null>(null);

  useEffect(() => {
    if (!requestId) {
      setDetail(null);
      return;
    }
    let cancelled = false;
    setError(null);
    setLoading(true);
    api<RequestDetail>(`/api/dispatch/requests/${requestId}`, { method: "GET" })
      .then((result) => {
        if (!cancelled) setDetail(result);
      })
      .catch((requestError: any) => {
        if (!cancelled) setError(requestError?.message ?? "Unable to load booking details.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [requestId]);

  useEffect(() => {
    if (!requestId) return;
    closeRef.current?.focus();
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [requestId, onClose]);

  if (!requestId) return null;

  const atw = detail?.documents.find((document) => document.documentType === "ATW");

  return (
    <div className="fixed inset-0 z-50" role="presentation">
      <button className="absolute inset-0 bg-foreground/25" aria-label="Close booking details" onClick={onClose} />
      <aside
        role="dialog"
        aria-modal="true"
        aria-labelledby="booking-drawer-title"
        className="absolute inset-y-0 right-0 flex w-full max-w-2xl flex-col border-l border-border bg-background shadow-2xl"
      >
        <div className="flex items-start justify-between gap-4 border-b border-border px-5 py-4 sm:px-6">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">Booking review</p>
            <h2 id="booking-drawer-title" className="mt-1 text-xl font-semibold text-foreground">
              {detail?.bookingNumber || `Request ${requestId.slice(0, 8).toUpperCase()}`}
            </h2>
            {detail ? <p className="mt-1 text-sm text-muted-foreground">{detail.customerName}</p> : null}
          </div>
          <Button ref={closeRef} variant="ghost" size="icon" onClick={onClose} aria-label="Close details">
            <X className="h-5 w-5" />
          </Button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5 sm:px-6">
          {loading ? (
            <LoadingSkeleton rows={7} />
          ) : error || !detail ? (
            <EmptyState title="Booking details unavailable" description={error ?? "The booking could not be loaded."} />
          ) : (
            <div className="space-y-7">
              <section aria-labelledby="route-title">
                <div className="flex items-center justify-between gap-3">
                  <h3 id="route-title" className="text-sm font-semibold text-foreground">Route and requested time</h3>
                  <StatusBadge status={detail.status} />
                </div>
                <div className="mt-3 grid gap-3 sm:grid-cols-[1fr_auto_1fr] sm:items-center">
                  <div>
                    <p className="text-xs uppercase tracking-[0.14em] text-muted-foreground">Pickup</p>
                    <p className="mt-1 text-sm font-medium text-foreground">{detail.pickupLocation}</p>
                  </div>
                  <ArrowRight className="hidden h-4 w-4 text-muted-foreground sm:block" />
                  <div>
                    <p className="text-xs uppercase tracking-[0.14em] text-muted-foreground">Dropoff</p>
                    <p className="mt-1 text-sm font-medium text-foreground">{detail.dropoffLocation}</p>
                  </div>
                </div>
                <div className="mt-3 flex items-center gap-2 text-sm text-muted-foreground">
                  <Clock3 className="h-4 w-4" />
                  {formatDateTime(detail.requestedPickupTime)}
                </div>
              </section>

              {(detail.pickupLatitude && detail.pickupLongitude) || (detail.dropoffLatitude && detail.dropoffLongitude) ? (
                <section aria-labelledby="map-title">
                  <h3 id="map-title" className="text-sm font-semibold text-foreground">Map</h3>
                  <div className="mt-3 grid gap-3 sm:grid-cols-2">
                    {detail.pickupLatitude && detail.pickupLongitude ? (
                      <LocationMap latitude={detail.pickupLatitude} longitude={detail.pickupLongitude} label={`Pickup: ${detail.pickupLocation}`} height="180px" />
                    ) : null}
                    {detail.dropoffLatitude && detail.dropoffLongitude ? (
                      <LocationMap latitude={detail.dropoffLatitude} longitude={detail.dropoffLongitude} label={`Dropoff: ${detail.dropoffLocation}`} height="180px" />
                    ) : null}
                  </div>
                </section>
              ) : null}

              <section aria-labelledby="cargo-title">
                <h3 id="cargo-title" className="text-sm font-semibold text-foreground">Container details</h3>
                <dl className="mt-3 grid grid-cols-2 gap-x-5 gap-y-3 text-sm sm:grid-cols-3">
                  <div><dt className="text-xs text-muted-foreground">Container</dt><dd className="mt-1 font-medium">{containerLabels[detail.containerSize]}</dd></div>
                  <div><dt className="text-xs text-muted-foreground">Movement</dt><dd className="mt-1 font-medium">{tripTypeLabels[detail.tripType]}</dd></div>
                  <div><dt className="text-xs text-muted-foreground">Container no.</dt><dd className="mt-1 font-medium">{detail.containerNumber || "Pending"}</dd></div>
                  <div><dt className="text-xs text-muted-foreground">Shipping line</dt><dd className="mt-1 font-medium">{detail.shippingLine || "Not provided"}</dd></div>
                  <div><dt className="text-xs text-muted-foreground">Cargo weight</dt><dd className="mt-1 font-medium">{detail.cargoWeight ? `${detail.cargoWeight.toLocaleString()} kg` : "Not provided"}</dd></div>
                  <div><dt className="text-xs text-muted-foreground">Submitted</dt><dd className="mt-1 font-medium">{formatDateTime(detail.createdAt)}</dd></div>
                </dl>
                {detail.cargoDescription || detail.specialInstructions ? (
                  <div className="mt-4 space-y-3 border-t border-border pt-4 text-sm">
                    {detail.cargoDescription ? <p><span className="text-muted-foreground">Cargo: </span>{detail.cargoDescription}</p> : null}
                    {detail.specialInstructions ? <p><span className="text-muted-foreground">Notes: </span>{detail.specialInstructions}</p> : null}
                  </div>
                ) : null}
              </section>

              <section aria-labelledby="documents-title">
                <div className="flex items-center justify-between gap-3">
                  <h3 id="documents-title" className="text-sm font-semibold text-foreground">Documents and ATW</h3>
                  {atw ? (
                    <Button variant="outline" size="sm" onClick={() => onPreview(detail.id, atw.id)}>
                      <FileSearch className="h-4 w-4" /> Preview ATW
                    </Button>
                  ) : null}
                </div>
                <div className="mt-3 divide-y divide-border border-y border-border">
                  {detail.documents.length === 0 ? (
                    <p className="py-4 text-sm text-muted-foreground">No documents were attached.</p>
                  ) : detail.documents.map((document) => (
                    <div key={document.id} className="flex items-center justify-between gap-4 py-3">
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium">{document.originalFileName || document.documentType}</p>
                        <p className="mt-0.5 text-xs text-muted-foreground">{formatBytes(document.sizeBytes)} · {formatDateTime(document.uploadedAt)}</p>
                      </div>
                      <span className="text-xs font-semibold text-muted-foreground">{document.analysisStatus.replace(/_/g, " ")}</span>
                    </div>
                  ))}
                </div>
              </section>

              <section aria-labelledby="activity-title">
                <h3 id="activity-title" className="text-sm font-semibold text-foreground">Timeline and activity log</h3>
                <ol className="mt-3 border-l border-border pl-4">
                  {detail.activity.length === 0 ? (
                    <li className="py-2 text-sm text-muted-foreground">No activity recorded yet.</li>
                  ) : detail.activity.map((item, index) => (
                    <li key={`${item.action}-${item.createdAt}-${index}`} className="relative pb-4 last:pb-0">
                      <span className="absolute -left-[1.3rem] top-1 h-2 w-2 rounded-full bg-primary" />
                      <p className="text-sm font-medium">{formatAction(item.action)}</p>
                      <p className="mt-0.5 text-xs text-muted-foreground">{item.actorUsername || "System"} · {formatDateTime(item.createdAt)}</p>
                    </li>
                  ))}
                </ol>
              </section>

              <section aria-labelledby="assignment-title">
                <h3 id="assignment-title" className="text-sm font-semibold text-foreground">Assignments</h3>
                <p className="mt-2 text-sm text-muted-foreground">
                  {detail.assignment
                    ? [detail.assignment.driverUsername, detail.assignment.truckAssetCode, detail.assignment.trailerAssetCode].filter(Boolean).join(" · ") || "Trip created; resources not assigned."
                    : "Assignments begin after approval in Planning."}
                </p>
              </section>
            </div>
          )}
        </div>

        {detail?.status === "SUBMITTED" ? (
          <div className="grid grid-cols-1 gap-2 border-t border-border bg-background px-5 py-4 sm:grid-cols-3 sm:px-6">
            <Button variant="outline" onClick={() => onReview("REQUEST_CHANGES", [detail.id])} disabled={busy}>Request changes</Button>
            <Button variant="outline" onClick={() => onReview("REJECT", [detail.id])} disabled={busy}>Reject</Button>
            <Button onClick={() => onApprove(detail.id)} disabled={busy}><Check className="h-4 w-4" /> Approve</Button>
          </div>
        ) : null}
      </aside>
    </div>
  );
}

function ReviewDialog({
  state,
  onClose,
  onConfirm,
  busy
}: {
  state: { action: ReviewAction; ids: string[] } | null;
  onClose: () => void;
  onConfirm: (remarks: string) => void;
  busy: boolean;
}) {
  const [remarks, setRemarks] = useState("");
  useEffect(() => setRemarks(""), [state]);
  if (!state) return null;
  const requestingChanges = state.action === "REQUEST_CHANGES";
  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-foreground/30 px-4" role="presentation">
      <div role="dialog" aria-modal="true" aria-labelledby="review-dialog-title" className="w-full max-w-lg rounded-2xl border border-border bg-background p-5 shadow-2xl sm:p-6">
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">{state.ids.length} selected</p>
            <h2 id="review-dialog-title" className="mt-1 text-lg font-semibold">{requestingChanges ? "Request customer changes" : "Reject shipment request"}</h2>
          </div>
          <Button variant="ghost" size="icon" onClick={onClose} aria-label="Close"><X className="h-5 w-5" /></Button>
        </div>
        <p className="mt-3 text-sm text-muted-foreground">
          {requestingChanges ? "Tell the customer exactly what must be corrected before they resubmit." : "Record a clear rejection reason for the customer and audit history."}
        </p>
        <div className="mt-5 space-y-2">
          <Label htmlFor="review-remarks">Remarks</Label>
          <Textarea id="review-remarks" value={remarks} onChange={(event) => setRemarks(event.target.value)} placeholder={requestingChanges ? "Example: Upload a readable ATW and confirm the booking number." : "Reason for rejection"} className="min-h-28" autoFocus />
        </div>
        <div className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <Button variant="outline" onClick={onClose} disabled={busy}>Cancel</Button>
          <Button variant={requestingChanges ? "default" : "destructive"} onClick={() => onConfirm(remarks)} disabled={busy || !remarks.trim()}>
            {requestingChanges ? "Send feedback" : "Reject request"}
          </Button>
        </div>
      </div>
    </div>
  );
}

export default function DispatchRequestsPage() {
  const { toasts, show } = useToast();
  const [requests, setRequests] = useState<DispatchRequestItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [status, setStatus] = useState<RequestStatus>("SUBMITTED");
  const [priority, setPriority] = useState<RequestPriority | "ALL">("ALL");
  const [atwFilter, setAtwFilter] = useState<AtwFilter>("ALL");
  const [sort, setSort] = useState<QueueSort>("PRIORITY");
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [drawerId, setDrawerId] = useState<string | null>(null);
  const [reviewDialog, setReviewDialog] = useState<{ action: ReviewAction; ids: string[] } | null>(null);
  const [now, setNow] = useState(Date.now());
  const pageSize = 20;

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 60_000);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const loadQueue = async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize), status, sort });
      if (search) params.set("search", search);
      if (priority !== "ALL") params.set("priority", priority);
      if (atwFilter !== "ALL") params.set("atwStatus", atwFilter);
      const result = await api<PagedResult<DispatchRequestItem>>(`/api/dispatch/requests?${params.toString()}`, { method: "GET" });
      setRequests(result.items ?? []);
      setTotalCount(result.totalCount ?? 0);
      setSelected(new Set());
    } catch (error: any) {
      show(error?.message ?? "Unable to load shipment requests.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadQueue();
  }, [page, status, priority, atwFilter, sort, search]);

  const selectedSubmittedIds = useMemo(
    () => requests.filter((item) => selected.has(item.id) && item.status === "SUBMITTED").map((item) => item.id),
    [requests, selected]
  );
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const runActions = async (ids: string[], action: "approve" | "request-changes" | "reject", remarks?: string) => {
    if (ids.length === 0) return;
    setBusy(true);
    const results: Array<{ id: string; ok: boolean }> = [];
    for (const id of ids) {
      try {
        await api(`/api/dispatch/requests/${id}/${action}`, {
          method: "POST",
          body: remarks ? JSON.stringify({ remarks }) : undefined
        });
        results.push({ id, ok: true });
      } catch {
        results.push({ id, ok: false });
      }
    }
    const succeeded = results.filter((result) => result.ok).length;
    const failed = results.length - succeeded;
    if (succeeded > 0) show(`${succeeded} request${succeeded === 1 ? "" : "s"} updated.`, "success");
    if (failed > 0) show(`${failed} request${failed === 1 ? "" : "s"} could not be updated. Refresh and try again.`, "error");
    setReviewDialog(null);
    setDrawerId(null);
    await loadQueue();
    setBusy(false);
  };

  const toggleAll = () => {
    if (selectedSubmittedIds.length === requests.filter((item) => item.status === "SUBMITTED").length) {
      setSelected(new Set());
      return;
    }
    setSelected(new Set(requests.filter((item) => item.status === "SUBMITTED").map((item) => item.id)));
  };

  const previewAtw = async (requestId: string, documentId: string) => {
    try {
      await previewFile(`/api/dispatch/requests/${requestId}/documents/${documentId}/content`);
    } catch (error: any) {
      show(error?.message ?? "ATW preview is unavailable.", "error");
    }
  };

  return (
    <div className="space-y-5">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Customer Requests"
        description="Review new customer submissions. Approved bookings move automatically to Planning."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/dispatch/board" className="text-muted-foreground hover:text-foreground">Dispatch</Link>
            <span className="text-muted-foreground">/</span>
            <span>Requests</span>
          </nav>
        }
        actions={<Button variant="outline" size="sm" onClick={() => void loadQueue()} disabled={loading}><RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} /> Refresh</Button>}
      />

      <section className="surface-card overflow-hidden" aria-label="Request review queue">
        <div className="border-b border-border p-4 sm:p-5">
          <div className="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
            <div className="flex flex-wrap gap-2" aria-label="Request status filters">
              {(["SUBMITTED", "NEEDS_REVISION", "REJECTED"] as RequestStatus[]).map((value) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => { setStatus(value); setPage(1); }}
                  className={`min-h-10 rounded-full border px-4 text-sm font-semibold transition-colors ${status === value ? "border-primary bg-primary text-primary-foreground" : "border-border bg-background text-muted-foreground hover:text-foreground"}`}
                >
                  {value === "SUBMITTED" ? "Needs review" : value === "NEEDS_REVISION" ? "With customer" : "Rejected"}
                </button>
              ))}
            </div>
            <Link to="/dispatch/planning" className="inline-flex min-h-10 items-center gap-2 self-start rounded-full border border-border px-4 text-sm font-semibold text-foreground hover:bg-muted xl:self-auto">
              Open Planning <ArrowRight className="h-4 w-4" />
            </Link>
          </div>

          <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-[minmax(260px,1fr)_180px_160px_210px]">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder="Search customer, booking, route, or container" className="pl-9" aria-label="Search requests" />
            </div>
            <label className="relative">
              <span className="sr-only">Priority</span>
              <select value={priority} onChange={(event) => { setPriority(event.target.value as RequestPriority | "ALL"); setPage(1); }} className="h-10 w-full rounded-xl border border-input bg-background px-3 text-sm">
                <option value="ALL">All priorities</option><option value="CRITICAL">Critical</option><option value="HIGH">High</option><option value="NORMAL">Normal</option>
              </select>
            </label>
            <label>
              <span className="sr-only">ATW status</span>
              <select value={atwFilter} onChange={(event) => { setAtwFilter(event.target.value as AtwFilter); setPage(1); }} className="h-10 w-full rounded-xl border border-input bg-background px-3 text-sm">
                <option value="ALL">All ATW</option><option value="UPLOADED">ATW uploaded</option><option value="MISSING">ATW missing</option>
              </select>
            </label>
            <label className="flex items-center gap-2">
              <SlidersHorizontal className="h-4 w-4 shrink-0 text-muted-foreground" />
              <select value={sort} onChange={(event) => setSort(event.target.value as QueueSort)} className="h-10 w-full rounded-xl border border-input bg-background px-3 text-sm">
                <option value="PRIORITY">Priority first</option><option value="REQUESTED_TIME">Requested time</option><option value="REQUESTED_TIME_DESC">Requested time, latest</option><option value="NEWEST">Newest submission</option><option value="OLDEST">Oldest submission</option><option value="CUSTOMER">Customer</option>
              </select>
            </label>
          </div>
        </div>

        {selectedSubmittedIds.length > 0 ? (
          <div className="flex flex-col gap-3 border-b border-primary/20 bg-primary/5 px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:px-5">
            <p className="text-sm font-semibold">{selectedSubmittedIds.length} submission{selectedSubmittedIds.length === 1 ? "" : "s"} selected</p>
            <div className="grid grid-cols-1 gap-2 sm:flex">
              <Button variant="outline" size="sm" onClick={() => setReviewDialog({ action: "REQUEST_CHANGES", ids: selectedSubmittedIds })}>Request changes</Button>
              <Button variant="outline" size="sm" onClick={() => setReviewDialog({ action: "REJECT", ids: selectedSubmittedIds })}>Reject</Button>
              <Button size="sm" onClick={() => void runActions(selectedSubmittedIds, "approve")}><Check className="h-4 w-4" /> Approve</Button>
            </div>
          </div>
        ) : null}

        {loading && requests.length === 0 ? (
          <div className="p-5"><LoadingSkeleton rows={6} /></div>
        ) : requests.length === 0 ? (
          <div className="p-8"><EmptyState title={status === "SUBMITTED" ? "Review queue is clear" : "No requests match these filters"} description={status === "SUBMITTED" ? "New customer submissions will appear here." : "Try another status or clear the search filters."} /></div>
        ) : (
          <div>
            <div className="hidden grid-cols-[40px_minmax(260px,1.2fr)_minmax(210px,1fr)_160px_150px_180px] gap-3 border-b border-border bg-muted/35 px-5 py-3 text-xs font-semibold uppercase tracking-[0.12em] text-muted-foreground lg:grid">
              <input type="checkbox" aria-label="Select all submitted requests" checked={selectedSubmittedIds.length > 0 && selectedSubmittedIds.length === requests.filter((item) => item.status === "SUBMITTED").length} onChange={toggleAll} className="h-4 w-4" />
              <span>Booking</span><span>Route</span><span>Requested</span><span>Readiness</span><span className="text-right">Actions</span>
            </div>
            <div className="divide-y divide-border">
              {requests.map((item) => (
                <article key={item.id} className="grid gap-4 px-4 py-4 transition-colors hover:bg-muted/20 sm:px-5 lg:grid-cols-[40px_minmax(260px,1.2fr)_minmax(210px,1fr)_160px_150px_180px] lg:items-center lg:gap-3">
                  <div className="flex items-center justify-between lg:block">
                    <input type="checkbox" aria-label={`Select booking ${item.bookingNumber || item.id.slice(0, 8)}`} checked={selected.has(item.id)} disabled={item.status !== "SUBMITTED"} onChange={() => setSelected((current) => { const next = new Set(current); next.has(item.id) ? next.delete(item.id) : next.add(item.id); return next; })} className="h-5 w-5" />
                    <div className="flex gap-2 lg:hidden"><StatusBadge status={item.status} /><span className={`rounded-full border px-2 py-1 text-[10px] font-semibold ${priorityStyles[item.priority]}`}>{item.priority}</span></div>
                  </div>
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="truncate text-sm font-semibold text-foreground">{item.bookingNumber || `Request ${item.id.slice(0, 8).toUpperCase()}`}</p>
                      <span className={`hidden rounded-full border px-2 py-0.5 text-[10px] font-semibold lg:inline-flex ${priorityStyles[item.priority]}`}>{item.priority}</span>
                    </div>
                    <p className="mt-1 truncate text-sm text-muted-foreground">{item.customerName}</p>
                    <p className="mt-1 text-xs text-muted-foreground">{containerLabels[item.containerSize]} · {tripTypeLabels[item.tripType]}{item.containerNumber ? ` · ${item.containerNumber}` : ""}</p>
                  </div>
                  <div className="min-w-0 text-sm">
                    <p className="truncate font-medium"><MapPin className="mr-1 inline h-3.5 w-3.5 text-muted-foreground" />{item.pickupLocation}</p>
                    <p className="mt-1 truncate text-muted-foreground">to {item.dropoffLocation}</p>
                  </div>
                  <div className="text-sm">
                    <p>{formatDateTime(item.requestedPickupTime)}</p>
                    <p className="mt-1 text-xs"><SlaCountdown dueAt={item.requestedPickupTime} now={now} /></p>
                  </div>
                  <div className="flex flex-wrap gap-2 lg:block">
                    {item.atwDocumentId ? <span className="inline-flex items-center gap-1 text-xs font-semibold text-success"><CheckCircle2 className="h-4 w-4" /> ATW uploaded</span> : <span className="inline-flex items-center gap-1 text-xs font-semibold text-warning-foreground"><FileWarning className="h-4 w-4" /> ATW missing</span>}
                    <div className="mt-1 hidden lg:block"><StatusBadge status={item.status} /></div>
                  </div>
                  <div className="grid grid-cols-2 gap-2 lg:flex lg:flex-col lg:items-stretch">
                    <Button variant="outline" size="sm" onClick={() => setDrawerId(item.id)}><FileSearch className="h-4 w-4" /> View booking</Button>
                    {item.atwDocumentId ? <Button variant="ghost" size="sm" onClick={() => void previewAtw(item.id, item.atwDocumentId!)}>Preview ATW</Button> : null}
                    {item.status === "SUBMITTED" ? <Button size="sm" onClick={() => void runActions([item.id], "approve")} disabled={busy}><Check className="h-4 w-4" /> Approve</Button> : null}
                  </div>
                </article>
              ))}
            </div>
          </div>
        )}

        <div className="flex items-center justify-between border-t border-border px-4 py-3 text-sm text-muted-foreground sm:px-5">
          <span>{totalCount === 0 ? "0 requests" : `${(page - 1) * pageSize + 1}–${Math.min(page * pageSize, totalCount)} of ${totalCount}`}</span>
          <div className="flex gap-2"><Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => setPage((value) => value - 1)}>Previous</Button><Button variant="outline" size="sm" disabled={page >= totalPages || loading} onClick={() => setPage((value) => value + 1)}>Next</Button></div>
        </div>
      </section>

      <RequestDetailsDrawer requestId={drawerId} onClose={() => setDrawerId(null)} onApprove={(id) => void runActions([id], "approve")} onReview={(action, ids) => setReviewDialog({ action, ids })} onPreview={(requestId, documentId) => void previewAtw(requestId, documentId)} busy={busy} />
      <ReviewDialog state={reviewDialog} onClose={() => setReviewDialog(null)} onConfirm={(remarks) => void runActions(reviewDialog?.ids ?? [], reviewDialog?.action === "REQUEST_CHANGES" ? "request-changes" : "reject", remarks.trim())} busy={busy} />
    </div>
  );
}
