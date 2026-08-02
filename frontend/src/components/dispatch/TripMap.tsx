import { useEffect, useMemo } from "react";
import L from "leaflet";
import { MapContainer, Marker, Polyline, Popup, TileLayer, useMap } from "react-leaflet";
import { cn } from "@/lib/utils";

export type TripMapPoint = {
  latitude?: number | null;
  longitude?: number | null;
  label: string;
  detail?: string | null;
};

type TripMapProps = {
  pickup?: TripMapPoint | null;
  dropoff?: TripMapPoint | null;
  driver?: TripMapPoint | null;
  activeLeg?: "pickup" | "dropoff";
  driverRecordedAt?: string | null;
  driverAccuracyMeters?: number | null;
  className?: string;
  heightClassName?: string;
  emptyTitle?: string;
};

type MarkerKind = "pickup" | "dropoff" | "driver";

const DAVAO_CENTER: [number, number] = [7.0731, 125.6128];

const markerIcons: Record<MarkerKind, L.DivIcon> = {
  pickup: L.divIcon({
    className: "",
    html: '<div class="trip-map-marker trip-map-marker-pickup"></div>',
    iconAnchor: [8, 18],
    popupAnchor: [0, -18]
  }),
  dropoff: L.divIcon({
    className: "",
    html: '<div class="trip-map-marker trip-map-marker-dropoff"></div>',
    iconAnchor: [8, 18],
    popupAnchor: [0, -18]
  }),
  driver: L.divIcon({
    className: "",
    html: '<div class="trip-map-marker trip-map-marker-driver"></div>',
    iconAnchor: [10, 20],
    popupAnchor: [0, -20]
  })
};

function hasPoint(point?: TripMapPoint | null): point is Required<Pick<TripMapPoint, "latitude" | "longitude">> & TripMapPoint {
  return (
    typeof point?.latitude === "number" &&
    Number.isFinite(point.latitude) &&
    typeof point.longitude === "number" &&
    Number.isFinite(point.longitude)
  );
}

function toLatLng(point: TripMapPoint): [number, number] {
  return [point.latitude as number, point.longitude as number];
}

function formatRecordedAt(value?: string | null) {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return date.toLocaleString();
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

    map.fitBounds(L.latLngBounds(positions), { padding: [28, 28], maxZoom: 14 });
  }, [map, positions]);

  return null;
}

export default function TripMap({
  pickup,
  dropoff,
  driver,
  activeLeg = "dropoff",
  driverRecordedAt,
  driverAccuracyMeters,
  className,
  heightClassName = "h-[22rem]",
  emptyTitle = "No map pins available"
}: TripMapProps) {
  const pickupPosition = hasPoint(pickup) ? toLatLng(pickup) : null;
  const dropoffPosition = hasPoint(dropoff) ? toLatLng(dropoff) : null;
  const driverPosition = hasPoint(driver) ? toLatLng(driver) : null;
  const routePositions = pickupPosition && dropoffPosition ? [pickupPosition, dropoffPosition] : null;
  const activeDestination = activeLeg === "pickup" ? pickupPosition : dropoffPosition;
  const driverLegPositions = driverPosition && activeDestination ? [driverPosition, activeDestination] : null;
  const positions = useMemo(
    () => {
      if (driverPosition && activeDestination) return [driverPosition, activeDestination];
      return [pickupPosition, dropoffPosition, driverPosition].filter(Boolean) as [number, number][];
    },
    [pickupPosition, dropoffPosition, driverPosition, activeDestination]
  );
  const recordedLabel = formatRecordedAt(driverRecordedAt);
  const stale = isStale(driverRecordedAt);

  if (positions.length === 0) {
    return (
      <div className={cn("surface-soft grid min-h-[14rem] place-items-center p-6 text-center", className)}>
        <div>
          <p className="text-sm font-semibold text-foreground">{emptyTitle}</p>
          <p className="mt-1 text-xs text-muted-foreground">
            Add pickup, dropoff, or driver coordinates to show this trip on the map.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className={cn("trip-map-shell", heightClassName, className)}>
      <MapContainer
        center={positions[0] ?? DAVAO_CENTER}
        zoom={13}
        scrollWheelZoom={false}
        className="trip-map-canvas"
      >
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
        {driverLegPositions ? (
          <Polyline
            positions={driverLegPositions}
            pathOptions={{ color: "hsl(var(--foreground))", weight: 2, opacity: 0.45, dashArray: "6 8" }}
          />
        ) : null}
        {pickupPosition ? (
          <Marker position={pickupPosition} icon={markerIcons.pickup}>
            <Popup>
              <strong>Pickup</strong>
              <br />
              {pickup?.label}
              {pickup?.detail ? (
                <>
                  <br />
                  {pickup.detail}
                </>
              ) : null}
            </Popup>
          </Marker>
        ) : null}
        {dropoffPosition ? (
          <Marker position={dropoffPosition} icon={markerIcons.dropoff}>
            <Popup>
              <strong>Dropoff</strong>
              <br />
              {dropoff?.label}
              {dropoff?.detail ? (
                <>
                  <br />
                  {dropoff.detail}
                </>
              ) : null}
            </Popup>
          </Marker>
        ) : null}
        {driverPosition ? (
          <Marker position={driverPosition} icon={markerIcons.driver}>
            <Popup>
              <strong>{driver?.label ?? "Driver location"}</strong>
              {recordedLabel ? (
                <>
                  <br />
                  {stale ? "Last ping" : "Updated"} {recordedLabel}
                </>
              ) : null}
              {typeof driverAccuracyMeters === "number" ? (
                <>
                  <br />
                  Accuracy {Math.round(driverAccuracyMeters)} m
                </>
              ) : null}
            </Popup>
          </Marker>
        ) : null}
      </MapContainer>
    </div>
  );
}
