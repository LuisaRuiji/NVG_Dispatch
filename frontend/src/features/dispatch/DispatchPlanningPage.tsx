import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  AlertTriangle,
  ArrowRight,
  CalendarDays,
  CalendarClock,
  CheckCircle2,
  ChevronRight,
  CircleDashed,
  Clock3,
  ClipboardCheck,
  Filter,
  Medal,
  MoreHorizontal,
  RefreshCw,
  Search,
  ShieldAlert,
  Truck,
  X
} from "lucide-react";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import ToastHost from "@/components/ToastHost";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useDispatchHub } from "@/hooks/useDispatchHub";
import { api } from "@/lib/api";
import { useToast } from "@/lib/useToast";
import type { DispatchTripDetail, TripStopType } from "./types";

type ApprovedBooking = {
  requestId: string;
  customerId: string;
  customerName: string;
  pickupLocation: string;
  pickupLatitude?: number | null;
  pickupLongitude?: number | null;
  dropoffLocation: string;
  dropoffLatitude?: number | null;
  dropoffLongitude?: number | null;
  requestedPickupTime?: string | null;
  containerSize?: string | null;
  tripType?: string | null;
  containerNumber?: string | null;
  shippingLine?: string | null;
  bookingNumber?: string | null;
  hasAtw: boolean;
  createdAt: string;
};

type PlanningConflict = {
  tripId: string;
  tripReference: string;
  windowStart: string;
  windowEnd: string;
  resources: string[];
};

type PlanningTrip = {
  tripId: string;
  status: "DRAFT" | "READY_FOR_DISPATCH";
  customerId: string;
  customerName: string;
  bookingNumber?: string | null;
  containerNumber?: string | null;
  containerSize?: string | null;
  tripType?: string | null;
  pickupLocation?: string | null;
  pickupLatitude?: number | null;
  pickupLongitude?: number | null;
  pickupScheduledAt?: string | null;
  dropoffLocation?: string | null;
  dropoffLatitude?: number | null;
  dropoffLongitude?: number | null;
  dropoffScheduledAt?: string | null;
  driverUserId?: string | null;
  driverUsername?: string | null;
  truckAssetId?: string | null;
  truckAssetCode?: string | null;
  trailerAssetId?: string | null;
  trailerAssetCode?: string | null;
  atwState: "MISSING" | "UPLOADED" | "VERIFIED" | "REJECTED";
  missingRequirements: string[];
  conflicts: PlanningConflict[];
  rowVersion: string;
  createdAt: string;
  updatedAt?: string | null;
};

type PlanningBoard = {
  approvedBookings: ApprovedBooking[];
  approvedTotalCount: number;
  draftTrips: PlanningTrip[];
  draftTotalCount: number;
  readyTrips: PlanningTrip[];
  readyCount: number;
  conflictCount: number;
  page: number;
  pageSize: number;
};

type PlanningCheck = { code: string; state: "Passed" | "Warning" | "Blocked"; message: string };
type PlanningExcludedResource = { resourceType: string; resourceId: string; resourceLabel: string; reasons: string[]; checks: PlanningCheck[] };
type PlanningRecommendation = {
  selectionRank: number;
  isRecommended: boolean;
  // Diagnostic fields are retained only for the internal, non-rendered legacy branch
  // until the client contract transition is complete.
  rank: number;
  driverUserId: string;
  driverName: string;
  truckAssetId: string;
  truckCode: string;
  trailerAssetId?: string | null;
  trailerCode?: string | null;
  score: number;
  criteriaContributions: { criterion: string; weight: number; explanation: string }[];
  reasons: string[];
  warnings: string[];
};
type PlanningDecisionSupport = {
  tripId: string;
  canMarkReady: boolean;
  resourcesEvaluated: boolean;
  bookingChecks: PlanningCheck[];
  availableAssignmentCount: number;
  excludedResources: PlanningExcludedResource[];
  recommendations: PlanningRecommendation[];
  recommendationToken: string;
  generatedAt: string;
  expiresAt: string;
  criteriaWeightVersion: string;
  availabilityVersion: number;
};

type PlanningResource = {
  id: string;
  code: string;
  label: string;
  capability?: string | null;
  operationalStatus: string;
  isAvailable: boolean;
  conflictTripId?: string | null;
  conflictTripReference?: string | null;
  busyUntil?: string | null;
};

type ResourceSnapshot = {
  drivers: PlanningResource[];
  trucks: PlanningResource[];
  trailers: PlanningResource[];
};

type PlanForm = {
  driverUserId: string;
  truckAssetId: string;
  trailerAssetId: string;
  containerNumber: string;
  pickupLocation: string;
  pickupScheduledAt: string;
  dropoffLocation: string;
  dropoffScheduledAt: string;
  notes: string;
  remarks: string;
};

const emptyResources: ResourceSnapshot = { drivers: [], trucks: [], trailers: [] };
const emptyDecisionSupport: PlanningDecisionSupport = {
  tripId: "",
  canMarkReady: false,
  resourcesEvaluated: false,
  bookingChecks: [],
  availableAssignmentCount: 0,
  excludedResources: [],
  recommendations: [],
  recommendationToken: "",
  generatedAt: "",
  expiresAt: "",
  criteriaWeightVersion: "",
  availabilityVersion: 0
};

const formatDateTime = (value?: string | null) => {
  if (!value) return "Unscheduled";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value.replace("T", " ").slice(0, 16);
  return new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric", hour: "numeric", minute: "2-digit" }).format(date);
};

const formatContainer = (value?: string | null) => {
  if (!value) return "Size pending";
  return value === "TWENTY_FT" || value === "TwentyFt" ? "20 ft" : value === "FORTY_HC" || value === "FortyHC" ? "40 HC" : "40 ft";
};

const formatTripType = (value?: string | null) =>
  value ? value.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/_/g, " ").toLowerCase().replace(/^./, (character: string) => character.toUpperCase()) : "Movement pending";

const toLocalInput = (value?: string | null) => {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value.slice(0, 16);
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
};

const toApiTime = (value: string) => value ? new Date(value).toISOString() : null;

const defaultFutureTime = () => {
  const date = new Date(Date.now() + 30 * 60_000);
  date.setMinutes(Math.ceil(date.getMinutes() / 15) * 15, 0, 0);
  return toLocalInput(date.toISOString());
};

type QueueFilter = "all" | "attention" | "due" | "planning" | "ready";
type QueuePriority = "high" | "medium" | "low";
type PlanningQueueItem =
  | { kind: "booking"; id: string; booking: ApprovedBooking; priority: QueuePriority; needsAttention: boolean; dueSoon: boolean }
  | { kind: "trip"; id: string; trip: PlanningTrip; priority: QueuePriority; needsAttention: boolean; dueSoon: boolean };

const isDueSoon = (value?: string | null) => {
  if (!value) return false;
  const time = new Date(value).getTime();
  const now = Date.now();
  return !Number.isNaN(time) && time >= now && time <= now + 2 * 60 * 60 * 1000;
};

const isPastDue = (value?: string | null) => {
  if (!value) return false;
  const time = new Date(value).getTime();
  return !Number.isNaN(time) && time < Date.now();
};

const bookingNeedsAttention = (booking: ApprovedBooking) => !booking.hasAtw || isPastDue(booking.requestedPickupTime);
const tripNeedsAttention = (trip: PlanningTrip) => trip.status !== "READY_FOR_DISPATCH" && (trip.conflicts.length > 0 || trip.missingRequirements.length > 0 || trip.atwState !== "VERIFIED" || isPastDue(trip.pickupScheduledAt));

const priorityForBooking = (booking: ApprovedBooking): QueuePriority =>
  bookingNeedsAttention(booking) ? "high" : isDueSoon(booking.requestedPickupTime) ? "medium" : "low";

const priorityForTrip = (trip: PlanningTrip): QueuePriority => {
  if (tripNeedsAttention(trip)) return "high";
  if (isDueSoon(trip.pickupScheduledAt)) return "medium";
  return "low";
};

function Requirement({ label, ready, optional = false }: { label: string; ready: boolean; optional?: boolean }) {
  const status = ready ? "Ready" : optional ? "Optional" : "Missing";
  return (
    <div className="flex min-w-0 items-center gap-1.5 text-xs">
      {ready ? <CheckCircle2 className="h-3.5 w-3.5 shrink-0 text-emerald-600" /> : optional ? <CircleDashed className="h-3.5 w-3.5 shrink-0 text-muted-foreground" /> : <AlertTriangle className="h-3.5 w-3.5 shrink-0 text-destructive" />}
      <span className="truncate text-muted-foreground">{label}</span>
      <span className={`ml-auto shrink-0 font-medium ${ready ? "text-emerald-700 dark:text-emerald-300" : optional ? "text-muted-foreground" : "text-destructive"}`}>{status}</span>
    </div>
  );
}

function PlanningQueueRow({
  item,
  busy,
  onStart,
  onOpen,
  onView
}: {
  item: PlanningQueueItem;
  busy: boolean;
  onStart: (booking: ApprovedBooking) => void;
  onOpen: (tripId: string) => void;
  onView: (tripId: string) => void;
}) {
  const isBooking = item.kind === "booking";
  const booking = isBooking ? item.booking : null;
  const trip = isBooking ? null : item.trip;
  const priorityStyles = {
    high: { rail: "bg-[#991B1B]", text: "text-[#991B1B]", action: "border-[#FECACA] bg-[#FEE2E2]" },
    medium: { rail: "bg-[#92400E]", text: "text-[#92400E]", action: "border-[#FDE68A] bg-[#FEF3C7]" },
    low: { rail: "bg-[#065F46]", text: "text-[#065F46]", action: "border-[#A7F3D0] bg-[#D1FAE5]" }
  }[item.priority];

  const identifier = booking?.bookingNumber || trip?.bookingNumber || `${isBooking ? "Request" : "Trip"} ${item.id.slice(0, 8).toUpperCase()}`;
  const customer = booking?.customerName || trip?.customerName || "Customer pending";
  const pickup = booking?.pickupLocation || trip?.pickupLocation || "Pickup pending";
  const dropoff = booking?.dropoffLocation || trip?.dropoffLocation || "Drop-off pending";
  const pickupTime = booking?.requestedPickupTime || trip?.pickupScheduledAt;
  const dropoffTime = trip?.dropoffScheduledAt;
  const isReady = trip?.status === "READY_FOR_DISPATCH";

  const nextAction = (() => {
    if (booking) {
      if (!booking.hasAtw) return { message: "ATW still needs review", label: "Start planning" };
      if (isPastDue(booking.requestedPickupTime)) return { message: "Confirm a future pickup time", label: "Set schedule" };
      return { message: "Ready to build a trip plan", label: "Start planning" };
    }
    if (!trip) return { message: "Review plan details", label: "Open trip" };
    if (trip.conflicts.length > 0) return { message: "Resolve assignment conflict", label: "Resolve conflict" };
    if (!trip.driverUserId) return { message: "Driver assignment required", label: "Assign driver" };
    if (!trip.truckAssetId) return { message: "Truck assignment required", label: "Assign truck" };
    if (!trip.containerNumber) return { message: "Container number required", label: "Add container" };
    if (trip.atwState !== "VERIFIED") return { message: "ATW verification required", label: "Review documents" };
    if (isReady) return { message: "All dispatch gates have passed", label: "Review ready plan" };
    return { message: "Schedule and assignments need review", label: "Complete plan" };
  })();

  const handleAction = () => {
    if (booking) onStart(booking);
    if (trip) onOpen(trip.tripId);
  };

  return (
    <article className="relative grid min-w-[1080px] grid-cols-[78px_minmax(130px,0.85fr)_minmax(200px,1.35fr)_minmax(175px,1fr)_220px_40px] items-center gap-3 overflow-hidden rounded-xl border border-border bg-card p-4">
      <span aria-hidden="true" className={`absolute inset-y-0 left-0 w-[5px] ${priorityStyles.rail}`} />
      <div className="pl-3">
        <p className={`text-[11px] font-bold uppercase tracking-[0.08em] ${priorityStyles.text}`}>{item.priority}</p>
        <p className="mt-2 text-xs text-muted-foreground">{isBooking ? "New booking" : isReady ? "Ready plan" : "Trip plan"}</p>
      </div>

      <div className="min-w-0 border-l border-border pl-3">
        <p className="truncate font-mono text-sm font-bold text-foreground">{identifier}</p>
        <p className="mt-1 truncate text-xs text-muted-foreground">{customer}</p>
        <div className="mt-2 flex flex-wrap gap-1.5">
          <span className="rounded-md bg-muted px-1.5 py-0.5 text-[10px] font-semibold text-muted-foreground">{formatContainer(booking?.containerSize || trip?.containerSize)}</span>
          <span className="rounded-md bg-muted px-1.5 py-0.5 text-[10px] font-semibold text-muted-foreground">{formatTripType(booking?.tripType || trip?.tripType)}</span>
        </div>
      </div>

      <div className="grid min-w-0 grid-cols-[1fr_auto_1fr] items-center gap-3 border-l border-border pl-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-foreground">{pickup}</p>
          <p className={`mt-1 text-xs ${isPastDue(pickupTime) ? "font-medium text-destructive" : isDueSoon(pickupTime) ? "font-medium text-amber-700 dark:text-amber-300" : "text-muted-foreground"}`}>Pickup · {formatDateTime(pickupTime)}</p>
        </div>
        <ArrowRight className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-foreground">{dropoff}</p>
          <p className="mt-1 text-xs text-muted-foreground">Drop-off · {formatDateTime(dropoffTime)}</p>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-x-4 gap-y-2 border-l border-border pl-3">
        <Requirement label="Driver" ready={Boolean(trip?.driverUserId)} />
        <Requirement label="Truck" ready={Boolean(trip?.truckAssetId)} />
        <Requirement label="Trailer" ready={Boolean(trip?.trailerAssetId)} optional />
        <Requirement label="ATW" ready={booking ? booking.hasAtw : trip?.atwState === "VERIFIED"} />
      </div>

      <div className={`rounded-lg border p-3 ${priorityStyles.action}`}>
        <p className={`text-xs font-semibold ${priorityStyles.text}`}>{nextAction.message}</p>
        <Button
          className={`planning-action mt-2 w-full ${isReady ? "bg-[#065F46] text-white hover:bg-[#064E3B]" : ""}`}
          size="sm"
          onClick={handleAction}
          disabled={busy && isBooking}
        >
          {busy && isBooking ? "Starting…" : nextAction.label}
          <ChevronRight className="h-4 w-4" />
        </Button>
      </div>

      {trip ? <button type="button" className="planning-icon-action grid h-10 w-10 place-items-center rounded-lg text-muted-foreground hover:bg-muted hover:text-foreground" onClick={() => onView(trip.tripId)} aria-label={`Open ${identifier} details`} title={`Open ${identifier} details`}><MoreHorizontal className="h-4 w-4" /></button> : <span aria-hidden="true" />}
    </article>
  );
}

function ResourceSelect({
  id,
  label,
  value,
  options,
  optional,
  isManager,
  onChange
}: {
  id: string;
  label: string;
  value: string;
  options: PlanningResource[];
  optional?: boolean;
  isManager: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}{optional ? " (optional)" : ""}</Label>
      <select id={id} value={value} onChange={(event) => onChange(event.target.value)} className="h-10 w-full rounded-lg border border-input bg-card px-3 text-sm">
        <option value="">{optional ? `No ${label.toLowerCase()}` : `Select ${label.toLowerCase()}`}</option>
        {options.map((option) => (
          <option key={option.id} value={option.id} disabled={!option.isAvailable && option.id !== value && !isManager}>
            {option.label}{option.capability ? ` · ${option.capability}` : ""}{!option.isAvailable ? ` · busy${option.conflictTripReference ? ` on ${option.conflictTripReference}` : ""}` : ""}
          </option>
        ))}
      </select>
    </div>
  );
}

function PlanningDrawer({
  tripId,
  onClose,
  onSaved,
  showToast
}: {
  tripId: string | null;
  onClose: () => void;
  onSaved: () => Promise<void>;
  showToast: (message: string, tone?: "success" | "error") => void;
}) {
  const navigate = useNavigate();
  const [trip, setTrip] = useState<DispatchTripDetail | null>(null);
  const [form, setForm] = useState<PlanForm | null>(null);
  const [resources, setResources] = useState<ResourceSnapshot>(emptyResources);
  const [decisionSupport, setDecisionSupport] = useState<PlanningDecisionSupport>(emptyDecisionSupport);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [validating, setValidating] = useState(false);
  const closeRef = useRef<HTMLButtonElement | null>(null);

  const loadTrip = async () => {
    if (!tripId) return;
    setLoading(true);
    try {
      const detail = await api<DispatchTripDetail>(`/api/dispatch/trips/${tripId}`, { method: "GET" });
      const pickup = detail.stops.find((stop) => stop.stopType === "PICKUP");
      const dropoff = detail.stops.find((stop) => stop.stopType === "DROPOFF");
      setTrip(detail);
      setForm({
        driverUserId: detail.driverUserId ?? "",
        truckAssetId: detail.truckAssetId ?? "",
        trailerAssetId: detail.trailerAssetId ?? "",
        containerNumber: detail.containerNumber ?? "",
        pickupLocation: pickup?.locationText ?? "",
        pickupScheduledAt: toLocalInput(pickup?.scheduledAt),
        dropoffLocation: dropoff?.locationText ?? "",
        dropoffScheduledAt: toLocalInput(dropoff?.scheduledAt),
        notes: detail.notes ?? "",
        remarks: ""
      });
    } catch (error: any) {
      showToast(error?.message ?? "Unable to load the trip plan.", "error");
    } finally {
      setLoading(false);
    }
  };

  const loadDecisionSupport = async (validate = false) => {
    if (!tripId) return;
    try {
      const endpoint = validate ? "validate" : "decision-support";
      setDecisionSupport(await api<PlanningDecisionSupport>(`/api/dispatch/planning/trips/${tripId}/${endpoint}`, { method: validate ? "POST" : "GET" }));
    } catch (error: any) {
      showToast(error?.message ?? "Unable to evaluate planning feasibility.", "error");
    }
  };

  useDispatchHub({
    onPlanningInvalidated: (event) => {
      if (!event.tripId || event.tripId === tripId) {
        void loadTrip();
        void loadDecisionSupport();
      }
    }
  });

  useEffect(() => { void loadTrip(); void loadDecisionSupport(); }, [tripId]);

  useEffect(() => {
    if (!tripId) return;
    closeRef.current?.focus();
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === "Escape") onClose(); };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [tripId, onClose]);

  useEffect(() => {
    if (!tripId || !form) return;
    const timer = window.setTimeout(async () => {
      const params = new URLSearchParams({ excludeTripId: tripId });
      const pickupAt = toApiTime(form.pickupScheduledAt);
      const dropoffAt = toApiTime(form.dropoffScheduledAt);
      if (pickupAt && dropoffAt && new Date(pickupAt) < new Date(dropoffAt)) {
        params.set("pickupAt", pickupAt);
        params.set("dropoffAt", dropoffAt);
      }
      try {
        setResources(await api<ResourceSnapshot>(`/api/dispatch/planning/resources?${params.toString()}`, { method: "GET" }));
      } catch {
        setResources(emptyResources);
      }
    }, 250);
    return () => window.clearTimeout(timer);
  }, [tripId, form?.pickupScheduledAt, form?.dropoffScheduledAt]);

  if (!tripId) return null;

  const selectedResource = (options: PlanningResource[], id: string) => options.find((option) => option.id === id);
  const selectedConflicts = form ? [selectedResource(resources.drivers, form.driverUserId), selectedResource(resources.trucks, form.truckAssetId), selectedResource(resources.trailers, form.trailerAssetId)].filter((resource): resource is PlanningResource => Boolean(resource && !resource.isAvailable)) : [];
  const atw = trip?.documents.find((document) => document.type === "ATW");
  const selectedRecommendation = form && decisionSupport?.recommendations.find((recommendation) =>
    recommendation.driverUserId === form.driverUserId &&
    recommendation.truckAssetId === form.truckAssetId &&
    (recommendation.trailerAssetId ?? "") === form.trailerAssetId);

  const save = async () => {
    if (!trip || !form) return;
    if (!form.pickupLocation.trim() || !form.dropoffLocation.trim()) return showToast("Pickup and dropoff locations are required.", "error");
    const pickupAt = toApiTime(form.pickupScheduledAt);
    const dropoffAt = toApiTime(form.dropoffScheduledAt);
    if (!pickupAt || !dropoffAt || new Date(pickupAt) >= new Date(dropoffAt)) return showToast("Dropoff time must be later than pickup time.", "error");
    if (selectedConflicts.length > 0) return showToast("Choose a non-overlapping resource combination before saving.", "error");

    setSaving(true);
    try {
      const pickup = trip.stops.find((stop) => stop.stopType === "PICKUP");
      const dropoff = trip.stops.find((stop) => stop.stopType === "DROPOFF");
      await api(`/api/dispatch/trips/${trip.id}`, {
        method: "PUT",
        body: JSON.stringify({
          customerId: trip.customer.id,
          driverUserId: form.driverUserId || null,
          truckAssetId: form.truckAssetId || null,
          trailerAssetId: form.trailerAssetId || null,
          notes: form.notes.trim() || null,
          remarks: form.remarks.trim() || null,
          rowVersion: trip.rowVersion,
          containerNumber: form.containerNumber.trim() || null,
          eirNumber: trip.eirNumber ?? null,
          bookingNumber: trip.bookingNumber ?? null,
          shippingLine: trip.shippingLine ?? null,
          containerSize: trip.containerSize ?? null,
          tripType: trip.tripType ?? null,
          stops: [
            { stopType: "PICKUP" as TripStopType, locationText: form.pickupLocation.trim(), scheduledAt: pickupAt, latitude: pickup?.latitude ?? null, longitude: pickup?.longitude ?? null },
            { stopType: "DROPOFF" as TripStopType, locationText: form.dropoffLocation.trim(), scheduledAt: dropoffAt, latitude: dropoff?.latitude ?? null, longitude: dropoff?.longitude ?? null }
          ]
        })
      });
      showToast("Plan saved and readiness recalculated.", "success");
      await onSaved();
      await loadTrip();
      await loadDecisionSupport();
    } catch (error: any) {
      showToast(error?.message ?? "The plan could not be saved.", "error");
    } finally {
      setSaving(false);
    }
  };

  const validateForDispatch = async () => {
    setValidating(true);
    try {
      await loadDecisionSupport(true);
      showToast("Dispatch validation refreshed. Review blockers and warnings below.", "success");
    } finally {
      setValidating(false);
    }
  };

  const markReady = async () => {
    if (!trip || !form || !decisionSupport) return;
    if (!decisionSupport.canMarkReady) return showToast("Resolve all dispatch blockers before marking the trip ready.", "error");
    if (!selectedRecommendation && !form.remarks.trim()) return showToast("Add an override reason for a valid manual assignment outside the current suggestions.", "error");
    setSaving(true);
    try {
      await api(`/api/dispatch/planning/trips/${trip.id}/ready`, {
        method: "POST",
        body: JSON.stringify({
          rowVersion: trip.rowVersion,
          recommendationToken: decisionSupport.recommendationToken,
          selectedRank: selectedRecommendation?.selectionRank ?? null,
          overrideReason: selectedRecommendation ? null : form.remarks.trim()
        })
      });
      showToast("Trip marked Ready for Dispatch.", "success");
      await onSaved();
      onClose();
    } catch (error: any) {
      showToast(error?.message ?? "The final readiness validation failed. Refresh and try again.", "error");
      await loadDecisionSupport();
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50" role="presentation">
      <button className="absolute inset-0 bg-foreground/25" aria-label="Close planning drawer" onClick={onClose} />
      <aside role="dialog" aria-modal="true" aria-labelledby="planning-drawer-title" className="absolute inset-y-0 right-0 flex w-full max-w-3xl flex-col border-l border-border bg-background shadow-2xl">
        <div className="flex items-start justify-between gap-4 border-b border-border px-5 py-4 sm:px-6">
          <div><p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">Trip planning</p><h2 id="planning-drawer-title" className="mt-1 text-xl font-semibold">{trip?.bookingNumber || `Trip ${tripId.slice(0, 8).toUpperCase()}`}</h2><p className="mt-1 text-sm text-muted-foreground">{trip?.customer.name || "Loading customer…"}</p></div>
          <Button ref={closeRef} variant="ghost" size="icon" onClick={onClose} aria-label="Close"><X className="h-5 w-5" /></Button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5 sm:px-6">
          {loading || !trip || !form ? <LoadingSkeleton rows={8} /> : (
            <div className="space-y-7">
              <section aria-labelledby="schedule-heading">
                <div className="flex items-center justify-between gap-3"><h3 id="schedule-heading" className="text-sm font-semibold">Schedule</h3><span className="text-xs text-muted-foreground">Times drive resource availability</span></div>
                <div className="mt-3 grid gap-4 sm:grid-cols-2">
                  <div className="space-y-2"><Label htmlFor="pickup-location">Pickup location</Label><Input id="pickup-location" value={form.pickupLocation} onChange={(event) => setForm({ ...form, pickupLocation: event.target.value })} /></div>
                  <div className="space-y-2"><Label htmlFor="pickup-time">Pickup time</Label><Input id="pickup-time" type="datetime-local" value={form.pickupScheduledAt} onChange={(event) => setForm({ ...form, pickupScheduledAt: event.target.value })} /></div>
                  <div className="space-y-2"><Label htmlFor="dropoff-location">Dropoff location</Label><Input id="dropoff-location" value={form.dropoffLocation} onChange={(event) => setForm({ ...form, dropoffLocation: event.target.value })} /></div>
                  <div className="space-y-2"><Label htmlFor="dropoff-time">Dropoff time</Label><Input id="dropoff-time" type="datetime-local" value={form.dropoffScheduledAt} onChange={(event) => setForm({ ...form, dropoffScheduledAt: event.target.value })} /></div>
                </div>
              </section>

              <section aria-labelledby="booking-validation-heading">
                <div className="flex items-center justify-between gap-3"><h3 id="booking-validation-heading" className="text-sm font-semibold">Booking checks</h3><span className="text-xs text-muted-foreground">Required before dispatch</span></div>
                <div className="mt-3 grid gap-2 sm:grid-cols-2">
                  {(decisionSupport?.bookingChecks ?? []).map((check) => <div key={check.message} className={`flex items-start gap-2 border-b py-2 text-sm ${check.state === "Blocked" ? "border-destructive/30" : "border-border"}`}>{check.state === "Passed" ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-success" /> : check.state === "Warning" ? <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0 text-warning-foreground" /> : <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-destructive" />}<p className="font-medium">{check.message}</p></div>)}
                </div>
              </section>

              <section aria-labelledby="assignment-check-heading">
                <div className="flex items-center justify-between gap-3"><div><h3 id="assignment-check-heading" className="text-sm font-semibold">Assignment check</h3><p className="mt-1 text-xs text-muted-foreground">{decisionSupport && !decisionSupport.resourcesEvaluated ? "Complete the booking checks before reviewing available resources." : "Availability, schedule, and equipment requirements are checked for this plan."}</p></div><span className="rounded-full border border-primary/20 bg-primary/10 px-2 py-1 text-xs font-semibold text-primary">{decisionSupport && !decisionSupport.resourcesEvaluated ? "Not ready" : decisionSupport?.availableAssignmentCount ? `${decisionSupport.availableAssignmentCount} options` : "Needs attention"}</span></div>
              </section>

              <section aria-labelledby="recommendations-heading">
                {decisionSupport && false ? <>
                <div className="flex items-center justify-between gap-3"><div><h3 id="recommendations-heading" className="text-sm font-semibold">TOPSIS ranked assignments</h3><p className="mt-1 text-xs text-muted-foreground">{decisionSupport ? `Generated ${formatDateTime(decisionSupport.generatedAt)} · expires ${formatDateTime(decisionSupport.expiresAt)} · ${decisionSupport.criteriaWeightVersion}` : "Loading ranked combinations…"}</p></div><Medal className="h-5 w-5 text-warning-foreground" /></div>
                <div className="mt-3 space-y-3">
                  {decisionSupport?.recommendations.length ? decisionSupport.recommendations.map((recommendation) => <article key={`${recommendation.driverUserId}-${recommendation.truckAssetId}-${recommendation.trailerAssetId ?? "none"}`} className={`rounded-xl border p-3 ${selectedRecommendation?.rank === recommendation.rank ? "border-primary bg-primary/5" : "border-border bg-muted/20"}`}><div className="flex items-start justify-between gap-3"><div><p className="text-sm font-semibold">Rank {recommendation.rank} · {recommendation.driverName} + {recommendation.truckCode}{recommendation.trailerCode ? ` + ${recommendation.trailerCode}` : ""}</p><p className="mt-1 text-xs text-muted-foreground">Score {Math.round(recommendation.score * 100)}% · {recommendation.reasons[0]}</p></div><Button type="button" variant="outline" size="sm" onClick={() => form && setForm({ ...form, driverUserId: recommendation.driverUserId, truckAssetId: recommendation.truckAssetId, trailerAssetId: recommendation.trailerAssetId ?? "" })}>Use rank {recommendation.rank}</Button></div><div className="mt-3 grid gap-1 text-xs text-muted-foreground sm:grid-cols-2">{recommendation.criteriaContributions.map((item) => <p key={item.criterion}>{item.criterion} ({Math.round(item.weight * 100)}%): {item.explanation}</p>)}</div>{recommendation.warnings.length ? <p className="mt-2 text-xs text-warning-foreground">Warning: {recommendation.warnings.join(" · ")}</p> : null}</article>) : <p className="rounded-xl border border-border bg-muted/20 p-3 text-sm text-muted-foreground">{decisionSupport && !decisionSupport.resourcesEvaluated ? "No TOPSIS ranking was generated: resources were not evaluated." : "Resources were evaluated, but no valid combination remains to rank."}</p>}
                </div>
                </> : <>
                  <div><h3 id="recommendations-heading" className="text-sm font-semibold">Recommended assignment</h3><p className="mt-1 text-xs text-muted-foreground">Choose the suggested resource set or review a viable alternative.</p></div>
                  <div className="mt-3 space-y-3">
                    {decisionSupport?.recommendations.length ? decisionSupport.recommendations.map((recommendation) => <article key={`${recommendation.driverUserId}-${recommendation.truckAssetId}-${recommendation.trailerAssetId ?? "none"}`} className={`rounded-xl border p-3 ${selectedRecommendation?.selectionRank === recommendation.selectionRank ? "border-primary bg-primary/5" : recommendation.isRecommended ? "border-primary/35 bg-primary/5" : "border-border bg-muted/20"}`}><div className="flex items-start justify-between gap-3"><div><p className="text-[10px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">{recommendation.isRecommended ? "Recommended" : "Alternative"}</p><p className="mt-1 text-sm font-semibold">{recommendation.driverName} + {recommendation.truckCode}{recommendation.trailerCode ? ` + ${recommendation.trailerCode}` : ""}</p><p className="mt-1 text-xs text-muted-foreground">{recommendation.reasons.join("; ")}</p></div><Button type="button" variant={recommendation.isRecommended ? "default" : "outline"} size="sm" onClick={() => form && setForm({ ...form, driverUserId: recommendation.driverUserId, truckAssetId: recommendation.truckAssetId, trailerAssetId: recommendation.trailerAssetId ?? "" })}>{recommendation.isRecommended ? "Use recommended" : "Use alternative"}</Button></div>{recommendation.warnings.length ? <p className="mt-2 text-xs text-warning-foreground">Warning: {recommendation.warnings.join("; ")}</p> : null}</article>) : <p className="rounded-xl border border-border bg-muted/20 p-3 text-sm text-muted-foreground">{decisionSupport && !decisionSupport.resourcesEvaluated ? "Complete the booking checks before resource suggestions can be made." : "No valid resource combination is available for this schedule. Review the assignment issues below."}</p>}
                  </div>
                </>}
              </section>

              <section aria-labelledby="excluded-heading">
                <div className="flex items-center justify-between gap-3"><h3 id="excluded-heading" className="text-sm font-semibold">Assignment issues</h3><span className="text-xs text-muted-foreground">Unavailable resources</span></div>
                {decisionSupport && false ? <>
                <div className="mt-3 space-y-2">{decisionSupport?.excludedResources.length ? decisionSupport.excludedResources.map((resource) => <div key={`${resource.resourceType}-${resource.resourceId}`} className="border-b border-border pb-2 text-sm"><p className="font-medium">{resource.resourceType} · {resource.resourceLabel}</p><p className="mt-1 text-xs text-muted-foreground">{resource.checks.filter((check) => check.state === "Blocked").map((check) => check.message).join(" · ")}</p></div>) : <p className="text-sm text-muted-foreground">{decisionSupport && !decisionSupport.resourcesEvaluated ? "Not applicable: resources were not evaluated." : "No resources are excluded for the saved schedule."}</p>}</div>
                </> : <div className="mt-3 space-y-2">{decisionSupport?.excludedResources.length ? decisionSupport.excludedResources.map((resource) => <div key={`${resource.resourceType}-${resource.resourceLabel}`} className="border-b border-border pb-2 text-sm"><p className="font-medium">{resource.resourceType}: {resource.resourceLabel}</p><p className="mt-1 text-xs text-muted-foreground">{resource.reasons.join("; ")}</p></div>) : <p className="text-sm text-muted-foreground">{decisionSupport && !decisionSupport.resourcesEvaluated ? "Complete the booking checks first." : "No assignment issues were found for the saved schedule."}</p>}</div>}
              </section>

              <section aria-labelledby="resources-heading">
                <div className="flex items-center justify-between gap-3"><h3 id="resources-heading" className="text-sm font-semibold">Resource assignment</h3><span className="text-xs text-muted-foreground">Availability shown for this schedule</span></div>
                <div className="mt-3 grid gap-4 sm:grid-cols-3">
                  <ResourceSelect id="driver" label="Driver" value={form.driverUserId} options={resources.drivers} isManager={false} onChange={(value) => setForm({ ...form, driverUserId: value })} />
                  <ResourceSelect id="truck" label="Truck" value={form.truckAssetId} options={resources.trucks} isManager={false} onChange={(value) => setForm({ ...form, truckAssetId: value })} />
                  <ResourceSelect id="trailer" label="Trailer" value={form.trailerAssetId} options={resources.trailers} optional isManager={false} onChange={(value) => setForm({ ...form, trailerAssetId: value })} />
                </div>
                {selectedConflicts.length > 0 ? (
                  <div className="mt-4 rounded-xl border border-destructive/30 bg-destructive/10 p-4">
                    <p className="flex items-center gap-2 text-sm font-semibold text-destructive"><AlertTriangle className="h-4 w-4" /> Assignment conflict</p>
                    <ul className="mt-2 space-y-1 text-sm text-foreground">{selectedConflicts.map((resource) => <li key={resource.id}>{resource.label} is busy on Trip {resource.conflictTripReference} until {formatDateTime(resource.busyUntil)}.</li>)}</ul>
                    <p className="mt-2 text-xs text-muted-foreground">Choose another resource. This schedule has an overlapping resource assignment.</p>
                  </div>
                ) : null}
              </section>

              <section aria-labelledby="details-heading">
                <h3 id="details-heading" className="text-sm font-semibold">Operational details</h3>
                <div className="mt-3 grid gap-4 sm:grid-cols-2">
                  <div className="space-y-2"><Label htmlFor="container-number">Container number</Label><Input id="container-number" value={form.containerNumber} onChange={(event) => setForm({ ...form, containerNumber: event.target.value })} placeholder="Required before dispatch" /></div>
                  <div className="rounded-xl border border-border bg-muted/30 p-3 text-sm"><p className="text-xs text-muted-foreground">Equipment requirement</p><p className="mt-1 font-semibold">{formatContainer(trip.containerSize)} · {formatTripType(trip.tripType)}</p></div>
                  <div className="space-y-2 sm:col-span-2"><Label htmlFor="planning-notes">Planning notes</Label><Textarea id="planning-notes" value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} placeholder="Port cutoffs, handling notes, or dispatch instructions" /></div>
                  <div className="space-y-2 sm:col-span-2"><Label htmlFor="planning-override-reason">Manual assignment reason</Label><Textarea id="planning-override-reason" value={form.remarks} onChange={(event) => setForm({ ...form, remarks: event.target.value })} placeholder="Required only when choosing a valid assignment outside the current suggestions." /></div>
                </div>
              </section>

              <section aria-labelledby="readiness-heading">
                <div className="flex items-center justify-between gap-3"><div><h3 id="readiness-heading" className="text-sm font-semibold">Final Dispatch Validation</h3><p className="mt-1 text-xs text-muted-foreground">Validation does not change Trip state. Mark Ready reruns every constraint atomically.</p></div><ClipboardCheck className="h-5 w-5 text-primary" /></div>
                <div className="mt-3 rounded-xl border border-border bg-muted/20 p-3 text-sm"><p className="font-medium">{decisionSupport?.canMarkReady ? "All required checks currently pass." : "Resolve blocked checks and select a valid assignment before handoff."}</p>{selectedRecommendation ? <p className="mt-1 text-xs text-muted-foreground">{selectedRecommendation.isRecommended ? "Recommended assignment selected." : "Alternative assignment selected."}</p> : form.driverUserId && form.truckAssetId ? <p className="mt-1 text-xs text-warning-foreground">Manual selection requires a reason if it is outside the current suggestions.</p> : null}</div>
                {atw?.state !== "VERIFIED" ? <Button variant="outline" size="sm" className="mt-4" onClick={() => navigate(`/dispatch/trips/${trip.id}`)}>Open trip document checklist <ArrowRight className="h-4 w-4" /></Button> : null}
              </section>
            </div>
          )}
        </div>

        <div className="flex flex-col-reverse gap-2 border-t border-border bg-background px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">
          <Button variant="ghost" onClick={() => navigate(`/dispatch/trips/${tripId}`)}>Open full trip</Button>
          <div className="flex flex-col-reverse gap-2 sm:flex-row"><Button variant="outline" onClick={onClose}>Cancel</Button><Button variant="outline" onClick={() => void validateForDispatch()} disabled={saving || loading || validating}>{validating ? "Validating…" : "Validate for dispatch"}</Button><Button onClick={() => void save()} disabled={saving || loading}>{saving ? "Saving…" : "Save Draft"}</Button><Button onClick={() => void markReady()} disabled={saving || loading || !decisionSupport?.canMarkReady}>{saving ? "Saving…" : "Mark Ready for Dispatch"}</Button></div>
        </div>
      </aside>
    </div>
  );
}

function RescheduleDialog({ booking, onClose, onConfirm, busy }: { booking: ApprovedBooking | null; onClose: () => void; onConfirm: (value: string) => void; busy: boolean }) {
  const [value, setValue] = useState(defaultFutureTime());
  useEffect(() => setValue(defaultFutureTime()), [booking]);
  if (!booking) return null;
  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-foreground/30 px-4" role="presentation">
      <div role="dialog" aria-modal="true" aria-labelledby="reschedule-title" className="w-full max-w-md rounded-2xl border border-border bg-background p-6 shadow-2xl">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-warning-foreground">Schedule confirmation</p><h2 id="reschedule-title" className="mt-1 text-lg font-semibold">Set a future pickup time</h2><p className="mt-2 text-sm text-muted-foreground">The customer's requested time has passed. Confirm a new planning time before creating the Draft trip.</p>
        <div className="mt-5 space-y-2"><Label htmlFor="rescheduled-pickup">Pickup time</Label><Input id="rescheduled-pickup" type="datetime-local" value={value} min={defaultFutureTime()} onChange={(event) => setValue(event.target.value)} /></div>
        <div className="mt-5 flex justify-end gap-2"><Button variant="outline" onClick={onClose}>Cancel</Button><Button onClick={() => onConfirm(value)} disabled={busy || !value}>Create trip plan</Button></div>
      </div>
    </div>
  );
}

export default function DispatchPlanningPage() {
  const navigate = useNavigate();
  const { toasts, show } = useToast();
  const [board, setBoard] = useState<PlanningBoard | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [day, setDay] = useState("");
  const dateInputRef = useRef<HTMLInputElement | null>(null);
  const [queueFilter, setQueueFilter] = useState<QueueFilter>("all");
  const [editorTripId, setEditorTripId] = useState<string | null>(null);
  const [rescheduleBooking, setRescheduleBooking] = useState<ApprovedBooking | null>(null);

  useEffect(() => {
    const timer = window.setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const loadBoard = async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams({ page: "1", pageSize: "50" });
      if (search) params.set("search", search);
      if (day) params.set("day", day);
      setBoard(await api<PlanningBoard>(`/api/dispatch/planning/board?${params.toString()}`, { method: "GET" }));
    } catch (error: any) {
      show(error?.message ?? "Unable to load the planning workspace.", "error");
    } finally {
      setLoading(false);
    }
  };

  useDispatchHub({ onPlanningInvalidated: () => { void loadBoard(); } });

  useEffect(() => { void loadBoard(); }, [search, day]);

  const queueItems = useMemo<PlanningQueueItem[]>(() => {
    const items: PlanningQueueItem[] = [
      ...(board?.approvedBookings ?? []).map((booking) => ({
        kind: "booking" as const,
        id: booking.requestId,
        booking,
        priority: priorityForBooking(booking),
        needsAttention: bookingNeedsAttention(booking),
        dueSoon: isDueSoon(booking.requestedPickupTime)
      })),
      ...(board?.draftTrips ?? []).map((trip) => ({
        kind: "trip" as const,
        id: trip.tripId,
        trip,
        priority: priorityForTrip(trip),
        needsAttention: tripNeedsAttention(trip),
        dueSoon: isDueSoon(trip.pickupScheduledAt)
      })),
      ...(board?.readyTrips ?? []).map((trip) => ({
        kind: "trip" as const,
        id: trip.tripId,
        trip,
        priority: priorityForTrip(trip),
        needsAttention: false,
        dueSoon: isDueSoon(trip.pickupScheduledAt)
      }))
    ];
    const rank = { high: 0, medium: 1, low: 2 } as const;
    return items.sort((left, right) => {
      const priorityDifference = rank[left.priority] - rank[right.priority];
      if (priorityDifference) return priorityDifference;
      const leftTime = new Date(left.kind === "booking" ? left.booking.requestedPickupTime ?? 0 : left.trip.pickupScheduledAt ?? 0).getTime();
      const rightTime = new Date(right.kind === "booking" ? right.booking.requestedPickupTime ?? 0 : right.trip.pickupScheduledAt ?? 0).getTime();
      return leftTime - rightTime;
    });
  }, [board]);
  const attentionCount = useMemo(() => queueItems.filter((item) => item.needsAttention).length, [queueItems]);
  const dueSoonCount = useMemo(() => queueItems.filter((item) => item.dueSoon).length, [queueItems]);
  const visibleQueue = useMemo(() => queueItems.filter((item) => {
    if (queueFilter === "attention") return item.needsAttention;
    if (queueFilter === "due") return item.dueSoon;
    if (queueFilter === "planning") return item.kind === "trip" && item.trip.status === "DRAFT";
    if (queueFilter === "ready") return item.kind === "trip" && item.trip.status === "READY_FOR_DISPATCH";
    return true;
  }), [queueFilter, queueItems]);
  const greeting = useMemo(() => {
    const hour = new Date().getHours();
    return hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
  }, []);

  const convert = async (booking: ApprovedBooking, override?: string) => {
    const requested = booking.requestedPickupTime ? new Date(booking.requestedPickupTime).getTime() : null;
    if (!override && requested !== null && requested <= Date.now()) {
      setRescheduleBooking(booking);
      return;
    }
    setBusy(true);
    try {
      const result = await api<{ tripId: string; created: boolean }>(`/api/dispatch/planning/bookings/${booking.requestId}/start`, { method: "POST", body: JSON.stringify({ scheduledPickupTime: override ? toApiTime(override) : null }) });
      setRescheduleBooking(null);
      show(result.created ? "Draft trip created. Complete its schedule and assignments." : "Existing Draft trip reopened for planning.", "success");
      await loadBoard();
      setEditorTripId(result.tripId);
    } catch (error: any) {
      show(error?.message ?? "The booking could not be moved into planning.", "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-5">
      <ToastHost toasts={toasts} />
      <header className="flex flex-col gap-4 border-b border-border pb-5 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex items-start gap-3">
          <div className="mt-0.5 grid h-10 w-10 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary"><CalendarClock className="h-5 w-5" /></div>
          <div>
            <p className="text-sm font-semibold text-foreground">{greeting}</p>
            <h1 className="mt-0.5 text-2xl font-bold tracking-tight text-foreground">Dispatch planning</h1>
            <p className="mt-1 text-sm text-muted-foreground">Review bookings, resolve blockers, and prepare the next trips for dispatch.</p>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className="flex h-10 items-center gap-2 rounded-lg border border-input bg-card px-3 text-sm font-medium text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            onClick={() => {
              const input = dateInputRef.current;
              if (!input) return;
              input.showPicker?.();
              input.focus();
            }}
          >
            <CalendarDays className="h-4 w-4 text-muted-foreground" />
            <span>{day ? new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric", year: "numeric" }).format(new Date(`${day}T12:00:00`)) : "All planning dates"}</span>
          </button>
          <input ref={dateInputRef} aria-label="Planning date" type="date" value={day} onChange={(event) => setDay(event.target.value)} className="sr-only" />
          {day ? <Button className="planning-action" variant="outline" size="sm" onClick={() => setDay("")}>Show all</Button> : null}
          <Button className="planning-action" variant="outline" size="sm" onClick={() => void loadBoard()} disabled={loading}><RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} /> Refresh</Button>
        </div>
      </header>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5" aria-label="Planning summary">
        {[
          { label: "Total bookings", value: (board?.approvedTotalCount ?? 0) + (board?.draftTotalCount ?? 0) + (board?.readyCount ?? 0), hint: "Across the planning queue", icon: CalendarClock, filter: "all" as const, tone: "text-primary bg-primary/10" },
          { label: "Need attention", value: attentionCount, hint: "Blocking setup or ATW", icon: AlertTriangle, filter: "attention" as const, tone: "text-amber-700 bg-amber-100 dark:text-amber-300 dark:bg-amber-950/60" },
          { label: "Due within 2 hrs", value: dueSoonCount, hint: "Upcoming pickups", icon: Clock3, filter: "due" as const, tone: "text-amber-700 bg-amber-100 dark:text-amber-300 dark:bg-amber-950/60" },
          { label: "In planning", value: board?.draftTotalCount ?? 0, hint: "Schedules and assignments", icon: CircleDashed, filter: "planning" as const, tone: "text-indigo-700 bg-indigo-100 dark:text-indigo-300 dark:bg-indigo-950/60" },
          { label: "Ready to dispatch", value: board?.readyCount ?? 0, hint: "All gates passed", icon: CheckCircle2, filter: "ready" as const, tone: "text-emerald-700 bg-emerald-100 dark:text-emerald-300 dark:bg-emerald-950/60" }
        ].map((item) => <button key={item.label} type="button" onClick={() => setQueueFilter(item.filter)} className={`operations-kpi text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${queueFilter === item.filter ? "border-primary" : "border-border"}`}>
          <span className={`operations-kpi__icon ${item.tone.replace(" bg-primary/10", "").replace(" bg-amber-100 dark:text-amber-300 dark:bg-amber-950/60", "").replace(" bg-indigo-100 dark:text-indigo-300 dark:bg-indigo-950/60", "").replace(" bg-emerald-100 dark:text-emerald-300 dark:bg-emerald-950/60", "")}`}><item.icon className="h-5 w-5" /></span><div><p className="operations-kpi__value tabular-nums">{item.value}</p><p className="operations-kpi__label">{item.label}</p><p className="mt-1 text-xs text-muted-foreground">{item.hint}</p></div>
        </button>)}
      </section>

      {loading && !board ? <LoadingSkeleton rows={8} /> : !board || (board.approvedBookings.length === 0 && board.draftTrips.length === 0 && board.readyTrips.length === 0) ? <div className="surface-card p-8"><EmptyState title="Planning workspace is clear" description="Approved bookings and Draft trip plans will appear here." /></div> : (
        <section aria-labelledby="dispatch-queue-heading">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <div className="flex flex-wrap items-center gap-2"><h2 id="dispatch-queue-heading" className="text-xl font-bold text-foreground">Dispatch queue</h2>{attentionCount > 0 ? <span className="rounded-md bg-amber-100 px-2 py-1 text-xs font-semibold text-amber-800 dark:bg-amber-950/60 dark:text-amber-300">{attentionCount} need attention</span> : null}</div>
              <p className="mt-1 text-sm text-muted-foreground">Prioritized by dispatch blockers, pickup urgency, and readiness.</p>
            </div>
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
              <div className="relative min-w-0 sm:w-72"><Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" /><Input id="planning-search" aria-label="Find a booking or trip" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder="Booking, customer, or route" className="h-10 rounded-lg bg-card pl-9" /></div>
              <span className="inline-flex h-10 items-center gap-2 rounded-lg border border-border bg-card px-3 text-sm font-medium text-muted-foreground"><Filter className="h-4 w-4" />Priority order</span>
            </div>
          </div>

          <div className="mt-4 flex gap-2 overflow-x-auto pb-1" aria-label="Queue filters">
            {[
              { value: "all" as const, label: "All", count: queueItems.length },
              { value: "attention" as const, label: "Attention", count: attentionCount },
              { value: "due" as const, label: "Due soon", count: dueSoonCount },
              { value: "planning" as const, label: "In planning", count: board.draftTotalCount },
              { value: "ready" as const, label: "Ready", count: board.readyCount }
            ].map((tab) => <button key={tab.value} type="button" aria-pressed={queueFilter === tab.value} onClick={() => setQueueFilter(tab.value)} className={`shrink-0 rounded-lg border px-3 py-2 text-sm font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${queueFilter === tab.value ? "border-primary bg-primary/10 text-primary" : "border-border bg-card text-muted-foreground hover:bg-muted hover:text-foreground"}`}>{tab.label} <span className="ml-1 tabular-nums">({tab.count})</span></button>)}
          </div>

          <div className="mt-3 overflow-x-auto pb-2">
            <div className="min-w-[1080px] space-y-3">
              {visibleQueue.length === 0 ? <div className="rounded-xl border border-dashed border-border bg-card px-5 py-10"><EmptyState title="No plans match this filter" description="Try another queue filter or clear the search to see more planning work." /></div> : visibleQueue.map((item) => <PlanningQueueRow key={item.id} item={item} busy={busy} onStart={(booking) => void convert(booking)} onOpen={setEditorTripId} onView={(tripId) => navigate(`/dispatch/trips/${tripId}`)} />)}
            </div>
          </div>
        </section>
      )}

      <div className="flex items-center justify-between rounded-xl border border-border bg-muted/20 px-4 py-3 text-sm"><p className="text-muted-foreground"><Truck className="mr-2 inline h-4 w-4" />Ready for Dispatch is an explicit, reserved pre-execution state on the existing Draft trip.</p><Link to="/dispatch/board" className="hidden items-center gap-2 font-semibold text-primary sm:inline-flex">Go to execution <ArrowRight className="h-4 w-4" /></Link></div>

      <PlanningDrawer tripId={editorTripId} onClose={() => setEditorTripId(null)} onSaved={loadBoard} showToast={show} />
      <RescheduleDialog booking={rescheduleBooking} onClose={() => setRescheduleBooking(null)} onConfirm={(value) => { if (rescheduleBooking) void convert(rescheduleBooking, value); }} busy={busy} />
    </div>
  );
}
