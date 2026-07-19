import { useEffect } from "react";
import { Link, useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useToast } from "@/lib/useToast";
import {
  Clock,
  ArrowLeft,
  Navigation,
  Compass,
  Zap,
  Activity,
  AlertTriangle,
  Play,
  Square,
  AlertOctagon,
  RefreshCw,
  Terminal
} from "lucide-react";
import { useTracking } from "./TrackingContext";
import { MapContainer, TileLayer, Marker, Popup, useMap } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";

// Simplified bulletproof Leaflet icons matching LiveOperationsMap style
const pickupIcon = L.divIcon({
  className: "custom-leaflet-icon",
  html: `<div style="background-color: #10b981; width: 32px; height: 32px; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; box-shadow: 0 2px 4px rgba(0,0,0,0.3); font-size: 16px;">📦</div>`,
  iconSize: [32, 32],
  iconAnchor: [16, 16],
  popupAnchor: [0, -16]
});

const dropoffIcon = L.divIcon({
  className: "custom-leaflet-icon",
  html: `<div style="background-color: #3b82f6; width: 32px; height: 32px; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; box-shadow: 0 2px 4px rgba(0,0,0,0.3); font-size: 16px;">📍</div>`,
  iconSize: [32, 32],
  iconAnchor: [16, 16],
  popupAnchor: [0, -16]
});

const truckIcon = L.divIcon({
  className: "custom-leaflet-icon",
  html: `<div style="background-color: #6366f1; width: 32px; height: 32px; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; box-shadow: 0 2px 4px rgba(0,0,0,0.3); font-size: 16px;">🚚</div>`,
  iconSize: [32, 32],
  iconAnchor: [16, 16],
  popupAnchor: [0, -16]
});

// Helper component to auto-adjust map bounds dynamically based on active trip status
function MapBoundsUpdater({ pickup, dropoff, truck, isHeadingToPickup }: { pickup: [number, number] | null; dropoff: [number, number] | null; truck: [number, number] | null; isHeadingToPickup: boolean }) {
  const map = useMap();
  useEffect(() => {
    const points: [number, number][] = [];
    if (truck && truck[0] && truck[1]) points.push(truck);
    
    if (isHeadingToPickup) {
      if (pickup && pickup[0] && pickup[1]) points.push(pickup);
    } else {
      if (dropoff && dropoff[0] && dropoff[1]) points.push(dropoff);
    }
    
    if (points.length > 1) {
      try {
        const bounds = L.latLngBounds(points);
        map.fitBounds(bounds, { padding: [40, 40], maxZoom: 15 });
      } catch (e) {
        // ignore
      }
    } else if (points.length === 1) {
      try {
        map.setView(points[0], 14);
      } catch (e) {
        // ignore
      }
    }
  }, [pickup, dropoff, truck, isHeadingToPickup, map]);
  return null;
}

// Helper component to draw the routing polyline following actual roads via OSRM public routing API
function RoutePolyline({ from, to, color }: { from: [number, number] | null; to: [number, number] | null; color: string }) {
  const map = useMap();
  useEffect(() => {
    if (!from || !to || !from[0] || !from[1] || !to[0] || !to[1]) return;

    let polyline: L.Polyline | null = null;

    const fetchRoute = async () => {
      try {
        // OSRM public API: coordinates are [longitude, latitude]
        const url = `https://router.project-osrm.org/route/v1/driving/${from[1]},${from[0]};${to[1]},${to[0]}?overview=full&geometries=geojson`;
        const response = await fetch(url);
        const data = await response.json();

        if (data.routes && data.routes.length > 0) {
          // OSRM returns [lng, lat] — Leaflet needs [lat, lng]
          const coords: [number, number][] = data.routes[0].geometry.coordinates.map(
            (c: [number, number]) => [c[1], c[0]] as [number, number]
          );
          polyline = L.polyline(coords, {
            color: color,
            weight: 4,
            opacity: 0.85,
          }).addTo(map);
        } else {
          // fallback to straight line if OSRM fails
          polyline = L.polyline([from, to], { color, weight: 3, dashArray: "6, 6", opacity: 0.7 }).addTo(map);
        }
      } catch {
        // fallback to straight line on network error
        try {
          polyline = L.polyline([from, to], { color, weight: 3, dashArray: "6, 6", opacity: 0.7 }).addTo(map);
        } catch { /* ignore */ }
      }
    };

    fetchRoute();

    return () => {
      if (polyline) polyline.remove();
    };
  }, [from?.[0], from?.[1], to?.[0], to?.[1], color, map]);

  return null;
}

export default function MyRouteMapPage() {
  const {
    trip,
    loading,
    error,
    trackingActive,
    currentCoords,
    gpsWarning,
    networkError,
    logs,
    startWatcher,
    stopWatcher,
    fetchActiveTrip
  } = useTracking();
  
  const { toasts } = useToast();
  const nav = useNavigate();

  useEffect(() => {
    fetchActiveTrip();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleStartTracking = async () => {
    await startWatcher();
  };

  const handleStopTracking = async () => {
    await stopWatcher();
  };

  return (
    <div className="container mx-auto p-4 max-w-4xl space-y-6">
      <ToastHost toasts={toasts} />

      <PageHeader
        title="My Route Map"
        description="Real-time GPS tracker — only visible to you"
        breadcrumbs={
          <nav className="flex items-center gap-1.5 text-xs">
            <Link to="/dispatch/my-trips" className="text-muted-foreground hover:text-foreground font-medium">
              My Trips
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground font-semibold">Route Map</span>
          </nav>
        }
        actions={
          <button
            onClick={() => nav("/dispatch/my-trips")}
            className="flex items-center gap-1.5 text-xs font-medium text-muted-foreground hover:text-foreground px-3 py-1.5 rounded-lg hover:bg-secondary transition-colors"
          >
            <ArrowLeft className="h-3.5 w-3.5" /> Back to Trips
          </button>
        }
      />

      {loading && !trip ? (
        <div className="space-y-4">
          <LoadingSkeleton rows={6} />
        </div>
      ) : error ? (
        <Card className="border-destructive/30 bg-destructive/5 p-8 text-center space-y-4">
          <AlertOctagon className="h-12 w-12 mx-auto text-destructive" />
          <h3 className="font-semibold text-lg text-destructive">Unable to Load Route Map</h3>
          <p className="text-sm text-muted-foreground">{error}</p>
          <button
            onClick={fetchActiveTrip}
            className="px-4 py-2 bg-secondary text-foreground text-xs font-semibold rounded-lg hover:bg-secondary/80 transition-colors inline-flex items-center gap-1.5"
          >
            <RefreshCw className="h-3.5 w-3.5" /> Retry
          </button>
        </Card>
      ) : trip ? (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">

          {/* Left column — controls + vehicle info */}
          <div className="md:col-span-1 space-y-6">

            {/* Tracking Controller */}
            <Card className="overflow-hidden border border-border bg-card shadow-sm">
              <CardHeader className="bg-muted/50 pb-3 pt-4 px-5">
                <CardTitle className="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
                  <Zap className="h-3.5 w-3.5 text-primary" /> Tracking Controller
                </CardTitle>
              </CardHeader>
              <CardContent className="pt-5 px-5 pb-5 space-y-4">
                {/* Trip status badge */}
                <div className="flex items-center justify-between">
                  <span className="text-xs text-muted-foreground">Trip Status</span>
                  <StatusBadge status={trip.currentTripStatus} />
                </div>

                {/* Live indicator */}
                <div className="flex items-center justify-between p-3 rounded-lg bg-background border border-border">
                  <div className="flex items-center gap-2">
                    <Activity
                      className={`h-4 w-4 ${trackingActive ? "text-red-500 animate-pulse" : "text-muted-foreground"}`}
                    />
                    <span className="text-xs font-semibold">GPS Service</span>
                  </div>
                  <span
                    className={`text-xs font-bold uppercase tracking-wider ${
                      trackingActive ? "text-red-500" : "text-muted-foreground"
                    }`}
                  >
                    {trackingActive ? "● Live" : "Offline"}
                  </span>
                </div>

                {/* Action button */}
                {!trackingActive ? (
                  <button
                    onClick={handleStartTracking}
                    disabled={loading}
                    className="w-full py-2.5 bg-primary hover:bg-primary/90 disabled:opacity-50 text-primary-foreground font-semibold rounded-lg flex items-center justify-center gap-2 text-sm transition-all active:scale-[0.98]"
                  >
                    <Play className="h-4 w-4" /> Start Tracking
                  </button>
                ) : (
                  <button
                    onClick={handleStopTracking}
                    disabled={loading}
                    className="w-full py-2.5 bg-destructive hover:bg-destructive/90 disabled:opacity-50 text-destructive-foreground font-semibold rounded-lg flex items-center justify-center gap-2 text-sm transition-all active:scale-[0.98]"
                  >
                    <Square className="h-4 w-4" /> Stop Tracking
                  </button>
                )}

                {/* Warnings */}
                {gpsWarning && (
                  <div className="p-3 bg-amber-500/10 border border-amber-500/30 text-amber-600 dark:text-amber-400 text-xs rounded-lg flex items-start gap-2">
                    <AlertTriangle className="h-3.5 w-3.5 shrink-0 mt-0.5" />
                    <span>{gpsWarning}</span>
                  </div>
                )}
                {networkError && (
                  <div className="p-3 bg-destructive/10 border border-destructive/30 text-destructive text-xs rounded-lg flex items-start gap-2">
                    <AlertTriangle className="h-3.5 w-3.5 shrink-0 mt-0.5" />
                    <span>{networkError}</span>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Vehicle info */}
            <Card className="overflow-hidden border border-border bg-card shadow-sm">
              <CardHeader className="bg-muted/50 pb-3 pt-4 px-5">
                <CardTitle className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
                  Vehicle Profile
                </CardTitle>
              </CardHeader>
              <CardContent className="pt-5 px-5 pb-5 space-y-3">
                <div className="flex justify-between border-b border-border pb-2.5 text-xs">
                  <span className="text-muted-foreground">Plate Number</span>
                  <span className="font-bold font-mono">{trip.plateNumber}</span>
                </div>
                <div className="flex justify-between border-b border-border pb-2.5 text-xs">
                  <span className="text-muted-foreground">Driver</span>
                  <span className="font-bold">{trip.driverName}</span>
                </div>
                <div className="flex justify-between text-xs">
                  <span className="text-muted-foreground">Session ID</span>
                  <span className="font-mono text-[10px] text-muted-foreground">{trip.tripId.slice(0, 8).toUpperCase()}…</span>
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Right column — route visual + telemetry + logs */}
          <div className="md:col-span-2 space-y-6">

            {/* Transit Route Visualizer */}
            <Card className="overflow-hidden border border-border bg-card shadow-sm">
              <CardHeader className="bg-muted/50 pb-3 pt-4 px-5">
                <CardTitle className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
                  Transit Route
                </CardTitle>
              </CardHeader>
              <CardContent className="pt-5 px-5 pb-5 space-y-6">
                {/* Route Map Container */}
                <div className="relative w-full h-[320px] bg-background border border-border rounded-lg overflow-hidden z-0">
                  <MapContainer center={[trip.pickupLatitude || 7.07, trip.pickupLongitude || 125.6]} zoom={12} className="h-full w-full">
                    <TileLayer
                      attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                      url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                    />
                    
                    {(() => {
                      const status = trip.currentTripStatus.toUpperCase().replace(/_/g, "");
                      const isHeadingToPickup = ["DISPATCHED", "ENROUTEPICKUP", "ATPICKUP"].includes(status);
                      const isHeadingToDropoff = ["LOADED", "ENROUTEDROPOFF", "ATDROPOFF"].includes(status);
                      const truckCoords: [number, number] | null = currentCoords 
                        ? [currentCoords.latitude, currentCoords.longitude] 
                        : (trip.lastLatitude && trip.lastLongitude ? [trip.lastLatitude, trip.lastLongitude] : null);

                      return (
                        <>
                          <MapBoundsUpdater 
                            pickup={trip.pickupLatitude && trip.pickupLongitude ? [trip.pickupLatitude, trip.pickupLongitude] : null}
                            dropoff={trip.dropoffLatitude && trip.dropoffLongitude ? [trip.dropoffLatitude, trip.dropoffLongitude] : null}
                            truck={truckCoords}
                            isHeadingToPickup={isHeadingToPickup}
                          />

                          {/* Render Pickup only if we are still heading to it */}
                          {isHeadingToPickup && trip.pickupLatitude && trip.pickupLongitude && (
                            <Marker position={[trip.pickupLatitude, trip.pickupLongitude]} icon={pickupIcon}>
                              <Popup>
                                <div className="text-xs space-y-0.5">
                                  <p className="font-bold text-emerald-600 uppercase tracking-wide">Pickup Stop</p>
                                  <p className="font-medium text-foreground">{trip.pickupLocation}</p>
                                </div>
                              </Popup>
                            </Marker>
                          )}

                          {trip.dropoffLatitude && trip.dropoffLongitude && (
                            <Marker position={[trip.dropoffLatitude, trip.dropoffLongitude]} icon={dropoffIcon}>
                              <Popup>
                                <div className="text-xs space-y-0.5">
                                  <p className="font-bold text-blue-600 uppercase tracking-wide">Dropoff Stop</p>
                                  <p className="font-medium text-foreground">{trip.dropoffLocation}</p>
                                </div>
                              </Popup>
                            </Marker>
                          )}

                          {/* Active truck position */}
                          {truckCoords && (
                            <Marker position={truckCoords} icon={truckIcon}>
                              <Popup>
                                <div className="text-xs space-y-1">
                                  <p className="font-bold text-indigo-600">{trip.plateNumber} (Your Truck)</p>
                                  <p className="text-[10px] text-muted-foreground">Driver: {trip.driverName}</p>
                                  {currentCoords != null && currentCoords.speed != null && (
                                    <p className="font-mono text-[10px] bg-secondary px-1.5 py-0.5 rounded inline-block">
                                      Speed: {Math.round(currentCoords.speed * 3.6)} km/h
                                    </p>
                                  )}
                                </div>
                              </Popup>
                            </Marker>
                          )}

                          {isHeadingToPickup && (
                            <RoutePolyline 
                              from={truckCoords} 
                              to={trip.pickupLatitude && trip.pickupLongitude ? [trip.pickupLatitude, trip.pickupLongitude] : null}
                              color="#10b981"
                            />
                          )}

                          {/* Dynamic route polyline connecting truck to next objective */}
                          {isHeadingToDropoff && (
                            <RoutePolyline 
                              from={truckCoords} 
                              to={trip.dropoffLatitude && trip.dropoffLongitude ? [trip.dropoffLatitude, trip.dropoffLongitude] : null}
                              color="#3b82f6"
                            />
                          )}
                        </>
                      );
                    })()}
                  </MapContainer>
                </div>

                {/* Delay warning */}
                {trip.delayFlag && (
                  <div className="flex items-center gap-2 text-xs text-amber-600 dark:text-amber-400 bg-amber-500/10 border border-amber-500/30 px-3 py-2 rounded-lg">
                    <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
                    Delay detected on this route. Please contact dispatch if unable to recover schedule.
                  </div>
                )}

                {/* Telemetry panels */}
                <div className="grid grid-cols-3 gap-3">
                  <div className="p-3 bg-muted/30 border border-border rounded-lg text-center space-y-1">
                    <Compass className="h-4 w-4 mx-auto text-primary" />
                    <div className="text-[10px] text-muted-foreground font-bold uppercase tracking-wide">Heading</div>
                    <div className="text-sm font-bold font-mono">
                      {currentCoords?.heading != null ? `${Math.round(currentCoords.heading)}°` : "—"}
                    </div>
                  </div>
                  <div className="p-3 bg-muted/30 border border-border rounded-lg text-center space-y-1">
                    <Navigation className="h-4 w-4 mx-auto text-primary rotate-45" />
                    <div className="text-[10px] text-muted-foreground font-bold uppercase tracking-wide">Speed</div>
                    <div className="text-sm font-bold font-mono">
                      {currentCoords?.speed != null ? `${Math.round(currentCoords.speed * 3.6)} km/h` : "0 km/h"}
                    </div>
                  </div>
                  <div className="p-3 bg-muted/30 border border-border rounded-lg text-center space-y-1">
                    <Clock className="h-4 w-4 mx-auto text-primary" />
                    <div className="text-[10px] text-muted-foreground font-bold uppercase tracking-wide">Accuracy</div>
                    <div className="text-sm font-bold font-mono">
                      {currentCoords ? `±${Math.round(currentCoords.accuracy)}m` : "—"}
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>

            {/* GPS Log Console */}
            <Card className="overflow-hidden border border-border bg-card shadow-sm">
              <CardHeader className="bg-muted/50 pb-3 pt-4 px-5 flex flex-row items-center justify-between">
                <CardTitle className="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
                  <Terminal className="h-3.5 w-3.5 text-primary" /> GPS Event Log
                </CardTitle>
                <span className="text-[10px] font-mono text-muted-foreground">DriverApp v1.0</span>
              </CardHeader>
              <CardContent className="pt-4 px-5 pb-5">
                <div className="h-44 w-full bg-zinc-950 border border-zinc-800 rounded-lg p-3 overflow-y-auto font-mono text-xs text-zinc-400 space-y-0.5">
                  {logs.length === 0 ? (
                    <div className="text-zinc-600 italic">No GPS events yet. Press Start Tracking to begin…</div>
                  ) : (
                    logs.map((log, i) => (
                      <div
                        key={i}
                        className={
                          log.includes("Error") || log.includes("error")
                            ? "text-red-400"
                            : log.includes("started") || log.includes("Sent")
                            ? "text-emerald-400"
                            : log.includes("stopped")
                            ? "text-amber-400"
                            : ""
                        }
                      >
                        {log}
                      </div>
                    ))
                  )}
                </div>
              </CardContent>
            </Card>
          </div>
        </div>
      ) : null}
    </div>
  );
}
