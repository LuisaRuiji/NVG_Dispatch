import { useMemo, useState } from "react";
import L from "leaflet";
import { MapContainer, Marker, Polyline, TileLayer, useMapEvents } from "react-leaflet";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export type LocationPins = {
  pickupLatitude?: number | null;
  pickupLongitude?: number | null;
  dropoffLatitude?: number | null;
  dropoffLongitude?: number | null;
};

type StopKind = "pickup" | "dropoff";

type LocationPinPickerProps = {
  value: LocationPins;
  onChange: (value: LocationPins) => void;
  className?: string;
};

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

function hasPair(latitude?: number | null, longitude?: number | null): latitude is number {
  return (
    typeof latitude === "number" &&
    Number.isFinite(latitude) &&
    typeof longitude === "number" &&
    Number.isFinite(longitude)
  );
}

function MapClickHandler({
  activeStop,
  onPick
}: {
  activeStop: StopKind;
  onPick: (stop: StopKind, latitude: number, longitude: number) => void;
}) {
  useMapEvents({
    click: (event) => {
      onPick(activeStop, event.latlng.lat, event.latlng.lng);
    }
  });

  return null;
}

function formatCoord(value?: number | null) {
  return typeof value === "number" && Number.isFinite(value) ? value.toFixed(5) : "--";
}

export default function LocationPinPicker({ value, onChange, className }: LocationPinPickerProps) {
  const [activeStop, setActiveStop] = useState<StopKind>("pickup");
  const pickupPosition = hasPair(value.pickupLatitude, value.pickupLongitude)
    ? ([value.pickupLatitude, value.pickupLongitude as number] as [number, number])
    : null;
  const dropoffPosition = hasPair(value.dropoffLatitude, value.dropoffLongitude)
    ? ([value.dropoffLatitude, value.dropoffLongitude as number] as [number, number])
    : null;
  const positions = useMemo(
    () => [pickupPosition, dropoffPosition].filter(Boolean) as [number, number][],
    [pickupPosition, dropoffPosition]
  );
  const routePositions = pickupPosition && dropoffPosition ? [pickupPosition, dropoffPosition] : null;

  const handlePick = (stop: StopKind, latitude: number, longitude: number) => {
    onChange(
      stop === "pickup"
        ? { ...value, pickupLatitude: latitude, pickupLongitude: longitude }
        : { ...value, dropoffLatitude: latitude, dropoffLongitude: longitude }
    );
  };

  const clearPins = () => {
    onChange({
      pickupLatitude: null,
      pickupLongitude: null,
      dropoffLatitude: null,
      dropoffLongitude: null
    });
  };

  return (
    <div className={cn("space-y-3", className)}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            size="sm"
            variant={activeStop === "pickup" ? "default" : "outline"}
            onClick={() => setActiveStop("pickup")}
          >
            Pickup Pin
          </Button>
          <Button
            type="button"
            size="sm"
            variant={activeStop === "dropoff" ? "default" : "outline"}
            onClick={() => setActiveStop("dropoff")}
          >
            Dropoff Pin
          </Button>
        </div>
        <Button type="button" size="sm" variant="ghost" onClick={clearPins}>
          Clear Pins
        </Button>
      </div>
      <div className="trip-map-shell h-[18rem]">
        <MapContainer
          center={positions[0] ?? DAVAO_CENTER}
          zoom={12}
          scrollWheelZoom={false}
          className="trip-map-canvas"
        >
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <MapClickHandler activeStop={activeStop} onPick={handlePick} />
          {routePositions ? (
            <Polyline
              positions={routePositions}
              pathOptions={{ color: "hsl(var(--primary))", weight: 4, opacity: 0.55 }}
            />
          ) : null}
          {pickupPosition ? <Marker position={pickupPosition} icon={pickupIcon} /> : null}
          {dropoffPosition ? <Marker position={dropoffPosition} icon={dropoffIcon} /> : null}
        </MapContainer>
      </div>
      <div className="grid gap-2 text-xs text-muted-foreground sm:grid-cols-2">
        <div className="rounded-lg border border-border/60 bg-muted/20 px-3 py-2">
          Pickup: {formatCoord(value.pickupLatitude)}, {formatCoord(value.pickupLongitude)}
        </div>
        <div className="rounded-lg border border-border/60 bg-muted/20 px-3 py-2">
          Dropoff: {formatCoord(value.dropoffLatitude)}, {formatCoord(value.dropoffLongitude)}
        </div>
      </div>
    </div>
  );
}
