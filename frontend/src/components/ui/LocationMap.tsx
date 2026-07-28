import { useEffect } from "react";
import { MapContainer, TileLayer, Marker, Popup, useMap } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";

// Fix for default marker icons in React Leaflet
delete (L.Icon.Default.prototype as any)._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-icon-2x.png",
  iconUrl: "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-icon.png",
  shadowUrl: "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-shadow.png",
});

interface LocationMapProps {
  latitude: number;
  longitude: number;
  label?: string;
  className?: string;
  height?: string;
}

// Helper component to auto-center map when coordinates change
function MapUpdater({ lat, lon }: { lat: number; lon: number }) {
  const map = useMap();
  useEffect(() => {
    map.setView([lat, lon], map.getZoom());
  }, [lat, lon, map]);
  return null;
}

export function LocationMap({ latitude, longitude, label, className = "", height = "300px" }: LocationMapProps) {
  if (!latitude || !longitude) return null;

  return (
    <div className={`relative z-0 overflow-hidden rounded-md border border-slate-200 dark:border-slate-800 [isolation:isolate] ${className}`} style={{ height, width: "100%" }}>
      <MapContainer center={[latitude, longitude]} zoom={15} style={{ height: "100%", width: "100%" }}>
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <MapUpdater lat={latitude} lon={longitude} />
        <Marker position={[latitude, longitude]}>
          <Popup>{label || "Selected Location"}</Popup>
        </Marker>
      </MapContainer>
    </div>
  );
}
