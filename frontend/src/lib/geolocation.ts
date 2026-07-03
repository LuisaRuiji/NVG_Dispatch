export type BrowserLocationFix = {
  latitude: number;
  longitude: number;
  accuracyMeters?: number | null;
  recordedAt: string;
};

export function getBrowserLocationFix(timeoutMs = 15000): Promise<BrowserLocationFix> {
  if (!("geolocation" in navigator)) {
    return Promise.reject(new Error("Location is not available on this device."));
  }

  // The caller decides when to ask again; this helper never runs background tracking by itself.
  return new Promise((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(
      (position) => {
        resolve({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracyMeters: Number.isFinite(position.coords.accuracy)
            ? position.coords.accuracy
            : null,
          recordedAt: new Date(position.timestamp).toISOString()
        });
      },
      (error) => {
        const message =
          error.code === error.PERMISSION_DENIED
            ? "Location permission was denied."
            : error.code === error.TIMEOUT
            ? "Location request timed out."
            : "Could not read current location.";
        reject(new Error(message));
      },
      {
        enableHighAccuracy: true,
        maximumAge: 15000,
        timeout: timeoutMs
      }
    );
  });
}
