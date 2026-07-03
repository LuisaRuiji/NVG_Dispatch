import { useEffect, useMemo, useState } from "react";
import L from "leaflet";
import { MapContainer, Marker, Polyline, Popup, TileLayer, useMap } from "react-leaflet";
import { useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import StatusBadge from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import { cn } from "@/lib/utils";
import { useToast } from "@/lib/useToast";
import { useDispatchHub } from "@/hooks/useDispatchHub";
import type { DispatchTripListItem } from "./types";
import { statusLabels } from "./types";
import { RefreshCw, Truck } from "lucide-react";

const DAVAO_CENTER: [number, number] = [7.0731, 125.6128];

const pickupIcon = L.divIcon({
  className: "",
  html: '<div class="trip-map-marker trip-map-marker-pickup"></div>',
  iconAnchor: [8, 18],
  popupAnchor: [0, -18]
});

const dropoffIcon = L.divIcon({
  className: "",
  html: '<div class="trip-map-marker trip-map-marker-dropoff"></div>',
  iconAnchor: [8, 18],
  popupAnchor: [0, -18]
});

const driverIcon = L.divIcon({
  className: "",
  html: '<div class="trip-map-marker trip-map-marker-driver"></div>',
  iconAnchor: [10, 20],
  popupAnchor: [0, -20]
});

function hasCoord(latitude?: number | null, longitude?: number | null): latitude is number {
  return (
    typeof latitude === "number" &&
    Number.isFinite(latitude) &&
    typeof longitude === "number" &&
    Number.isFinite(longitude)
  );
}

function formatLastPing(value?: string | null) {
  if (!value) return "No ping";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "No ping";
  return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

function isStale(value?: string | null) {
  if (!value) return false;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return false;
  return Date.now() - date.getTime() > 5 * 60 * 1000;
}

function BoundsController({ positions }: { positions: [number, number][] }) {
  const map = useMap();

  useEffect(() => {
    window.setTimeout(() => map.invalidateSize(), 0);
  }, [map]);

  useEffect(() => {
    if (positions.length === 0) return;
    if (positions.length === 1) {
      map.setView(positions[0], 13);
      return;
    }

    map.fitBounds(L.latLngBounds(positions), { padding: [32, 32], maxZoom: 14 });
  }, [map, positions]);

  return null;
}

function FleetMap({
  trips,
  selected,
  onSelect
}: {
  trips: DispatchTripListItem[];
  selected?: DispatchTripListItem | null;
  onSelect: (tripId: string) => void;
}) {
  const driverPositions = trips
    .filter((trip) =>
      hasCoord(trip.latestDriverLocation?.latitude, trip.latestDriverLocation?.longitude)
    )
    .map((trip) => ({
      trip,
      position: [
        trip.latestDriverLocation!.latitude,
        trip.latestDriverLocation!.longitude
      ] as [number, number]
    }));
  const pickupPosition =
    selected && hasCoord(selected.pickupLatitude, selected.pickupLongitude)
      ? ([selected.pickupLatitude, selected.pickupLongitude as number] as [number, number])
      : null;
  const dropoffPosition =
    selected && hasCoord(selected.dropoffLatitude, selected.dropoffLongitude)
      ? ([selected.dropoffLatitude, selected.dropoffLongitude as number] as [number, number])
      : null;
  const positions = useMemo(
    () =>
      [
        ...driverPositions.map((item) => item.position),
        pickupPosition,
        dropoffPosition
      ].filter(Boolean) as [number, number][],
    [driverPositions, pickupPosition, dropoffPosition]
  );
  const routePositions = pickupPosition && dropoffPosition ? [pickupPosition, dropoffPosition] : null;

  if (positions.length === 0) {
    return (
      <div className="surface-soft grid min-h-[34rem] place-items-center p-6 text-center">
        <div>
          <p className="text-sm font-semibold text-foreground">No active map points yet</p>
          <p className="mt-1 text-xs text-muted-foreground">
            Active trips appear here once stops have pins or drivers send a location.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="trip-map-shell h-[34rem] lg:h-[calc(100vh-16rem)]">
      <MapContainer center={positions[0] ?? DAVAO_CENTER} zoom={12} scrollWheelZoom className="trip-map-canvas">
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <BoundsController positions={positions} />
        {routePositions ? (
          <Polyline
            positions={routePositions}
            pathOptions={{ color: "hsl(var(--primary))", weight: 4, opacity: 0.55 }}
          />
        ) : null}
        {pickupPosition ? (
          <Marker position={pickupPosition} icon={pickupIcon}>
            <Popup>
              <strong>Pickup</strong>
              <br />
              {selected?.pickupLocation ?? "Pickup"}
            </Popup>
          </Marker>
        ) : null}
        {dropoffPosition ? (
          <Marker position={dropoffPosition} icon={dropoffIcon}>
            <Popup>
              <strong>Dropoff</strong>
              <br />
              {selected?.dropoffLocation ?? "Dropoff"}
            </Popup>
          </Marker>
        ) : null}
        {driverPositions.map(({ trip, position }) => (
          <Marker
            key={trip.id}
            position={position}
            icon={driverIcon}
            eventHandlers={{ click: () => onSelect(trip.id) }}
          >
            <Popup>
              <strong>{trip.driverUsername ?? "Driver"}</strong>
              <br />
              Trip {trip.id.slice(0, 8)}
              <br />
              {trip.truckAssetCode ?? "No truck"} - {formatLastPing(trip.latestDriverLocation?.recordedAt)}
            </Popup>
          </Marker>
        ))}
      </MapContainer>
    </div>
  );
}

export default function DispatchMapPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const [loading, setLoading] = useState(true);
  const [trips, setTrips] = useState<DispatchTripListItem[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const selectedTrip = trips.find((trip) => trip.id === selectedId) ?? trips[0] ?? null;

  const loadTrips = async () => {
    try {
      setLoading(true);
      const result = await api<PagedResult<DispatchTripListItem>>(
        "/api/dispatch/trips/active?page=1&pageSize=100",
        { method: "GET" }
      );
      const items = result.items ?? [];
      setTrips(items);
      setSelectedId((current) =>
        current && items.some((trip) => trip.id === current) ? current : items[0]?.id ?? null
      );
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load active trip map.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTrips();
  }, []);

  useDispatchHub({
    onDriverLocationUpdated: (event) => {
      setTrips((current) =>
        current.map((trip) =>
          trip.id.toLowerCase() === event.tripId.toLowerCase()
            ? {
                ...trip,
                status: event.tripStatus as DispatchTripListItem["status"],
                driverUserId: event.driverId,
                driverUsername: event.driverName ?? trip.driverUsername,
                truckAssetId: event.truckId ?? trip.truckAssetId,
                truckAssetCode: event.truckPlate ?? trip.truckAssetCode,
                latestDriverLocation: {
                  latitude: event.latitude,
                  longitude: event.longitude,
                  accuracyMeters: event.accuracyMeters,
                  recordedAt: event.recordedAt
                }
              }
            : trip
        )
      );
    },
    onTripStatusChanged: () => {
      void loadTrips();
    }
  });

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Dispatch Map"
        description="Active trip locations for dispatch operations."
        actions={
          <Button variant="outline" className="gap-2" onClick={() => void loadTrips()} disabled={loading}>
            <RefreshCw className={cn("h-4 w-4", loading ? "animate-spin" : "")} />
            Refresh
          </Button>
        }
      />

      {loading ? (
        <LoadingSkeleton rows={8} />
      ) : trips.length === 0 ? (
        <EmptyState title="No active trips" description="Dispatched and in-progress trips will appear here." />
      ) : (
        <div className="grid gap-6 xl:grid-cols-[22rem_1fr]">
          <div className="surface-card max-h-[34rem] overflow-y-auto p-4 lg:max-h-[calc(100vh-16rem)]">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold text-foreground">Active Trips</h2>
              <span className="text-xs text-muted-foreground">{trips.length} total</span>
            </div>
            <div className="mt-4 space-y-2">
              {trips.map((trip) => {
                const stale = isStale(trip.latestDriverLocation?.recordedAt);
                const selected = selectedTrip?.id === trip.id;
                return (
                  <button
                    type="button"
                    key={trip.id}
                    onClick={() => setSelectedId(trip.id)}
                    className={cn(
                      "w-full rounded-xl border px-3 py-3 text-left transition hover:bg-muted/60",
                      selected
                        ? "border-primary bg-primary/5 shadow-sm"
                        : "border-border/60 bg-background"
                    )}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="truncate font-mono text-sm font-semibold text-foreground">
                          {trip.containerNumber ?? trip.id.slice(0, 8)}
                        </p>
                        <p className="mt-1 truncate text-xs text-muted-foreground">
                          {trip.driverUsername ?? "Unassigned driver"} - {trip.truckAssetCode ?? "No truck"}
                        </p>
                      </div>
                      <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
                    </div>
                    <div className="mt-3 flex items-center justify-between gap-3 text-xs text-muted-foreground">
                      <span className="inline-flex min-w-0 items-center gap-1">
                        <Truck className="h-3.5 w-3.5 shrink-0 text-primary" />
                        <span className="truncate">{trip.customer?.name ?? "Customer"}</span>
                      </span>
                      <span className={stale ? "text-amber-700" : ""}>
                        {formatLastPing(trip.latestDriverLocation?.recordedAt)}
                      </span>
                    </div>
                  </button>
                );
              })}
            </div>
          </div>

          <div className="space-y-4">
            <FleetMap trips={trips} selected={selectedTrip} onSelect={setSelectedId} />
            {selectedTrip ? (
              <div className="surface-card p-4">
                <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                  <div>
                    <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">
                      Selected Trip
                    </p>
                    <h2 className="mt-1 font-mono text-lg font-semibold text-foreground">
                      {selectedTrip.containerNumber ?? selectedTrip.id.slice(0, 8)}
                    </h2>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {selectedTrip.pickupLocation ?? "Pickup pending"} to{" "}
                      {selectedTrip.dropoffLocation ?? "Dropoff pending"}
                    </p>
                  </div>
                  <Button variant="outline" onClick={() => nav(`/dispatch/trips/${selectedTrip.id}`)}>
                    Open Trip
                  </Button>
                </div>
              </div>
            ) : null}
          </div>
        </div>
      )}
    </div>
  );
}
