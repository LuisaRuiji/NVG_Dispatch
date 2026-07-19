import React, { useEffect, useState, useRef, useMemo } from "react";
import { MapContainer, TileLayer, Marker, Popup } from "react-leaflet";
import "leaflet/dist/leaflet.css";
import L from "leaflet";
import { HubConnectionBuilder, LogLevel, HubConnection } from "@microsoft/signalr";
import { api } from "@/lib/api";
import { LiveMapTripResponse, LocationUpdateBroadcastPayload } from "@/types/LiveMap";
import { AlertTriangle } from "lucide-react";
import StatusBadge from "@/components/StatusBadge";
import { Link } from "react-router-dom";

// Leaflet icon setup
const createIcon = (color: string, emoji: string) =>
  L.divIcon({
    className: "custom-leaflet-icon",
    html: `<div style="background-color: ${color}; width: 32px; height: 32px; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; box-shadow: 0 2px 4px rgba(0,0,0,0.3); font-size: 16px;">${emoji}</div>`,
    iconSize: [32, 32],
    iconAnchor: [16, 16],
    popupAnchor: [0, -16]
  });

const icons = {
  truck: createIcon("#3b82f6", "🚚"), // Blue
  truckDelayed: createIcon("#ef4444", "🚚"), // Red
  pickup: createIcon("#10b981", "📦"), // Emerald
  dropoff: createIcon("#8b5cf6", "📍") // Purple
};

type Props = {
  readOnly?: boolean;
};

export default function LiveOperationsMap({ readOnly = false }: Props) {
  const [trips, setTrips] = useState<Record<string, LiveMapTripResponse>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [hubConnected, setHubConnected] = useState(false);
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
    if (connectionRef.current) return;
    const token = localStorage.getItem("nvg_token");
    if (!token) return;

    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/dispatch-location", {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // Retry logic
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on("ReceiveLocationUpdate", (payload: LocationUpdateBroadcastPayload) => {
      setTrips((prev) => {
        const trip = prev[payload.tripId];
        if (!trip) return prev; // If trip not in snapshot, we might want to fetch it, but ignoring is fine for now

        return {
          ...prev,
          [payload.tripId]: {
            ...trip,
            lastLatitude: payload.latitude,
            lastLongitude: payload.longitude,
            lastLocationAt: payload.recordedAt,
            currentTripStatus: payload.tripStatus,
            // we could store speed/accuracy in the trip state if we expand the interface, 
            // but for now we'll just rely on what we have or extend the type.
            // Let's extend it dynamically here for the popup
            _speed: payload.speedKph,
            _accuracy: payload.accuracyMeters
          } as LiveMapTripResponse & { _speed?: number | null; _accuracy?: number | null }
        };
      });
    });

    connection.onreconnected(() => {
      setHubConnected(true);
      fetchSnapshot(); // Refresh state after reconnect
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

  const center: [number, number] = [7.1907, 125.4553]; // Davao City as fallback default
  
  // Try to find the first valid location to center the map
  const initialCenter = useMemo(() => {
    const validTrip = Object.values(trips).find(t => t.lastLatitude && t.lastLongitude);
    if (validTrip && validTrip.lastLatitude && validTrip.lastLongitude) {
        return [validTrip.lastLatitude, validTrip.lastLongitude] as [number, number];
    }
    return center;
  }, [trips]);

  if (loading && Object.keys(trips).length === 0) {
    return <div className="flex h-full items-center justify-center p-8"><span className="animate-pulse text-muted-foreground">Loading Live Map...</span></div>;
  }

  if (error) {
    return (
      <div className="flex h-full flex-col items-center justify-center space-y-4 p-8">
        <AlertTriangle className="h-12 w-12 text-destructive" />
        <h3 className="text-lg font-semibold">Failed to load map</h3>
        <p className="text-sm text-muted-foreground">{error}</p>
        <button onClick={fetchSnapshot} className="px-4 py-2 bg-primary text-primary-foreground rounded-lg">Retry</button>
      </div>
    );
  }

  const isStale = (lastUpdatedAt: string | null) => {
    if (!lastUpdatedAt) return true;
    const diffMs = Date.now() - new Date(lastUpdatedAt).getTime();
    return diffMs > 5 * 60 * 1000; // 5 minutes
  };

  return (
    <div className="relative h-full w-full rounded-xl overflow-hidden border border-border shadow-sm">
      {/* Status Overlay */}
      <div className="absolute top-4 right-4 z-[400] flex flex-col gap-2">
        <div className={`px-3 py-1.5 rounded-full text-xs font-semibold shadow-sm flex items-center gap-2 ${hubConnected ? 'bg-emerald-100 text-emerald-800 border border-emerald-200' : 'bg-amber-100 text-amber-800 border border-amber-200 animate-pulse'}`}>
           <span className={`h-2 w-2 rounded-full ${hubConnected ? 'bg-emerald-500' : 'bg-amber-500'}`} />
           {hubConnected ? "LIVE" : "RECONNECTING..."}
        </div>
        {Object.values(trips).filter(t => t.delayFlag).length > 0 && (
            <div className="px-3 py-1.5 rounded-full bg-red-100 text-red-800 border border-red-200 text-xs font-semibold shadow-sm flex items-center gap-2">
                <AlertTriangle className="h-3.5 w-3.5" />
                {Object.values(trips).filter(t => t.delayFlag).length} Delayed
            </div>
        )}
      </div>

      <MapContainer center={initialCenter} zoom={11} className="h-full w-full">
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />

        {Object.values(trips).map((trip) => {
          const tExtended = trip as LiveMapTripResponse & { _speed?: number | null; _accuracy?: number | null };
          const hasLocation = trip.lastLatitude !== null && trip.lastLongitude !== null;
          const stale = isStale(trip.lastLocationAt);
          
          return (
            <React.Fragment key={trip.tripId}>
              {/* Pickup Marker */}
              {trip.pickupLatitude && trip.pickupLongitude && (
                 <Marker position={[trip.pickupLatitude, trip.pickupLongitude]} icon={icons.pickup}>
                    <Popup>
                        <div className="text-sm font-semibold mb-1">Pickup</div>
                        <div className="text-xs text-muted-foreground">{trip.pickupLocation || "Unknown"}</div>
                        <div className="text-[10px] text-muted-foreground mt-1">Trip: {trip.tripId.substring(0,8)}</div>
                    </Popup>
                 </Marker>
              )}

              {/* Dropoff Marker */}
              {trip.dropoffLatitude && trip.dropoffLongitude && (
                 <Marker position={[trip.dropoffLatitude, trip.dropoffLongitude]} icon={icons.dropoff}>
                    <Popup>
                        <div className="text-sm font-semibold mb-1">Drop-off</div>
                        <div className="text-xs text-muted-foreground">{trip.dropoffLocation || "Unknown"}</div>
                        <div className="text-[10px] text-muted-foreground mt-1">Trip: {trip.tripId.substring(0,8)}</div>
                    </Popup>
                 </Marker>
              )}

              {/* Truck Marker */}
              {hasLocation && (
                <Marker 
                    position={[trip.lastLatitude!, trip.lastLongitude!]} 
                    icon={trip.delayFlag ? icons.truckDelayed : icons.truck}
                >
                  <Popup className="min-w-[200px]">
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
                            <span className={`font-medium ${stale ? 'text-red-500 font-bold' : ''}`}>
                                {trip.lastLocationAt ? new Date(trip.lastLocationAt).toLocaleTimeString() : 'Unknown'}
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

                      {!readOnly && (
                          <div className="pt-2 border-t mt-2">
                             <Link 
                                to={`/dispatch/trips/${trip.tripId}`} 
                                className="block w-full text-center py-1.5 bg-primary/10 text-primary hover:bg-primary/20 transition-colors rounded text-xs font-semibold"
                             >
                                View Trip Details
                             </Link>
                          </div>
                      )}
                    </div>
                  </Popup>
                </Marker>
              )}
            </React.Fragment>
          );
        })}
      </MapContainer>
    </div>
  );
}
