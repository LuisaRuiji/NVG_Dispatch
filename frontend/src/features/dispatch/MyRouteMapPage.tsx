import { useEffect } from "react";
import { ArrowLeft, AlertTriangle, LocateFixed, Navigation, RefreshCw, Signal, Truck } from "lucide-react";
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

export default function MyRouteMapPage() {
  const nav = useNavigate();
  const { toasts } = useToast();
  const { trip, loading, error, trackingActive, currentCoords, gpsWarning, networkError, startWatcher, stopWatcher, fetchActiveTrip } = useTracking();

  useEffect(() => {
    void fetchActiveTrip();
    return () => { void stopWatcher(); };
  }, []);

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
          <div><p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">Active trip</p><div className="mt-2 flex flex-wrap items-center gap-2"><span className="font-mono text-lg font-bold">{trip.tripId.slice(0, 8).toUpperCase()}</span><StatusBadge status={trip.currentTripStatus} /></div><p className="mt-2 text-sm text-muted-foreground">Next stop: {trip.currentTripStatus.replace(/_/g, " ").toLowerCase().includes("dropoff") ? trip.dropoffLocation ?? "Drop-off" : trip.pickupLocation ?? "Pickup"}</p></div>
          <div className="flex flex-wrap gap-2"><Button variant="outline" size="sm" onClick={() => void fetchActiveTrip()}><RefreshCw className="h-4 w-4" />Refresh map</Button>{trackingActive ? <Button variant="outline" size="sm" onClick={() => void stopWatcher()}><Signal className="h-4 w-4" />Stop sharing</Button> : <Button size="sm" onClick={() => void startWatcher()}><LocateFixed className="h-4 w-4" />Share current location</Button>}</div>
        </div>
        <TripMap
          className="rounded-none border-0"
          heightClassName="h-[min(62vh,38rem)] min-h-[24rem]"
          pickup={{ latitude: trip.pickupLatitude, longitude: trip.pickupLongitude, label: trip.pickupLocation ?? "Pickup" }}
          dropoff={{ latitude: trip.dropoffLatitude, longitude: trip.dropoffLongitude, label: trip.dropoffLocation ?? "Drop-off" }}
          driver={driver}
          driverRecordedAt={lastUpdatedAt}
          driverAccuracyMeters={currentCoords?.accuracy}
          emptyTitle="No route coordinates available"
        />
      </section>

      <div className="grid gap-4 md:grid-cols-3">
        <section className="surface-soft p-4"><p className="flex items-center gap-2 text-sm font-semibold"><Signal className="h-4 w-4 text-primary" />Location sharing</p><p className="mt-2 text-sm text-foreground">{trackingActive ? "Sharing while this page is open" : "Not currently sharing"}</p><p className="mt-1 text-xs text-muted-foreground">VAIA only uses this location for the assigned active trip.</p></section>
        <section className="surface-soft p-4"><p className="flex items-center gap-2 text-sm font-semibold"><LocateFixed className="h-4 w-4 text-primary" />Last location</p><p className="mt-2 text-sm text-foreground">{formatLastUpdated(lastUpdatedAt)}</p><p className="mt-1 text-xs text-muted-foreground">{driver ? "Shown on the map above." : "Share your location when ready."}</p></section>
        <section className="surface-soft p-4"><p className="flex items-center gap-2 text-sm font-semibold"><Truck className="h-4 w-4 text-primary" />Assigned vehicle</p><p className="mt-2 text-sm text-foreground">{trip.plateNumber || "Truck not specified"}</p><p className="mt-1 text-xs text-muted-foreground">Trip-scoped location visibility.</p></section>
      </div>

      {gpsWarning || networkError || hasCoordinateWarning ? <section role="status" className="flex items-start gap-3 rounded-2xl border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-sm text-amber-800 dark:text-amber-200"><AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" /><div><p className="font-semibold">Location needs attention</p><p className="mt-1 text-xs">{networkError ? "Location sharing is reconnecting. Your trip status remains unchanged." : gpsWarning ? "GPS signal is weak or unavailable. Move to an open area and try sharing again." : "Pickup or drop-off map coordinates are missing. Contact dispatch to update the trip pin."}</p></div></section> : null}
    </div>
  );
}
