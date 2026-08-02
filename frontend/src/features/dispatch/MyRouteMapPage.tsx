import { ArrowLeft, AlertTriangle, Clock3, LocateFixed, Navigation, RefreshCw, Signal, Truck } from "lucide-react";
import { useNavigate } from "react-router-dom";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import PageHeader from "@/components/PageHeader";
import StatusBadge from "@/components/StatusBadge";
import ToastHost from "@/components/ToastHost";
import TripMap from "@/components/dispatch/TripMap";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { useTracking } from "./TrackingContext";

function formatLastUpdated(value?: string | null) {
  if (!value) return "No location update received";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "No location update received";
  const minutes = Math.max(0, Math.round((Date.now() - date.getTime()) / 60000));
  return minutes < 1 ? "Updated just now" : `Updated ${minutes} minute${minutes === 1 ? "" : "s"} ago`;
}

function formatEtaTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Calculating…";
  return new Intl.DateTimeFormat(undefined, { hour: "numeric", minute: "2-digit" }).format(date);
}

function formatTravelMinutes(value: number) {
  const hours = Math.floor(value / 60);
  const minutes = value % 60;
  if (!hours) return `${minutes} min`;
  return minutes ? `${hours} hr ${minutes} min` : `${hours} hr`;
}

export default function MyRouteMapPage() {
  const nav = useNavigate();
  const { toasts } = useToast();
  const { trip, loading, error, trackingActive, trackingStarting, eta, currentCoords, gpsWarning, networkError, startWatcher, fetchActiveTrip } = useTracking();

  if (loading && !trip) {
    return <div className="mx-auto max-w-6xl space-y-6"><PageHeader title="Route map" description="Loading active trip…" /><LoadingSkeleton rows={6} /></div>;
  }

  if (!trip) {
    return <div className="mx-auto max-w-3xl space-y-6"><PageHeader title="Route map" description="Route visibility is available for your active trip." /><EmptyState title="No active route" description={error ?? "A route map becomes available when you have an active assigned trip."} /></div>;
  }

  const driver = currentCoords
    ? { latitude: currentCoords.latitude, longitude: currentCoords.longitude, label: "Your current location" }
    : trip.lastLatitude != null && trip.lastLongitude != null
      ? { latitude: trip.lastLatitude, longitude: trip.lastLongitude, label: "Last shared location" }
      : null;
  const lastUpdatedAt = currentCoords ? new Date().toISOString() : trip.lastLocationAt;
  const hasCoordinateWarning = trip.pickupLatitude == null || trip.pickupLongitude == null || trip.dropoffLatitude == null || trip.dropoffLongitude == null;
  const normalizedStatus = trip.currentTripStatus.replace(/_/g, "").toUpperCase();
  const trackingRequired = ["ENROUTEPICKUP", "ATPICKUP", "LOADED", "ENROUTEDROPOFF", "ATDROPOFF", "ONHOLD", "FAILEDATTEMPT"].includes(normalizedStatus);
  const activeLeg = ["LOADED", "ENROUTEDROPOFF", "ATDROPOFF"].includes(normalizedStatus) ? "dropoff" : "pickup";
  const nextStopLabel = activeLeg === "dropoff" ? trip.dropoffLocation ?? "Drop-off" : trip.pickupLocation ?? "Pickup";

  return (
    <div className="mx-auto max-w-6xl space-y-6 pb-8">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Route map"
        description="Your current trip route and the latest shared location."
        breadcrumbs={<span className="text-sm text-muted-foreground">My trips / Route map</span>}
        actions={<div className="flex gap-2"><Button variant="outline" onClick={() => nav(`/dispatch/my-trips/${trip.tripId}`)}><Navigation className="h-4 w-4" />Open trip</Button><Button variant="outline" size="icon" aria-label="Back to my trips" onClick={() => nav("/dispatch/my-trips")}><ArrowLeft className="h-4 w-4" /></Button></div>}
      />

      <section className="surface-card overflow-hidden">
        <div className="flex flex-col gap-4 border-b border-border p-5 md:flex-row md:items-center md:justify-between md:p-6">
          <div><p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">Active trip</p><div className="mt-2 flex flex-wrap items-center gap-2"><span className="font-mono text-lg font-bold">{trip.tripId.slice(0, 8).toUpperCase()}</span><StatusBadge status={trip.currentTripStatus} /></div><p className="mt-2 text-sm text-muted-foreground">Next stop: {nextStopLabel}</p><p className="mt-2 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-foreground"><Clock3 className="h-4 w-4 text-primary" /><span className="font-semibold">ETA to {activeLeg === "dropoff" ? "drop-off" : "pickup"}:</span>{eta && eta.destinationType === activeLeg.toUpperCase() ? <><span className="font-semibold">{formatEtaTime(eta.estimatedArrivalAt)}</span><span className="text-xs text-muted-foreground">about {formatTravelMinutes(eta.estimatedTravelMinutes)} · {eta.remainingDistanceKm.toFixed(1)} km remaining</span></> : <span className="text-xs text-muted-foreground">Calculating from the latest shared location…</span>}</p></div>
          <div className="flex flex-wrap items-center gap-2"><Button variant="outline" size="sm" onClick={() => void fetchActiveTrip()}><RefreshCw className="h-4 w-4" />Refresh map</Button>{trackingStarting ? <span role="status" className="inline-flex h-9 items-center gap-2 rounded-lg border border-border bg-muted px-3 text-xs font-semibold text-muted-foreground"><LocateFixed className="h-4 w-4" />Starting location sharing…</span> : trackingActive ? <span role="status" className="inline-flex h-9 items-center gap-2 rounded-lg border border-success/30 bg-success/10 px-3 text-xs font-semibold text-success"><Signal className="h-4 w-4" />Location sharing active</span> : trackingRequired ? <Button size="sm" onClick={() => void startWatcher()}><LocateFixed className="h-4 w-4" />Retry location sharing</Button> : <span className="text-xs text-muted-foreground">Sharing starts when the trip begins</span>}</div>
        </div>
        <TripMap
          className="rounded-none border-0"
          heightClassName="h-[min(62vh,38rem)] min-h-[24rem]"
          pickup={{ latitude: trip.pickupLatitude, longitude: trip.pickupLongitude, label: trip.pickupLocation ?? "Pickup" }}
          dropoff={{ latitude: trip.dropoffLatitude, longitude: trip.dropoffLongitude, label: trip.dropoffLocation ?? "Drop-off" }}
          driver={driver}
          activeLeg={activeLeg}
          driverRecordedAt={lastUpdatedAt}
          driverAccuracyMeters={currentCoords?.accuracy}
          emptyTitle="No route coordinates available"
        />
      </section>

      <div className="grid gap-4 md:grid-cols-3">
        <section className="surface-soft p-4"><p className="flex items-center gap-2 text-sm font-semibold"><Signal className="h-4 w-4 text-primary" />Location sharing</p><p className="mt-2 text-sm text-foreground">{trackingActive ? "Sharing automatically while VAIA is open" : trackingStarting ? "Starting automatically…" : trackingRequired ? "Location access needs attention" : "Starts automatically with the trip"}</p><p className="mt-1 text-xs text-muted-foreground">VAIA only shares location for the assigned active trip.</p></section>
        <section className="surface-soft p-4"><p className="flex items-center gap-2 text-sm font-semibold"><LocateFixed className="h-4 w-4 text-primary" />Last location</p><p className="mt-2 text-sm text-foreground">{formatLastUpdated(lastUpdatedAt)}</p><p className="mt-1 text-xs text-muted-foreground">{driver ? "Shown on the map above." : "Waiting for the first automatic GPS update."}</p></section>
        <section className="surface-soft p-4"><p className="flex items-center gap-2 text-sm font-semibold"><Truck className="h-4 w-4 text-primary" />Assigned vehicle</p><p className="mt-2 text-sm text-foreground">{trip.plateNumber || "Truck not specified"}</p><p className="mt-1 text-xs text-muted-foreground">Trip-scoped location visibility.</p></section>
      </div>

      {gpsWarning || networkError || hasCoordinateWarning ? <section role="status" className="flex items-start gap-3 rounded-2xl border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-sm text-amber-800 dark:text-amber-200"><AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" /><div><p className="font-semibold">Location needs attention</p><p className="mt-1 text-xs">{networkError ? "Location sharing is reconnecting. Your trip status remains unchanged." : gpsWarning ?? "Pickup or drop-off map coordinates are missing. Contact dispatch to update the trip pin."}</p></div></section> : null}
    </div>
  );
}
