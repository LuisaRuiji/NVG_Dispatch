import React, { useEffect, useState, useRef, useMemo } from "react";
import { MapContainer, TileLayer, Marker, Popup, Polyline } from "react-leaflet";
import "leaflet/dist/leaflet.css";
import L from "leaflet";
import { HubConnectionBuilder, LogLevel, HubConnection } from "@microsoft/signalr";
import { api, getAccessToken } from "@/lib/api";
import { LiveMapTripResponse, LocationUpdateBroadcastPayload } from "@/types/LiveMap";
import { AlertTriangle, Focus } from "lucide-react";
import StatusBadge from "@/components/StatusBadge";
import { Link } from "react-router-dom";

// Sleek SVG Pin Icon Generator for Professional Enterprise Fleet Maps
const createPin = (type: "pickup" | "pickupCompleted" | "dropoff" | "truck" | "truckDelayed") => {
  let bgColor = "linear-gradient(135deg, #10b981 0%, #059669 100%)";
  let shadowColor = "rgba(16, 185, 129, 0.45)";
  let svgContent = "";
  let pulseRing = "";

  if (type === "pickup") {
    bgColor = "linear-gradient(135deg, #10b981 0%, #059669 100%)";
    shadowColor = "rgba(16, 185, 129, 0.45)";
    svgContent = `<path fill="none" stroke="white" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" d="M20 7.5L12 3L4 7.5M20 7.5l-8 4.5m8-4.5v9l-8 4.5m0-9L4 7.5m8 4.5v9M4 7.5v9l8 4.5"/>`;
  } else if (type === "pickupCompleted") {
    bgColor = "linear-gradient(135deg, #059669 0%, #047857 100%)";
    shadowColor = "rgba(5, 150, 105, 0.45)";
    svgContent = `<path fill="none" stroke="white" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" d="M20 6L9 17l-5-5"/>`;
  } else if (type === "dropoff") {
    bgColor = "linear-gradient(135deg, #3b82f6 0%, #1d4ed8 100%)";
    shadowColor = "rgba(59, 130, 246, 0.45)";
    svgContent = `<path fill="none" stroke="white" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" d="M12 21s-6-5.333-6-10a6 6 0 0 1 12 0c0 4.667-6 10-6 10z"/><circle cx="12" cy="11" r="2.5" fill="white"/>`;
  } else if (type === "truck") {
    bgColor = "linear-gradient(135deg, #6366f1 0%, #4338ca 100%)";
    shadowColor = "rgba(99, 102, 241, 0.45)";
    pulseRing = `<div style="position: absolute; width: 42px; height: 42px; border-radius: 50%; border: 2px solid #6366f1; animation: ping 1.5s cubic-bezier(0, 0, 0.2, 1) infinite; opacity: 0.75;"></div>`;
    svgContent = `<rect x="1" y="3" width="15" height="13" rx="2" fill="none" stroke="white" stroke-width="2"/><path d="M16 8h4l3 3v5h-7V8z" fill="none" stroke="white" stroke-width="2"/><circle cx="5.5" cy="18.5" r="2" fill="white"/><circle cx="18.5" cy="18.5" r="2" fill="white"/>`;
  } else if (type === "truckDelayed") {
    bgColor = "linear-gradient(135deg, #ef4444 0%, #b91c1c 100%)";
    shadowColor = "rgba(239, 68, 68, 0.45)";
    pulseRing = `<div style="position: absolute; width: 42px; height: 42px; border-radius: 50%; border: 2px solid #ef4444; animation: ping 1.2s cubic-bezier(0, 0, 0.2, 1) infinite; opacity: 0.75;"></div>`;
    svgContent = `<rect x="1" y="3" width="15" height="13" rx="2" fill="none" stroke="white" stroke-width="2"/><path d="M16 8h4l3 3v5h-7V8z" fill="none" stroke="white" stroke-width="2"/><circle cx="5.5" cy="18.5" r="2" fill="white"/><circle cx="18.5" cy="18.5" r="2" fill="white"/>`;
  }

  const html = `
    <div style="position: relative; display: flex; align-items: center; justify-content: center; width: 42px; height: 42px;">
      ${pulseRing}
      <div style="
        background: ${bgColor};
        width: 34px;
        height: 34px;
        border-radius: 50% 50% 50% 4px;
        transform: rotate(-45deg);
        display: flex;
        align-items: center;
        justify-content: center;
        border: 2px solid #ffffff;
        box-shadow: 0 4px 10px ${shadowColor}, 0 2px 4px rgba(0,0,0,0.25);
      ">
        <svg viewBox="0 0 24 24" style="width: 18px; height: 18px; transform: rotate(45deg);">
          ${svgContent}
        </svg>
      </div>
    </div>
  `;

  return L.divIcon({
    className: "custom-leaflet-pin-icon",
    html: html,
    iconSize: [42, 42],
    iconAnchor: [21, 38],
    popupAnchor: [0, -34]
  });
};

const icons = {
  truck: createPin("truck"),
  truckDelayed: createPin("truckDelayed"),
  pickup: createPin("pickup"),
  pickupCompleted: createPin("pickupCompleted"),
  dropoff: createPin("dropoff")
};

type FilterMode = "ALL" | "TODAY" | "DELAYED";

type Props = {
  readOnly?: boolean;
};

const formatDateDisplay = (dateStr?: string | null) => {
  if (!dateStr) return null;
  const clean = dateStr.replace("Z", "");
  const [datePart, timePart] = clean.split("T");
  if (!datePart || !timePart) return null;
  const [y, m, d] = datePart.split("-").map(Number);
  const [h, min] = timePart.split(":").map(Number);
  if (isNaN(y) || isNaN(m) || isNaN(d) || isNaN(h) || isNaN(min)) return null;
  const obj = new Date(y, m - 1, d, h, min);
  return obj.toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: true
  });
};

const isToday = (dateStr?: string | null) => {
  if (!dateStr) return false;
  const clean = dateStr.replace("Z", "");
  const [datePart] = clean.split("T");
  if (!datePart) return false;
  const [y, m, d] = datePart.split("-").map(Number);
  if (isNaN(y) || isNaN(m) || isNaN(d)) return false;
  const now = new Date();
  return d === now.getDate() && (m - 1) === now.getMonth() && y === now.getFullYear();
};

// Helper to spread out stacked pins when multiple draft trips share identical center coordinates
const getJitteredPosition = (
  lat: number,
  lon: number,
  index: number,
  isDropoff = false
): [number, number] => {
  if (index === 0) return [lat, lon];
  const angle = (index * 60 + (isDropoff ? 30 : 0)) * (Math.PI / 180);
  const radius = 0.0035 * Math.ceil(index / 2); // ~350m radius offset ring
  return [lat + Math.sin(angle) * radius, lon + Math.cos(angle) * radius];
};

export default function LiveOperationsMap({ readOnly = false }: Props) {
  const [trips, setTrips] = useState<Record<string, LiveMapTripResponse>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [hubConnected, setHubConnected] = useState(false);
  const [filterMode, setFilterMode] = useState<FilterMode>("ALL");
  const [selectedTripId, setSelectedTripId] = useState<string | null>(null);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    fetchSnapshot();
    return () => {
      if (connectionRef.current) {
        connectionRef.current.stop();
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const fetchSnapshot = async () => {
    setLoading(true);
    try {
      const data = await api<LiveMapTripResponse[]>("/api/dispatch/live-map", { method: "GET" });
      const dict: Record<string, LiveMapTripResponse> = {};
      data.forEach((trip) => {
        dict[trip.tripId] = trip;
      });
      setTrips(dict);
      setError(null);
      connectSignalR();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to load live map.");
    } finally {
      setLoading(false);
    }
  };

  const connectSignalR = async () => {
    if (connectionRef.current) {
      if (connectionRef.current.state === "Connected") {
        setHubConnected(true);
      }
      return;
    }
    const baseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() || "";
    const hubUrl = baseUrl ? `${baseUrl.replace(/\/$/, "")}/hubs/dispatch-location` : "/hubs/dispatch-location";

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => getAccessToken() || localStorage.getItem("nvg_token") || ""
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on("ReceiveLocationUpdate", (payload: LocationUpdateBroadcastPayload) => {
      setTrips((prev) => {
        const trip = prev[payload.tripId];
        if (!trip) return prev;

        return {
          ...prev,
          [payload.tripId]: {
            ...trip,
            lastLatitude: payload.latitude,
            lastLongitude: payload.longitude,
            lastLocationAt: payload.recordedAt,
            currentTripStatus: payload.tripStatus,
            _speed: payload.speedKph,
            _accuracy: payload.accuracyMeters
          } as LiveMapTripResponse & { _speed?: number | null; _accuracy?: number | null }
        };
      });
    });

    connection.onreconnected(() => {
      setHubConnected(true);
      fetchSnapshot();
    });

    connection.onreconnecting(() => {
      setHubConnected(false);
    });

    connection.onclose(() => {
      setHubConnected(false);
    });

    try {
      await connection.start();
      setHubConnected(true);
    } catch (err) {
      console.error("SignalR Connection Error: ", err);
      setHubConnected(false);
    }
  };

  const filteredTrips = useMemo(() => {
    const list = Object.values(trips);
    if (filterMode === "DELAYED") {
      return list.filter((t) => t.delayFlag);
    }
    if (filterMode === "TODAY") {
      return list.filter(
        (t) => isToday(t.pickupScheduledAt) || isToday(t.dropoffScheduledAt) || isToday(t.lastLocationAt)
      );
    }
    return list;
  }, [trips, filterMode]);

  const center: [number, number] = [7.1907, 125.4553]; // Davao City default

  const initialCenter = useMemo(() => {
    const validTrip = filteredTrips.find((t) => t.lastLatitude && t.lastLongitude);
    if (validTrip && validTrip.lastLatitude && validTrip.lastLongitude) {
      return [validTrip.lastLatitude, validTrip.lastLongitude] as [number, number];
    }
    return center;
  }, [filteredTrips]);

  if (loading && Object.keys(trips).length === 0) {
    return (
      <div className="flex h-full items-center justify-center p-8">
        <span className="animate-pulse text-muted-foreground">Loading Live Map...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex h-full flex-col items-center justify-center space-y-4 p-8">
        <AlertTriangle className="h-12 w-12 text-destructive" />
        <h3 className="text-lg font-semibold">Failed to load map</h3>
        <p className="text-sm text-muted-foreground">{error}</p>
        <button onClick={fetchSnapshot} className="px-4 py-2 bg-primary text-primary-foreground rounded-lg">
          Retry
        </button>
      </div>
    );
  }

  const isStale = (lastUpdatedAt: string | null) => {
    if (!lastUpdatedAt) return true;
    const diffMs = Date.now() - new Date(lastUpdatedAt).getTime();
    return diffMs > 5 * 60 * 1000;
  };

  const totalDelayed = Object.values(trips).filter((t) => t.delayFlag).length;

  return (
    <div className="flex flex-col h-full w-full space-y-3">
      {/* Header Filter & Live Status Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-3 surface-card p-3">
        <div className="flex items-center gap-2">
          <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Filter Map:</span>
          <div className="flex items-center gap-1 rounded-lg border border-border bg-muted/40 p-1">
            <button
              onClick={() => setFilterMode("ALL")}
              className={`px-3 py-1.5 text-xs font-semibold rounded-md transition-colors ${filterMode === "ALL"
                  ? "bg-primary text-primary-foreground shadow-xs"
                  : "text-muted-foreground hover:text-foreground"
                }`}
            >
              All Active ({Object.keys(trips).length})
            </button>
            <button
              onClick={() => setFilterMode("TODAY")}
              className={`px-3 py-1.5 text-xs font-semibold rounded-md transition-colors ${filterMode === "TODAY"
                  ? "bg-primary text-primary-foreground shadow-xs"
                  : "text-muted-foreground hover:text-foreground"
                }`}
            >
              Today
            </button>
            <button
              onClick={() => setFilterMode("DELAYED")}
              className={`px-3 py-1.5 text-xs font-semibold rounded-md transition-colors ${filterMode === "DELAYED"
                  ? "bg-rose-600 text-white shadow-xs"
                  : "text-muted-foreground hover:text-foreground"
                }`}
            >
              Delayed ({totalDelayed})
            </button>
          </div>

          {selectedTripId ? (
            <button
              onClick={() => setSelectedTripId(null)}
              className="rounded-lg border border-slate-200 bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-200 flex items-center gap-1.5"
            >
              <Focus className="h-3.5 w-3.5" /> Clear Selection Focus
            </button>
          ) : null}
        </div>

        {/* Signal Status */}
        <div className="flex items-center gap-2">
          <div
            className={`px-3 py-1.5 rounded-full text-xs font-semibold shadow-xs flex items-center gap-2 ${hubConnected
                ? "bg-emerald-100 text-emerald-800 border border-emerald-200"
                : "bg-amber-100 text-amber-800 border border-amber-200 animate-pulse"
              }`}
          >
            <span className={`h-2 w-2 rounded-full ${hubConnected ? "bg-emerald-500" : "bg-amber-500"}`} />
            {hubConnected ? "LIVE" : "RECONNECTING..."}
          </div>
          {totalDelayed > 0 && (
            <div className="px-3 py-1.5 rounded-full bg-red-100 text-red-800 border border-red-200 text-xs font-semibold shadow-xs flex items-center gap-2">
              <AlertTriangle className="h-3.5 w-3.5" />
              {totalDelayed} Delayed
            </div>
          )}
        </div>
      </div>

      {/* Map Display */}
      <div className="relative flex-1 min-h-[520px] w-full rounded-xl overflow-hidden border border-border shadow-xs">
        <MapContainer center={initialCenter} zoom={11} className="h-full w-full">
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />

          {filteredTrips.map((trip, idx) => {
            const tExtended = trip as LiveMapTripResponse & { _speed?: number | null; _accuracy?: number | null };
            const hasLocation = trip.lastLatitude !== null && trip.lastLongitude !== null;
            const stale = isStale(trip.lastLocationAt);
            const isSelected = selectedTripId === trip.tripId;

            const pickupPos = trip.pickupLatitude && trip.pickupLongitude
              ? getJitteredPosition(trip.pickupLatitude, trip.pickupLongitude, idx, false)
              : null;

            const dropoffPos = trip.dropoffLatitude && trip.dropoffLongitude
              ? getJitteredPosition(trip.dropoffLatitude, trip.dropoffLongitude, idx, true)
              : null;

            // Build path points if selected
            const routePositions: [number, number][] = [];
            if (isSelected) {
              if (pickupPos) routePositions.push(pickupPos);
              if (hasLocation) routePositions.push([trip.lastLatitude!, trip.lastLongitude!]);
              if (dropoffPos) routePositions.push(dropoffPos);
            }

            return (
              <React.Fragment key={trip.tripId}>
                {/* Render Pickup/Dropoff for all active and draft trips */}
                {pickupPos && (() => {
                  const isPickupCompleted = ["LOADED", "ENROUTE_DROPOFF", "AT_DROPOFF", "DELIVERED", "CLOSED"].includes(trip.currentTripStatus.toUpperCase());
                  return (
                    <Marker
                      position={pickupPos}
                      icon={isPickupCompleted ? icons.pickupCompleted : icons.pickup}
                      eventHandlers={{
                        click: () => setSelectedTripId(trip.tripId)
                      }}
                    >
                      <Popup>
                        <div className="text-sm font-semibold mb-1">
                          {isPickupCompleted ? "Pickup Completed" : "Pickup Location"}
                        </div>
                        <div className="text-xs text-muted-foreground">{trip.pickupLocation || "Unknown"}</div>
                        {trip.pickupScheduledAt && (
                          <div className="text-[11px] font-medium text-slate-700 mt-1">
                            Scheduled: {formatDateDisplay(trip.pickupScheduledAt)}
                          </div>
                        )}
                        <div className="text-[10px] text-muted-foreground mt-1 flex items-center justify-between border-t pt-1">
                          <span>Trip: {trip.tripId.substring(0, 8)}</span>
                          <StatusBadge status={trip.currentTripStatus} />
                        </div>
                      </Popup>
                    </Marker>
                  );
                })()}

                {dropoffPos && (
                  <Marker
                    position={dropoffPos}
                    icon={icons.dropoff}
                    eventHandlers={{
                      click: () => setSelectedTripId(trip.tripId)
                    }}
                  >
                    <Popup>
                      <div className="text-sm font-semibold mb-1">Drop-off Destination</div>
                      <div className="text-xs text-muted-foreground">{trip.dropoffLocation || "Unknown"}</div>
                      {trip.dropoffScheduledAt && (
                        <div className="text-[11px] font-medium text-slate-700 mt-1">
                          Scheduled: {formatDateDisplay(trip.dropoffScheduledAt)}
                        </div>
                      )}
                      <div className="text-[10px] text-muted-foreground mt-1 flex items-center justify-between border-t pt-1">
                        <span>Trip: {trip.tripId.substring(0, 8)}</span>
                        <StatusBadge status={trip.currentTripStatus} />
                      </div>
                    </Popup>
                  </Marker>
                )}

                {/* Render connecting route line when selected */}
                {isSelected && routePositions.length > 1 && (
                  <Polyline positions={routePositions} pathOptions={{ color: "#3b82f6", weight: 3, dashArray: "6, 6" }} />
                )}

                {/* Truck Marker (Always visible for active fleet) */}
                {hasLocation && (
                  <Marker
                    position={[trip.lastLatitude!, trip.lastLongitude!]}
                    icon={trip.delayFlag ? icons.truckDelayed : icons.truck}
                    eventHandlers={{
                      click: () => setSelectedTripId(trip.tripId)
                    }}
                  >
                    <Popup className="min-w-[220px]">
                      <div className="space-y-3 p-1">
                        <div className="flex items-center justify-between border-b pb-2">
                          <h4 className="font-bold text-sm">{trip.plateNumber}</h4>
                          <StatusBadge status={trip.currentTripStatus} />
                        </div>

                        <div className="space-y-1.5 text-xs">
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">Driver:</span>
                            <span className="font-medium">{trip.driverName}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-muted-foreground">Updated:</span>
                            <span className={`font-medium ${stale ? "text-red-500 font-bold" : ""}`}>
                              {trip.lastLocationAt ? new Date(trip.lastLocationAt).toLocaleTimeString() : "Unknown"}
                            </span>
                          </div>
                          {tExtended._speed != null && (
                            <div className="flex justify-between">
                              <span className="text-muted-foreground">Speed:</span>
                              <span className="font-medium">{Math.round(tExtended._speed)} km/h</span>
                            </div>
                          )}
                          {tExtended._accuracy != null && (
                            <div className="flex justify-between">
                              <span className="text-muted-foreground">Accuracy:</span>
                              <span className="font-medium">±{Math.round(tExtended._accuracy)}m</span>
                            </div>
                          )}
                          {stale && (
                            <div className="mt-2 p-1.5 bg-red-50 text-red-700 border border-red-200 rounded text-[10px] flex items-start gap-1">
                              <AlertTriangle className="h-3 w-3 shrink-0 mt-0.5" />
                              <span>No update for 5+ mins. Location may be outdated.</span>
                            </div>
                          )}
                        </div>

                        <div className="pt-2 border-t mt-2 flex gap-2">
                          <button
                            onClick={() => setSelectedTripId(trip.tripId)}
                            className="flex-1 text-center py-1.5 bg-slate-100 text-slate-700 hover:bg-slate-200 transition-colors rounded text-xs font-semibold"
                          >
                            Focus Trip Pins
                          </button>
                          {!readOnly && (
                            <Link
                              to={`/dispatch/trips/${trip.tripId}`}
                              className="flex-1 text-center py-1.5 bg-primary/10 text-primary hover:bg-primary/20 transition-colors rounded text-xs font-semibold"
                            >
                              Details
                            </Link>
                          )}
                        </div>
                      </div>
                    </Popup>
                  </Marker>
                )}
              </React.Fragment>
            );
          })}
        </MapContainer>
      </div>
    </div>
  );
}
