export interface LiveMapTripResponse {
  tripId: string;
  dispatchTruckId: string | null;
  plateNumber: string;
  dispatchDriverId: string | null;
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

export interface LocationUpdateBroadcastPayload {
  tripId: string;
  dispatchTruckId: string;
  truckPlateNumber: string;
  dispatchDriverId: string;
  driverUserId: string;
  latitude: number;
  longitude: number;
  accuracyMeters: number | null;
  speedKph: number | null;
  heading: number | null;
  tripStatus: string;
  recordedAt: string;
  receivedAt: string;
}
