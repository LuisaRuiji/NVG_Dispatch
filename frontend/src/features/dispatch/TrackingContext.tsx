import { createContext, useContext, useEffect, useRef, useState, ReactNode } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/lib/useToast";
import { useDispatchHub } from "@/hooks/useDispatchHub";

export interface LiveMapTripDetail {
  tripId: string;
  dispatchTruckId: string;
  plateNumber: string;
  dispatchDriverId: string;
  driverName: string;
  currentTripStatus: string;
  lastLatitude: number | null;
  lastLongitude: number | null;
  lastLocationAt: string | null;
  pickupLocation: string | null;
  pickupLatitude: number | null;
  pickupLongitude: number | null;
  dropoffLocation: string | null;
  dropoffLatitude: number | null;
  dropoffLongitude: number | null;
  delayFlag: boolean;
}

export interface CurrentCoords {
  latitude: number;
  longitude: number;
  accuracy: number;
  speed: number | null;
  heading: number | null;
}

interface TrackingContextType {
  trip: LiveMapTripDetail | null;
  loading: boolean;
  error: string | null;
  trackingActive: boolean;
  currentCoords: CurrentCoords | null;
  gpsWarning: string | null;
  networkError: string | null;
  logs: string[];
  fetchActiveTrip: () => Promise<void>;
  startWatcher: () => void;
  stopWatcher: () => void;
}

const TrackingContext = createContext<TrackingContextType | undefined>(undefined);

export function TrackingProvider({ children }: { children: ReactNode }) {
  const [trip, setTrip] = useState<LiveMapTripDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [trackingActive, setTrackingActive] = useState(false);
  const [currentCoords, setCurrentCoords] = useState<CurrentCoords | null>(null);
  const [gpsWarning, setGpsWarning] = useState<string | null>(null);
  const [networkError, setNetworkError] = useState<string | null>(null);
  const [logs, setLogs] = useState<string[]>([]);
  const { show } = useToast();

  const watcherId = useRef<number | null>(null);
  const lastSentTime = useRef<number>(0);
  const lastSentCoords = useRef<{ latitude: number; longitude: number } | null>(null);
  const isSending = useRef<boolean>(false);
  const tripRef = useRef<LiveMapTripDetail | null>(null);

  // Sync trip ref to state for access inside location callback
  useEffect(() => {
    tripRef.current = trip;
  }, [trip]);

  useDispatchHub({
    onTripStatusChanged: (e) => {
      if (trip && e.tripId === trip.tripId) {
        setTrip(prev => prev ? { ...prev, currentTripStatus: e.newStatus } : null);
      }
    }
  });

  const fetchActiveTrip = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await api<LiveMapTripDetail>("/api/driver/my-route-map", { method: "GET" });
      setTrip(data);
      const finalStatuses = ["DELIVERED", "CLOSED", "CANCELLED"];
      if (finalStatuses.includes(data.currentTripStatus.toUpperCase())) {
        setError("Your assigned trip is already completed or cancelled.");
      }
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "No active trip found for your profile today.";
      setError(message);
    } finally {
      setLoading(false);
    }
  };

  const getDistanceMeters = (lat1: number, lon1: number, lat2: number, lon2: number) => {
    const R = 6371000;
    const phi1 = (lat1 * Math.PI) / 180;
    const phi2 = (lat2 * Math.PI) / 180;
    const deltaPhi = ((lat2 - lat1) * Math.PI) / 180;
    const deltaLambda = ((lon2 - lon1) * Math.PI) / 180;
    const a =
      Math.sin(deltaPhi / 2) * Math.sin(deltaPhi / 2) +
      Math.cos(phi1) * Math.cos(phi2) * Math.sin(deltaLambda / 2) * Math.sin(deltaLambda / 2);
    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    return R * c;
  };

  const handleLocationUpdate = async (position: GeolocationPosition) => {
    const { latitude, longitude, accuracy, speed, heading } = position.coords;
    setCurrentCoords({ latitude, longitude, accuracy, speed, heading });

    if (accuracy > 100) {
      setGpsWarning(`Poor GPS accuracy (${Math.round(accuracy)}m). Try moving to an open area.`);
    } else {
      setGpsWarning(null);
    }

    const now = Date.now();
    const timeDiff = now - lastSentTime.current;
    let shouldSend = false;
    if (lastSentTime.current === 0) {
      shouldSend = true;
    } else if (timeDiff >= 12000) {
      shouldSend = true;
    } else if (lastSentCoords.current) {
      const distance = getDistanceMeters(
        lastSentCoords.current.latitude,
        lastSentCoords.current.longitude,
        latitude,
        longitude
      );
      if (distance >= 50) shouldSend = true;
    }

    const currentTrip = tripRef.current;
    if (shouldSend && currentTrip) {
      if (isSending.current) return;
      isSending.current = true;
      const payload = {
        TripId: currentTrip.tripId,
        DispatchDriverId: currentTrip.dispatchDriverId,
        DispatchTruckId: currentTrip.dispatchTruckId,
        Latitude: latitude,
        Longitude: longitude,
        AccuracyMeters: accuracy,
        SpeedKph: speed !== null ? speed * 3.6 : null,
        Heading: heading,
        RecordedAt: new Date().toISOString(),
        Source: 0 // LocationUpdateSource.DriverApp
      };
      try {
        await api("/api/driver/location", {
          method: "POST",
          body: JSON.stringify(payload)
        });
        setNetworkError(null);
        lastSentTime.current = Date.now();
        lastSentCoords.current = { latitude, longitude };
        setLogs(prev => [
          `[${new Date().toLocaleTimeString()}] Sent: Lat ${latitude.toFixed(6)}, Lon ${longitude.toFixed(6)} (±${Math.round(accuracy)}m)`,
          ...prev.slice(0, 49)
        ]);
      } catch (err: unknown) {
        const message = err instanceof Error ? err.message : "Connection error.";
        setNetworkError("Connection offline. Retrying location update...");
        setLogs(prev => [
          `[${new Date().toLocaleTimeString()}] Error: ${message}`,
          ...prev.slice(0, 49)
        ]);
      } finally {
        isSending.current = false;
      }
    }
  };

  const startWatcher = async () => {
    if (!navigator.geolocation) {
      show("Geolocation is not supported by your browser.", "error");
      return;
    }
    if (watcherId.current !== null) return; // prevent duplicate watchers
    setLogs(prev => [`[${new Date().toLocaleTimeString()}] Requesting GPS authorization...`, ...prev]);

    try {
      const currentTrip = tripRef.current;
      if (currentTrip) {
        let initLat: number | null = null;
        let initLon: number | null = null;
        try {
          const pos = await new Promise<GeolocationPosition>((resolve, reject) =>
            navigator.geolocation.getCurrentPosition(resolve, reject, { timeout: 5000 })
          );
          initLat = pos.coords.latitude;
          initLon = pos.coords.longitude;
        } catch {
          // silently continue
        }

        await api(`/api/driver/trips/${currentTrip.tripId}/start-tracking`, {
          method: "POST",
          body: JSON.stringify({ Latitude: initLat, Longitude: initLon })
        });
        setTrackingActive(true);
        const initMsg = initLat
          ? `[${new Date().toLocaleTimeString()}] Initial fix: (${initLat.toFixed(6)}, ${initLon!.toFixed(6)})`
          : `[${new Date().toLocaleTimeString()}] Initial fix: unavailable`;
        setLogs(prev => [
          `[${new Date().toLocaleTimeString()}] Live tracking started.`,
          initMsg,
          ...prev
        ]);
        show("GPS tracking started successfully.", "success");
      }
    } catch (err: any) {
      setLogs(prev => [`[${new Date().toLocaleTimeString()}] Error starting backend session: ${err.message}`, ...prev]);
      show(err.message, "error");
    }

    watcherId.current = navigator.geolocation.watchPosition(
      (pos) => {
        if (!trackingActive) setTrackingActive(true);
        if (logs.length === 0 || !logs[0].includes("Live tracking started")) {
          setLogs(prev => [`[${new Date().toLocaleTimeString()}] Live tracking started.`, ...prev]);
        }
        setLogs(prev => [`[${new Date().toLocaleTimeString()}] Initial fix: (${pos.coords.latitude.toFixed(6)}, ${pos.coords.longitude.toFixed(6)})`, ...prev]);
        handleLocationUpdate(pos);
      },
      (err) => {
        setLogs(prev => [`[${new Date().toLocaleTimeString()}] GPS Error: ${err.message}`, ...prev]);
        setGpsWarning(`GPS Error: ${err.message}`);
      },
      {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 0
      }
    );
  };

  const stopWatcher = async () => {
    if (watcherId.current !== null) {
      navigator.geolocation.clearWatch(watcherId.current);
      watcherId.current = null;
    }
    setTrackingActive(false);
    setCurrentCoords(null);
    setGpsWarning(null);
    setNetworkError(null);
    lastSentTime.current = 0;
    lastSentCoords.current = null;

    const currentTrip = tripRef.current;
    if (currentTrip) {
      try {
        const endLat = currentCoords?.latitude ?? null;
        const endLon = currentCoords?.longitude ?? null;
        await api(`/api/driver/trips/${currentTrip.tripId}/stop-tracking`, { 
          method: "POST",
          body: JSON.stringify({ Latitude: endLat, Longitude: endLon })
        });
        setLogs(prev => [`[${new Date().toLocaleTimeString()}] Tracking stopped by driver.`, ...prev]);
        show("GPS tracking stopped.", "success");
      } catch (err: any) {
        setLogs(prev => [`[${new Date().toLocaleTimeString()}] Error stopping tracking session: ${err.message}`, ...prev]);
        show(err.message, "error");
      }
    }
  };

  return (
    <TrackingContext.Provider
      value={{
        trip,
        loading,
        error,
        trackingActive,
        currentCoords,
        gpsWarning,
        networkError,
        logs,
        fetchActiveTrip,
        startWatcher,
        stopWatcher
      }}
    >
      {children}
    </TrackingContext.Provider>
  );
}

export function useTracking() {
  const context = useContext(TrackingContext);
  if (context === undefined) {
    throw new Error("useTracking must be used within a TrackingProvider");
  }
  return context;
}
