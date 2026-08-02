import { createContext, useContext, useEffect, useRef, useState, ReactNode } from "react";
import { api, apiOptional } from "@/lib/api";
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

export interface LiveTripEta {
  tripId: string;
  destinationType: "PICKUP" | "DROPOFF";
  destinationLocation: string;
  remainingDistanceKm: number;
  estimatedTravelMinutes: number;
  estimatedArrivalAt: string;
  calculatedAt: string;
}

type LocationUpdateResponse = {
  eta?: LiveTripEta | null;
};

interface TrackingContextType {
  trip: LiveMapTripDetail | null;
  loading: boolean;
  error: string | null;
  trackingActive: boolean;
  trackingStarting: boolean;
  eta: LiveTripEta | null;
  currentCoords: CurrentCoords | null;
  gpsWarning: string | null;
  networkError: string | null;
  logs: string[];
  fetchActiveTrip: () => Promise<void>;
  startWatcher: () => Promise<void>;
  stopWatcher: () => void;
}

const TrackingContext = createContext<TrackingContextType | undefined>(undefined);

const automaticTrackingStatuses = new Set([
  "ENROUTEPICKUP",
  "ATPICKUP",
  "LOADED",
  "ENROUTEDROPOFF",
  "ATDROPOFF",
  "ONHOLD",
  "FAILEDATTEMPT"
]);

function normalizeStatus(value: string) {
  return value.replace(/_/g, "").toUpperCase();
}

export function TrackingProvider({ children, enabled = false }: { children: ReactNode; enabled?: boolean }) {
  const [trip, setTrip] = useState<LiveMapTripDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [trackingActive, setTrackingActive] = useState(false);
  const [trackingStarting, setTrackingStarting] = useState(false);
  const [eta, setEta] = useState<LiveTripEta | null>(null);
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
  const automaticStartAttemptedTripId = useRef<string | null>(null);

  // Sync trip ref to state for access inside location callback
  useEffect(() => {
    tripRef.current = trip;
  }, [trip]);

  const fetchEta = async (tripId: string) => {
    try {
      setEta(await apiOptional<LiveTripEta>(`/api/driver/trips/${tripId}/eta`, { method: "GET" }));
    } catch {
      setEta(null);
    }
  };

  useDispatchHub({
    onTripStatusChanged: (e) => {
      if (trip && e.tripId === trip.tripId) {
        setTrip(prev => prev ? { ...prev, currentTripStatus: e.newStatus } : null);
        setEta(null);
        void fetchEta(e.tripId);
      }
    }
  });

  const fetchActiveTrip = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await api<LiveMapTripDetail>("/api/driver/my-route-map", { method: "GET" });
      setTrip(data);
      void fetchEta(data.tripId);
      const finalStatuses = ["DELIVERED", "CLOSED", "CANCELLED"];
      if (finalStatuses.includes(data.currentTripStatus.toUpperCase())) {
        setError("Your assigned trip is already completed or cancelled.");
      }
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "No active trip found for your profile today.";
      setError(message);
      setEta(null);
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
    } else if (timeDiff >= 60000) {
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
        const response = await api<LocationUpdateResponse>("/api/driver/location", {
          method: "POST",
          body: JSON.stringify(payload)
        });
        setEta(response.eta ?? null);
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
    if (watcherId.current !== null || trackingStarting) return;
    const currentTrip = tripRef.current;
    if (!currentTrip || !automaticTrackingStatuses.has(normalizeStatus(currentTrip.currentTripStatus))) return;

    setTrackingStarting(true);
    setGpsWarning(null);
    setLogs(prev => [`[${new Date().toLocaleTimeString()}] Requesting GPS authorization...`, ...prev]);

    try {
      const initialPosition = await new Promise<GeolocationPosition>((resolve, reject) =>
        navigator.geolocation.getCurrentPosition(resolve, reject, {
          enableHighAccuracy: true,
          timeout: 10000,
          maximumAge: 0
        })
      );

      await api(`/api/driver/trips/${currentTrip.tripId}/start-tracking`, {
        method: "POST",
        body: JSON.stringify({
          Latitude: initialPosition.coords.latitude,
          Longitude: initialPosition.coords.longitude
        })
      });

      setCurrentCoords({
        latitude: initialPosition.coords.latitude,
        longitude: initialPosition.coords.longitude,
        accuracy: initialPosition.coords.accuracy,
        speed: initialPosition.coords.speed,
        heading: initialPosition.coords.heading
      });
      setTrackingActive(true);
      setLogs(prev => [
        `[${new Date().toLocaleTimeString()}] Live tracking started automatically.`,
        `[${new Date().toLocaleTimeString()}] Initial fix: (${initialPosition.coords.latitude.toFixed(6)}, ${initialPosition.coords.longitude.toFixed(6)})`,
        ...prev
      ]);

      watcherId.current = navigator.geolocation.watchPosition(
        (position) => {
          setTrackingActive(true);
          void handleLocationUpdate(position);
        },
        (error) => {
          setLogs(prev => [`[${new Date().toLocaleTimeString()}] GPS Error: ${error.message}`, ...prev]);
          setGpsWarning(error.code === error.PERMISSION_DENIED
            ? "Location permission is required during an active trip. Allow location access, then retry."
            : `GPS is temporarily unavailable: ${error.message}`);
          if (error.code === error.PERMISSION_DENIED && watcherId.current !== null) {
            navigator.geolocation.clearWatch(watcherId.current);
            watcherId.current = null;
            setTrackingActive(false);
          }
        },
        { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 }
      );

      await handleLocationUpdate(initialPosition);
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : "Location access was not granted.";
      setTrackingActive(false);
      setGpsWarning(message.toLowerCase().includes("denied")
        ? "Location permission is required during an active trip. Allow location access, then retry."
        : `Automatic location sharing could not start: ${message}`);
      setLogs(prev => [`[${new Date().toLocaleTimeString()}] Automatic tracking did not start: ${message}`, ...prev]);
    } finally {
      setTrackingStarting(false);
    }
  };

  const stopWatcher = async () => {
    const wasTracking = watcherId.current !== null;
    if (watcherId.current !== null) {
      navigator.geolocation.clearWatch(watcherId.current);
      watcherId.current = null;
    }
    setTrackingActive(false);
    setCurrentCoords(null);
    setEta(null);
    setGpsWarning(null);
    setNetworkError(null);
    lastSentTime.current = 0;
    lastSentCoords.current = null;

    const currentTrip = tripRef.current;
    if (wasTracking && currentTrip) {
      try {
        const endLat = currentCoords?.latitude ?? null;
        const endLon = currentCoords?.longitude ?? null;
        await api(`/api/driver/trips/${currentTrip.tripId}/stop-tracking`, { 
          method: "POST",
          body: JSON.stringify({ Latitude: endLat, Longitude: endLon })
        });
        setLogs(prev => [`[${new Date().toLocaleTimeString()}] Tracking stopped with the trip lifecycle.`, ...prev]);
      } catch (error: unknown) {
        const message = error instanceof Error ? error.message : "Unable to stop the tracking session.";
        setLogs(prev => [`[${new Date().toLocaleTimeString()}] Error stopping tracking session: ${message}`, ...prev]);
      }
    }
  };

  useEffect(() => {
    if (!enabled) return;
    void fetchActiveTrip();
  }, [enabled]);

  useEffect(() => {
    if (!enabled || !trip) return;
    const shouldTrack = automaticTrackingStatuses.has(normalizeStatus(trip.currentTripStatus));

    if (shouldTrack && watcherId.current === null && automaticStartAttemptedTripId.current !== trip.tripId) {
      automaticStartAttemptedTripId.current = trip.tripId;
      void startWatcher();
      return;
    }

    if (!shouldTrack && watcherId.current !== null) {
      void stopWatcher();
    }
  }, [enabled, trip?.tripId, trip?.currentTripStatus]);

  useEffect(() => () => {
    if (watcherId.current !== null) navigator.geolocation.clearWatch(watcherId.current);
  }, []);

  return (
    <TrackingContext.Provider
      value={{
        trip,
        loading,
        error,
        trackingActive,
        trackingStarting,
        eta,
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
