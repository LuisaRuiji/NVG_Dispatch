import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Calendar, ChevronRight, Clock, MapPin, Navigation, RefreshCw, Truck } from "lucide-react";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import PageHeader from "@/components/PageHeader";
import StatusBadge from "@/components/StatusBadge";
import ToastHost from "@/components/ToastHost";
import { Button } from "@/components/ui/button";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import { useToast } from "@/lib/useToast";
import {
  getDriverDocumentBlockers,
  getDriverTripNextAction,
  summarizeDocumentBlockers
} from "./driverTripUi";
import type { DispatchTripListItem, TripStatus } from "./types";

type StatusScope = "ACTIVE" | "ALL";

const driverActionMap: Record<TripStatus, { endpoint: string; label: string } | null> = {
  DRAFT: null,
  READY_FOR_DISPATCH: null,
  DISPATCHED: { endpoint: "start", label: "Start trip to pickup" },
  ENROUTE_PICKUP: { endpoint: "arrive-pickup", label: "Mark arrived at pickup" },
  AT_PICKUP: { endpoint: "confirm-loaded", label: "Confirm container loaded" },
  LOADED: { endpoint: "depart-pickup", label: "Start trip to drop-off" },
  ENROUTE_DROPOFF: { endpoint: "arrive-dropoff", label: "Mark arrived at drop-off" },
  AT_DROPOFF: { endpoint: "confirm-delivery", label: "Confirm delivery" },
  DELIVERED: null,
  CLOSED: null,
  CANCELLED: null,
  ON_HOLD: null,
  FAILED_ATTEMPT: null
};

function formatDateTime(value?: string | null) {
  if (!value) return "Not scheduled";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Not scheduled";
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit"
  }).format(date);
}

function getErrorMessage(error: unknown, fallback: string) {
  return error instanceof Error ? error.message : fallback;
}

export default function MyTripsPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const [loading, setLoading] = useState(false);
  const [actionTripId, setActionTripId] = useState<string | null>(null);
  const [trips, setTrips] = useState<DispatchTripListItem[]>([]);
  const [statusScope, setStatusScope] = useState<StatusScope>("ACTIVE");
  const [selectedDate, setSelectedDate] = useState(() => new Date().toISOString().slice(0, 10));

  const loadTrips = async () => {
    try {
      setLoading(true);
      const params = new URLSearchParams({ scope: statusScope, page: "1", pageSize: "50" });
      if (statusScope === "ALL") {
        params.set("from", `${selectedDate}T00:00:00Z`);
        params.set("to", `${selectedDate}T23:59:59.999Z`);
      }
      const result = await api<PagedResult<DispatchTripListItem>>(`/api/dispatch/my-trips?${params}`, { method: "GET" });
      setTrips(result.items ?? []);
    } catch (error: unknown) {
      show(getErrorMessage(error, "Unable to load your trips."), "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadTrips();
  }, [statusScope, selectedDate]);

  const handleAction = async (trip: DispatchTripListItem, endpoint: string) => {
    try {
      setActionTripId(trip.id);
      await api(`/api/dispatch/trips/${trip.id}/${endpoint}`, {
        method: "POST",
        body: JSON.stringify({ eventAt: new Date().toISOString(), rowVersion: trip.rowVersion, remarks: null })
      });
      show("Trip updated.", "success");
      await loadTrips();
    } catch (error: unknown) {
      show(getErrorMessage(error, "The trip could not be updated. Refresh and try again."), "error");
      await loadTrips();
    } finally {
      setActionTripId(null);
    }
  };

  const activeTrip = useMemo(
    () => trips.find((trip) => driverActionMap[trip.status] !== null) ?? (statusScope === "ACTIVE" ? trips[0] ?? null : null),
    [statusScope, trips]
  );
  const remainingTrips = useMemo(() => trips.filter((trip) => trip.id !== activeTrip?.id), [activeTrip?.id, trips]);

  return (
    <div className="mx-auto max-w-5xl space-y-6 pb-8">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="My trips"
        description="Your current work, scheduled stops, and required documents."
        breadcrumbs={<span className="text-sm text-muted-foreground">Driver</span>}
      />

      <div className="flex flex-col gap-3 border-b border-border pb-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex rounded-full bg-muted p-1" role="tablist" aria-label="Trip schedule scope">
          {([
            ["ACTIVE", "Current"],
            ["ALL", "Schedules"]
          ] as const).map(([scope, label]) => (
            <button
              key={scope}
              type="button"
              role="tab"
              aria-selected={statusScope === scope}
              onClick={() => setStatusScope(scope)}
              className={`h-10 rounded-full px-4 text-sm font-semibold transition-colors ${
                statusScope === scope ? "bg-primary text-primary-foreground shadow-card" : "text-muted-foreground hover:text-foreground"
              }`}
            >
              {label}
            </button>
          ))}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {statusScope === "ALL" ? (
            <label className="flex h-10 items-center gap-2 rounded-full border border-input bg-card px-3 text-sm text-muted-foreground">
              <Calendar className="h-4 w-4" aria-hidden="true" />
              <span className="sr-only">Schedule date</span>
              <input
                type="date"
                value={selectedDate}
                onChange={(event) => setSelectedDate(event.target.value)}
                className="min-w-0 bg-transparent text-foreground outline-none"
              />
            </label>
          ) : null}
          <Button variant="outline" size="icon" onClick={() => void loadTrips()} disabled={loading} aria-label="Refresh trips">
            <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
          </Button>
        </div>
      </div>

      {loading && trips.length === 0 ? <LoadingSkeleton rows={5} /> : null}

      {!loading && trips.length === 0 ? (
        <EmptyState
          title={statusScope === "ACTIVE" ? "No current trip" : "No scheduled trips"}
          description={statusScope === "ACTIVE" ? "You do not have an active assignment right now." : "There are no trips assigned for this date."}
        />
      ) : null}

      {activeTrip ? (
        <section aria-labelledby="current-trip-heading" className="surface-card overflow-hidden">
          <div className="flex flex-col gap-4 border-b border-border bg-muted/45 p-5 sm:flex-row sm:items-start sm:justify-between">
            <div className="min-w-0">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">Current trip</p>
              <div className="mt-2 flex flex-wrap items-center gap-2">
                <h2 id="current-trip-heading" className="font-mono text-lg font-bold text-foreground">{activeTrip.id.slice(0, 8).toUpperCase()}</h2>
                <StatusBadge status={activeTrip.status} />
              </div>
              <p className="mt-2 text-sm text-muted-foreground">
                {activeTrip.customer?.name ?? "Customer not specified"}
                {activeTrip.containerNumber ? <span className="ml-2 text-foreground">Container {activeTrip.containerNumber}</span> : null}
              </p>
            </div>
            <p className="max-w-sm text-sm font-medium text-foreground">{getDriverTripNextAction(activeTrip)}</p>
          </div>

          <div className="grid gap-5 p-5 md:grid-cols-2">
            <StopSummary label="Pickup" location={activeTrip.pickupLocation} scheduledAt={activeTrip.pickupScheduledAt} tone="pickup" />
            <StopSummary label="Drop-off" location={activeTrip.dropoffLocation} scheduledAt={activeTrip.dropoffScheduledAt} tone="dropoff" />
          </div>

          <div className="flex flex-col gap-4 border-t border-border p-5 sm:flex-row sm:items-center sm:justify-between">
            <div className="space-y-1 text-sm">
              <p className="flex items-center gap-2 text-foreground"><Truck className="h-4 w-4 text-muted-foreground" /> {activeTrip.truckAssetCode ?? "Truck not specified"}</p>
              {summarizeDocumentBlockers(getDriverDocumentBlockers(activeTrip.documents, activeTrip.missingRequiredDocumentCount)).map((blocker) => (
                <p key={blocker} className="text-xs font-medium text-amber-700 dark:text-amber-300">Document attention: {blocker}</p>
              ))}
            </div>
            <div className="flex flex-col gap-2 sm:flex-row">
              <Button variant="outline" onClick={() => nav(`/dispatch/my-trips/${activeTrip.id}`)}>
                Trip details <ChevronRight className="h-4 w-4" />
              </Button>
              {driverActionMap[activeTrip.status] ? (
                <Button
                  className="h-12 sm:h-11"
                  disabled={actionTripId !== null}
                  onClick={() => void handleAction(activeTrip, driverActionMap[activeTrip.status]!.endpoint)}
                >
                  <Navigation className="h-4 w-4" />
                  {actionTripId === activeTrip.id ? "Updating…" : driverActionMap[activeTrip.status]!.label}
                </Button>
              ) : null}
            </div>
          </div>
        </section>
      ) : null}

      {remainingTrips.length > 0 ? (
        <section aria-labelledby="scheduled-trips-heading" className="space-y-3">
          <div className="flex items-baseline justify-between gap-3">
            <h2 id="scheduled-trips-heading" className="text-lg font-semibold text-foreground">{activeTrip ? "Other assigned trips" : "Scheduled trips"}</h2>
            <span className="text-xs text-muted-foreground">{remainingTrips.length} scheduled</span>
          </div>
          <div className="overflow-hidden rounded-2xl border border-border bg-card">
            {remainingTrips.map((trip) => (
              <button
                key={trip.id}
                type="button"
                onClick={() => nav(`/dispatch/my-trips/${trip.id}`)}
                className="flex w-full items-center justify-between gap-4 border-b border-border px-4 py-4 text-left last:border-b-0 hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-sm font-semibold">{trip.id.slice(0, 8).toUpperCase()}</span>
                    <StatusBadge status={trip.status} />
                  </div>
                  <p className="mt-1 truncate text-sm text-foreground">{trip.pickupLocation ?? "Pickup not specified"} <span className="text-muted-foreground">→</span> {trip.dropoffLocation ?? "Drop-off not specified"}</p>
                  <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(trip.pickupScheduledAt)}</p>
                </div>
                <ChevronRight className="h-5 w-5 shrink-0 text-muted-foreground" />
              </button>
            ))}
          </div>
        </section>
      ) : null}
    </div>
  );
}

function StopSummary({ label, location, scheduledAt, tone }: { label: string; location?: string | null; scheduledAt?: string | null; tone: "pickup" | "dropoff" }) {
  return (
    <div className="flex gap-3">
      <MapPin className={`mt-0.5 h-5 w-5 shrink-0 ${tone === "pickup" ? "text-primary" : "text-accent"}`} aria-hidden="true" />
      <div className="min-w-0">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">{label}</p>
        <p className="mt-1 font-semibold text-foreground">{location ?? "Location not specified"}</p>
        <p className="mt-1 flex items-center gap-1.5 text-xs text-muted-foreground"><Clock className="h-3.5 w-3.5" />{formatDateTime(scheduledAt)}</p>
      </div>
    </div>
  );
}
