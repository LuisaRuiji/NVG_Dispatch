import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  AlertTriangle,
  ArrowRight,
  CalendarClock,
  CheckCircle2,
  ChevronRight,
  CircleDashed,
  Clock3,
  ClipboardCheck,
  FileCheck2,
  MapPin,
  Medal,
  RefreshCw,
  Search,
  ShieldAlert,
  Truck,
  X
} from "lucide-react";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import PageHeader from "@/components/PageHeader";
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
type PlanningExcludedResource = { resourceType: string; resourceId: string; resourceLabel: string; checks: PlanningCheck[] };
type PlanningContribution = { criterion: string; direction: string; weight: number; rawValue: number; weightedValue: number; explanation: string };
type PlanningRecommendation = {
  rank: number;
  driverUserId: string;
  driverName: string;
  truckAssetId: string;
  truckCode: string;
  trailerAssetId?: string | null;
  trailerCode?: string | null;
  score: number;
  criteriaContributions: PlanningContribution[];
  reasons: string[];
  warnings: string[];
};
type PlanningDecisionSupport = {
  tripId: string;
  canMarkReady: boolean;
  resourcesEvaluated: boolean;
  bookingChecks: PlanningCheck[];
  feasibleCombinationCount: number;
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

function BookingCard({ booking, onStart, busy }: { booking: ApprovedBooking; onStart: (booking: ApprovedBooking) => void; busy: boolean }) {
  return (
    <article className="rounded-xl border border-border bg-background p-4 shadow-sm">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold">{booking.bookingNumber || `Request ${booking.requestId.slice(0, 8).toUpperCase()}`}</p>
          <p className="mt-1 truncate text-xs text-muted-foreground">{booking.customerName}</p>
        </div>
        <span className="rounded-full border border-primary/25 bg-primary/10 px-2 py-1 text-[10px] font-semibold text-primary">APPROVED</span>
      </div>
      <div className="mt-4 space-y-2 text-sm">
        <p className="truncate font-medium"><MapPin className="mr-1 inline h-3.5 w-3.5 text-muted-foreground" />{booking.pickupLocation}</p>
        <p className="truncate pl-[18px] text-muted-foreground">to {booking.dropoffLocation}</p>
        <p className="flex items-center gap-2 text-xs text-muted-foreground"><Clock3 className="h-3.5 w-3.5" />{formatDateTime(booking.requestedPickupTime)}</p>
      </div>
      <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-border pt-3 text-xs text-muted-foreground">
        <span>{formatContainer(booking.containerSize)}</span><span>·</span><span>{formatTripType(booking.tripType)}</span>
        <span className={`ml-auto inline-flex items-center gap-1 font-semibold ${booking.hasAtw ? "text-success" : "text-warning-foreground"}`}>
          {booking.hasAtw ? <FileCheck2 className="h-3.5 w-3.5" /> : <ShieldAlert className="h-3.5 w-3.5" />} {booking.hasAtw ? "ATW attached" : "ATW missing"}
        </span>
      </div>
      <Button className="mt-4 w-full" size="sm" onClick={() => onStart(booking)} disabled={busy}>Start planning <ChevronRight className="h-4 w-4" /></Button>
    </article>
  );
}

function TripPlanCard({ trip, onOpen }: { trip: PlanningTrip; onOpen: (tripId: string) => void }) {
  const isReady = trip.status === "READY_FOR_DISPATCH";
  return (
    <article className={`rounded-xl border bg-background p-4 shadow-sm ${trip.conflicts.length > 0 ? "border-destructive/35" : isReady ? "border-success/35" : "border-border"}`}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold">{trip.bookingNumber || `Trip ${trip.tripId.slice(0, 8).toUpperCase()}`}</p>
          <p className="mt-1 truncate text-xs text-muted-foreground">{trip.customerName}</p>
        </div>
        {trip.conflicts.length > 0 ? <span className="inline-flex items-center gap-1 rounded-full border border-destructive/30 bg-destructive/10 px-2 py-1 text-[10px] font-semibold text-destructive"><AlertTriangle className="h-3 w-3" /> CONFLICT</span> : isReady ? <span className="inline-flex items-center gap-1 rounded-full border border-success/30 bg-success/10 px-2 py-1 text-[10px] font-semibold text-success"><CheckCircle2 className="h-3 w-3" /> READY</span> : <span className="rounded-full border border-border bg-muted px-2 py-1 text-[10px] font-semibold text-muted-foreground">PLANNING</span>}
      </div>
      <div className="mt-4 space-y-2 text-sm">
        <p className="truncate font-medium"><MapPin className="mr-1 inline h-3.5 w-3.5 text-muted-foreground" />{trip.pickupLocation || "Pickup pending"}</p>
        <p className="truncate pl-[18px] text-muted-foreground">to {trip.dropoffLocation || "Dropoff pending"}</p>
        <p className="flex items-center gap-2 text-xs text-muted-foreground"><Clock3 className="h-3.5 w-3.5" />{formatDateTime(trip.pickupScheduledAt)}–{trip.dropoffScheduledAt ? formatDateTime(trip.dropoffScheduledAt) : "end pending"}</p>
      </div>
      <dl className="mt-4 grid grid-cols-3 gap-2 border-y border-border py-3 text-xs">
        <div className="min-w-0"><dt className="text-muted-foreground">Driver</dt><dd className="mt-1 truncate font-semibold">{trip.driverUsername || "Unassigned"}</dd></div>
        <div className="min-w-0"><dt className="text-muted-foreground">Truck</dt><dd className="mt-1 truncate font-semibold">{trip.truckAssetCode || "Unassigned"}</dd></div>
        <div className="min-w-0"><dt className="text-muted-foreground">Trailer</dt><dd className="mt-1 truncate font-semibold">{trip.trailerAssetCode || "Optional"}</dd></div>
      </dl>
      {!isReady && trip.missingRequirements.length > 0 ? <p className="mt-3 line-clamp-2 text-xs text-muted-foreground">Next: {trip.missingRequirements.slice(0, 2).join(" · ")}</p> : null}
      <Button className="mt-4 w-full" variant={isReady ? "default" : "outline"} size="sm" onClick={() => onOpen(trip.tripId)}>{isReady ? "Review ready plan" : "Continue planning"} <ChevronRight className="h-4 w-4" /></Button>
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
      <select id={id} value={value} onChange={(event) => onChange(event.target.value)} className="h-11 w-full rounded-xl border border-input bg-background px-3 text-sm">
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
  const [decisionSupport, setDecisionSupport] = useState<PlanningDecisionSupport | null>(null);
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
    if (!selectedRecommendation && !form.remarks.trim()) return showToast("Add an override reason for a valid manual assignment outside the ranked list.", "error");
    setSaving(true);
    try {
      await api(`/api/dispatch/planning/trips/${trip.id}/ready`, {
        method: "POST",
        body: JSON.stringify({
          rowVersion: trip.rowVersion,
          recommendationToken: decisionSupport.recommendationToken,
          selectedRank: selectedRecommendation?.rank ?? null,
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
                <div className="flex items-center justify-between gap-3"><h3 id="booking-validation-heading" className="text-sm font-semibold">Booking pre-validation</h3><span className="text-xs text-muted-foreground">Server-verified gates</span></div>
                <div className="mt-3 grid gap-2 sm:grid-cols-2">
                  {(decisionSupport?.bookingChecks ?? []).map((check) => <div key={check.code} className={`flex items-start gap-2 border-b py-2 text-sm ${check.state === "Blocked" ? "border-destructive/30" : "border-border"}`}>{check.state === "Passed" ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-success" /> : check.state === "Warning" ? <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0 text-warning-foreground" /> : <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-destructive" />}<div><p className="font-medium">{check.code.replace(/_/g, " ")}</p><p className="mt-0.5 text-xs text-muted-foreground">{check.message}</p></div></div>)}
                </div>
              </section>

              <section aria-labelledby="csp-heading">
                <div className="flex items-center justify-between gap-3"><div><h3 id="csp-heading" className="text-sm font-semibold">CSP feasibility summary</h3><p className="mt-1 text-xs text-muted-foreground">{decisionSupport && !decisionSupport.resourcesEvaluated ? "Resources were not evaluated because a booking-level requirement is blocked." : "Only combinations that pass availability, overlap, equipment, capacity, document, and reachability constraints are ranked."}</p></div><span className="rounded-full border border-primary/20 bg-primary/10 px-2 py-1 text-xs font-semibold text-primary">{decisionSupport && !decisionSupport.resourcesEvaluated ? "Not evaluated" : `${decisionSupport?.feasibleCombinationCount ?? 0} valid`}</span></div>
              </section>

              <section aria-labelledby="recommendations-heading">
                <div className="flex items-center justify-between gap-3"><div><h3 id="recommendations-heading" className="text-sm font-semibold">TOPSIS ranked assignments</h3><p className="mt-1 text-xs text-muted-foreground">{decisionSupport ? `Generated ${formatDateTime(decisionSupport.generatedAt)} · expires ${formatDateTime(decisionSupport.expiresAt)} · ${decisionSupport.criteriaWeightVersion}` : "Loading ranked combinations…"}</p></div><Medal className="h-5 w-5 text-warning-foreground" /></div>
                <div className="mt-3 space-y-3">
                  {decisionSupport?.recommendations.length ? decisionSupport.recommendations.map((recommendation) => <article key={`${recommendation.driverUserId}-${recommendation.truckAssetId}-${recommendation.trailerAssetId ?? "none"}`} className={`rounded-xl border p-3 ${selectedRecommendation?.rank === recommendation.rank ? "border-primary bg-primary/5" : "border-border bg-muted/20"}`}><div className="flex items-start justify-between gap-3"><div><p className="text-sm font-semibold">Rank {recommendation.rank} · {recommendation.driverName} + {recommendation.truckCode}{recommendation.trailerCode ? ` + ${recommendation.trailerCode}` : ""}</p><p className="mt-1 text-xs text-muted-foreground">Score {Math.round(recommendation.score * 100)}% · {recommendation.reasons[0]}</p></div><Button type="button" variant="outline" size="sm" onClick={() => form && setForm({ ...form, driverUserId: recommendation.driverUserId, truckAssetId: recommendation.truckAssetId, trailerAssetId: recommendation.trailerAssetId ?? "" })}>Use rank {recommendation.rank}</Button></div><div className="mt-3 grid gap-1 text-xs text-muted-foreground sm:grid-cols-2">{recommendation.criteriaContributions.map((item) => <p key={item.criterion}>{item.criterion} ({Math.round(item.weight * 100)}%): {item.explanation}</p>)}</div>{recommendation.warnings.length ? <p className="mt-2 text-xs text-warning-foreground">Warning: {recommendation.warnings.join(" · ")}</p> : null}</article>) : <p className="rounded-xl border border-border bg-muted/20 p-3 text-sm text-muted-foreground">{decisionSupport && !decisionSupport.resourcesEvaluated ? "No TOPSIS ranking was generated: resources were not evaluated." : "Resources were evaluated, but no valid combination remains to rank."}</p>}
                </div>
              </section>

              <section aria-labelledby="excluded-heading">
                <div className="flex items-center justify-between gap-3"><h3 id="excluded-heading" className="text-sm font-semibold">Excluded resources and reasons</h3><span className="text-xs text-muted-foreground">Hard CSP exclusions</span></div>
                <div className="mt-3 space-y-2">{decisionSupport?.excludedResources.length ? decisionSupport.excludedResources.map((resource) => <div key={`${resource.resourceType}-${resource.resourceId}`} className="border-b border-border pb-2 text-sm"><p className="font-medium">{resource.resourceType} · {resource.resourceLabel}</p><p className="mt-1 text-xs text-muted-foreground">{resource.checks.filter((check) => check.state === "Blocked").map((check) => check.message).join(" · ")}</p></div>) : <p className="text-sm text-muted-foreground">{decisionSupport && !decisionSupport.resourcesEvaluated ? "Not applicable: resources were not evaluated." : "No resources are excluded for the saved schedule."}</p>}</div>
              </section>

              <section aria-labelledby="resources-heading">
                <div className="flex items-center justify-between gap-3"><h3 id="resources-heading" className="text-sm font-semibold">Manual assignment controls</h3><span className="text-xs text-muted-foreground">Only valid CSP options can be saved</span></div>
                <div className="mt-3 grid gap-4 sm:grid-cols-3">
                  <ResourceSelect id="driver" label="Driver" value={form.driverUserId} options={resources.drivers} isManager={false} onChange={(value) => setForm({ ...form, driverUserId: value })} />
                  <ResourceSelect id="truck" label="Truck" value={form.truckAssetId} options={resources.trucks} isManager={false} onChange={(value) => setForm({ ...form, truckAssetId: value })} />
                  <ResourceSelect id="trailer" label="Trailer" value={form.trailerAssetId} options={resources.trailers} optional isManager={false} onChange={(value) => setForm({ ...form, trailerAssetId: value })} />
                </div>
                {selectedConflicts.length > 0 ? (
                  <div className="mt-4 rounded-xl border border-destructive/30 bg-destructive/10 p-4">
                    <p className="flex items-center gap-2 text-sm font-semibold text-destructive"><AlertTriangle className="h-4 w-4" /> Assignment conflict</p>
                    <ul className="mt-2 space-y-1 text-sm text-foreground">{selectedConflicts.map((resource) => <li key={resource.id}>{resource.label} is busy on Trip {resource.conflictTripReference} until {formatDateTime(resource.busyUntil)}.</li>)}</ul>
                    <p className="mt-2 text-xs text-muted-foreground">Choose another resource. Overlapping assignments cannot pass Planning CSP validation.</p>
                  </div>
                ) : null}
              </section>

              <section aria-labelledby="details-heading">
                <h3 id="details-heading" className="text-sm font-semibold">Operational details</h3>
                <div className="mt-3 grid gap-4 sm:grid-cols-2">
                  <div className="space-y-2"><Label htmlFor="container-number">Container number</Label><Input id="container-number" value={form.containerNumber} onChange={(event) => setForm({ ...form, containerNumber: event.target.value })} placeholder="Required before dispatch" /></div>
                  <div className="rounded-xl border border-border bg-muted/30 p-3 text-sm"><p className="text-xs text-muted-foreground">Equipment requirement</p><p className="mt-1 font-semibold">{formatContainer(trip.containerSize)} · {formatTripType(trip.tripType)}</p></div>
                  <div className="space-y-2 sm:col-span-2"><Label htmlFor="planning-notes">Planning notes</Label><Textarea id="planning-notes" value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} placeholder="Port cutoffs, handling notes, or dispatch instructions" /></div>
                  <div className="space-y-2 sm:col-span-2"><Label htmlFor="planning-override-reason">Manual assignment override reason</Label><Textarea id="planning-override-reason" value={form.remarks} onChange={(event) => setForm({ ...form, remarks: event.target.value })} placeholder="Required only when choosing a valid CSP combination outside the ranked recommendations." /></div>
                </div>
              </section>

              <section aria-labelledby="readiness-heading">
                <div className="flex items-center justify-between gap-3"><div><h3 id="readiness-heading" className="text-sm font-semibold">Final Dispatch Validation</h3><p className="mt-1 text-xs text-muted-foreground">Validation does not change Trip state. Mark Ready reruns every constraint atomically.</p></div><ClipboardCheck className="h-5 w-5 text-primary" /></div>
                <div className="mt-3 rounded-xl border border-border bg-muted/20 p-3 text-sm"><p className="font-medium">{decisionSupport?.canMarkReady ? "All mandatory gates currently pass." : "Resolve blocked checks and select a valid assignment before handoff."}</p>{selectedRecommendation ? <p className="mt-1 text-xs text-muted-foreground">Selected rank: {selectedRecommendation.rank}.</p> : form.driverUserId && form.truckAssetId ? <p className="mt-1 text-xs text-warning-foreground">Manual selection requires a reason if it is outside the ranked list.</p> : null}</div>
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
  const { toasts, show } = useToast();
  const [board, setBoard] = useState<PlanningBoard | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [day, setDay] = useState("");
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

  const inPlanning = useMemo(() => board?.draftTrips ?? [], [board]);
  const ready = useMemo(() => board?.readyTrips ?? [], [board]);

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
      <PageHeader title="Dispatch Planning" description="Turn approved bookings into conflict-free, dispatch-ready trip plans." breadcrumbs={<nav className="flex items-center gap-2" aria-label="Breadcrumb"><Link to="/dispatch/board" className="text-muted-foreground hover:text-foreground">Dispatch</Link><span className="text-muted-foreground">/</span><span>Planning</span></nav>} actions={<Button variant="outline" size="sm" onClick={() => void loadBoard()} disabled={loading}><RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} /> Refresh</Button>} />

      <section className="surface-card overflow-hidden" aria-label="Planning controls">
        <div className="grid gap-4 border-b border-border p-4 sm:p-5 lg:grid-cols-[minmax(260px,1fr)_210px_auto] lg:items-end">
          <div className="space-y-2"><Label htmlFor="planning-search">Find a booking or trip</Label><div className="relative"><Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" /><Input id="planning-search" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder="Customer, booking, container, or route" className="pl-9" /></div></div>
          <div className="space-y-2"><Label htmlFor="planning-day">Planning date</Label><Input id="planning-day" type="date" value={day} onChange={(event) => setDay(event.target.value)} /></div>
          {day ? <Button variant="ghost" onClick={() => setDay("")}>Show all dates</Button> : <p className="pb-2 text-sm text-muted-foreground">All waiting work is visible</p>}
        </div>
        <div className="grid divide-y divide-border sm:grid-cols-4 sm:divide-x sm:divide-y-0">
          {[{ label: "Approved bookings", value: board?.approvedTotalCount ?? 0, icon: CalendarClock }, { label: "In planning", value: board?.draftTotalCount ?? 0, icon: CircleDashed }, { label: "Ready for dispatch", value: board?.readyCount ?? 0, icon: CheckCircle2 }, { label: "Plans with conflicts", value: board?.conflictCount ?? 0, icon: AlertTriangle }].map((item) => <div key={item.label} className="flex items-center gap-3 px-4 py-3 sm:px-5"><item.icon className="h-4 w-4 text-muted-foreground" /><div><p className="text-xl font-semibold tabular-nums">{item.value}</p><p className="text-xs text-muted-foreground">{item.label}</p></div></div>)}
        </div>
      </section>

      {loading && !board ? <LoadingSkeleton rows={8} /> : !board || (board.approvedBookings.length === 0 && board.draftTrips.length === 0 && board.readyTrips.length === 0) ? <div className="surface-card p-8"><EmptyState title="Planning workspace is clear" description="Approved bookings and Draft trip plans will appear here." /></div> : (
        <div className="grid gap-4 xl:grid-cols-3">
          <section className="min-w-0 rounded-2xl border border-border bg-muted/20" aria-labelledby="approved-lane">
            <div className="flex items-center justify-between border-b border-border px-4 py-3"><div><p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">01 · Intake</p><h2 id="approved-lane" className="mt-1 text-base font-semibold">Approved bookings</h2></div><span className="rounded-full bg-background px-2.5 py-1 text-xs font-semibold">{board.approvedBookings.length}</span></div>
            <div className="space-y-3 p-3">{board.approvedBookings.length === 0 ? <p className="px-2 py-8 text-center text-sm text-muted-foreground">No approved bookings waiting.</p> : board.approvedBookings.map((booking) => <BookingCard key={booking.requestId} booking={booking} onStart={(item) => void convert(item)} busy={busy} />)}</div>
          </section>
          <section className="min-w-0 rounded-2xl border border-border bg-muted/20" aria-labelledby="planning-lane">
            <div className="flex items-center justify-between border-b border-border px-4 py-3"><div><p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">02 · Build plan</p><h2 id="planning-lane" className="mt-1 text-base font-semibold">Scheduling and assignments</h2></div><span className="rounded-full bg-background px-2.5 py-1 text-xs font-semibold">{inPlanning.length}</span></div>
            <div className="space-y-3 p-3">{inPlanning.length === 0 ? <p className="px-2 py-8 text-center text-sm text-muted-foreground">No incomplete plans.</p> : inPlanning.map((trip) => <TripPlanCard key={trip.tripId} trip={trip} onOpen={setEditorTripId} />)}</div>
          </section>
          <section className="min-w-0 rounded-2xl border border-success/25 bg-success/5" aria-labelledby="ready-lane">
            <div className="flex items-center justify-between border-b border-success/20 px-4 py-3"><div><p className="text-xs font-semibold uppercase tracking-[0.16em] text-success">03 · Handoff</p><h2 id="ready-lane" className="mt-1 text-base font-semibold">Ready for dispatch</h2></div><span className="rounded-full bg-background px-2.5 py-1 text-xs font-semibold">{ready.length}</span></div>
            <div className="space-y-3 p-3">{ready.length === 0 ? <p className="px-2 py-8 text-center text-sm text-muted-foreground">Plans appear here after every dispatch gate passes.</p> : ready.map((trip) => <TripPlanCard key={trip.tripId} trip={trip} onOpen={setEditorTripId} />)}</div>
          </section>
        </div>
      )}

      <div className="flex items-center justify-between rounded-xl border border-border bg-muted/20 px-4 py-3 text-sm"><p className="text-muted-foreground"><Truck className="mr-2 inline h-4 w-4" />Ready for Dispatch is an explicit, reserved pre-execution state on the existing Draft trip.</p><Link to="/dispatch/board" className="hidden items-center gap-2 font-semibold text-primary sm:inline-flex">Go to execution <ArrowRight className="h-4 w-4" /></Link></div>

      <PlanningDrawer tripId={editorTripId} onClose={() => setEditorTripId(null)} onSaved={loadBoard} showToast={show} />
      <RescheduleDialog booking={rescheduleBooking} onClose={() => setRescheduleBooking(null)} onConfirm={(value) => { if (rescheduleBooking) void convert(rescheduleBooking, value); }} busy={busy} />
    </div>
  );
}
