import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import type { AssetResponse } from "@/lib/api/types";
import type {
  ContainerSize,
  DispatchAssignmentDayTripItem,
  DispatchAssignmentDayView,
  DispatchTripListItem,
  TripType,
  TripStatus
} from "@/features/dispatch/types";

type UserSummary = {
  id: string;
  username: string;
  isActive: boolean;
  roles: string[];
};

type DispatchRequestSummary = {
  id: string;
  customerName: string;
  pickupLocation: string;
  dropoffLocation: string;
  requestedPickupTime?: string | null;
  containerSize: ContainerSize;
  tripType: TripType;
  containerNumber?: string | null;
  bookingNumber?: string | null;
  documentsCount: number;
  status: "APPROVED";
};

export type DispatcherAlertTone = "danger" | "attention";

export type DispatcherAlert = {
  tripId: string;
  reference: string;
  title: string;
  detail: string;
  tone: DispatcherAlertTone;
};

export type DispatcherTimelineTrip = DispatchAssignmentDayTripItem & {
  pickupLocation?: string | null;
  dropoffLocation?: string | null;
  isDelayed: boolean;
};

export type DriverAvailabilityState = "AVAILABLE" | "SCHEDULED" | "ON_ROAD";

export type DriverAvailabilityItem = {
  driverId: string;
  driverName: string;
  state: DriverAvailabilityState;
  tripId?: string;
  tripReference?: string;
  tripStatus?: TripStatus;
  nextStart?: string;
  currentTruck?: string;
  todayJobs: number;
  lastActivityAt?: string;
};

export type TruckAvailabilityState = "AVAILABLE" | "ON_ROAD" | "INACTIVE";

export type TruckAvailabilityItem = {
  truckId: string;
  assetCode: string;
  plateNo?: string | null;
  state: TruckAvailabilityState;
  tripId?: string;
  tripReference?: string;
  tripStatus?: TripStatus;
  driverName?: string;
  lastActivityAt?: string;
};

export type UpcomingBookingItem = {
  requestId: string;
  customerName: string;
  pickupLocation: string;
  dropoffLocation: string;
  requestedPickupTime?: string | null;
  containerSize: ContainerSize;
  tripType: TripType;
  containerNumber?: string | null;
  bookingNumber?: string | null;
  documentsCount: number;
};

export type DispatcherOperationsSnapshot = {
  timeline: DispatcherTimelineTrip[];
  alerts: DispatcherAlert[];
  drivers: DriverAvailabilityItem[];
  trucks: TruckAvailabilityItem[];
  upcomingBookings: UpcomingBookingItem[];
  loadedAt: string;
};

const runningStatuses = new Set<TripStatus>([
  "DISPATCHED",
  "ENROUTE_PICKUP",
  "AT_PICKUP",
  "LOADED",
  "ENROUTE_DROPOFF",
  "AT_DROPOFF",
  "ON_HOLD",
  "FAILED_ATTEMPT"
]);

function toDateOnly(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function getTripReference(trip: Pick<DispatchTripListItem, "id" | "containerNumber">) {
  return trip.containerNumber ?? `Trip ${trip.id.slice(0, 8).toUpperCase()}`;
}

function buildAlerts(activeTrips: DispatchTripListItem[]): DispatcherAlert[] {
  const alerts = activeTrips
    .filter((trip) => runningStatuses.has(trip.status))
    .flatMap<DispatcherAlert>((trip) => {
      const reference = getTripReference(trip);

      if (trip.status === "FAILED_ATTEMPT") {
        return [{
          tripId: trip.id,
          reference,
          title: "Failed delivery attempt",
          detail: `${trip.driverUsername ?? "Unassigned driver"} needs a resolution before this trip can continue.`,
          tone: "danger"
        }];
      }

      if (trip.lateDelivery) {
        return [{
          tripId: trip.id,
          reference,
          title: "Delivery window missed",
          detail: `${trip.dropoffLocation ?? "Dropoff"} is past its scheduled time.`,
          tone: "danger"
        }];
      }

      if (trip.status === "ON_HOLD") {
        const duration = typeof trip.onHoldMinutes === "number" ? ` for ${trip.onHoldMinutes} min` : "";
        return [{
          tripId: trip.id,
          reference,
          title: `Trip on hold${duration}`,
          detail: `${trip.driverUsername ?? "Unassigned driver"} cannot continue until the hold is reviewed.`,
          tone: "attention"
        }];
      }

      if (trip.latePickup) {
        return [{
          tripId: trip.id,
          reference,
          title: "Pickup window missed",
          detail: `${trip.pickupLocation ?? "Pickup"} requires schedule attention.`,
          tone: "attention"
        }];
      }

      if (trip.rejectedRequiredDocumentCount > 0) {
        return [{
          tripId: trip.id,
          reference,
          title: "Required document rejected",
          detail: `${trip.rejectedRequiredDocumentCount} document${trip.rejectedRequiredDocumentCount === 1 ? "" : "s"} must be replaced.`,
          tone: "attention"
        }];
      }

      return [];
    });

  const toneRank: Record<DispatcherAlertTone, number> = { danger: 0, attention: 1 };
  return alerts.sort((a, b) => toneRank[a.tone] - toneRank[b.tone]).slice(0, 5);
}

function buildDriverAvailability(
  users: UserSummary[],
  activeTrips: DispatchTripListItem[],
  timeline: DispatchAssignmentDayTripItem[]
): DriverAvailabilityItem[] {
  const now = Date.now();
  const runningByDriver = new Map<string, DispatchTripListItem>();
  activeTrips.forEach((trip) => {
    if (trip.driverUserId && runningStatuses.has(trip.status) && !runningByDriver.has(trip.driverUserId)) {
      runningByDriver.set(trip.driverUserId, trip);
    }
  });

  const scheduledByDriver = new Map<string, DispatchAssignmentDayTripItem>();
  timeline
    .filter((trip) => trip.driverUserId && new Date(trip.plannedStart).getTime() >= now)
    .sort((a, b) => new Date(a.plannedStart).getTime() - new Date(b.plannedStart).getTime())
    .forEach((trip) => {
      if (trip.driverUserId && !scheduledByDriver.has(trip.driverUserId)) {
        scheduledByDriver.set(trip.driverUserId, trip);
      }
    });

  const todayJobsByDriver = new Map<string, number>();
  timeline.forEach((trip) => {
    if (!trip.driverUserId) return;
    todayJobsByDriver.set(trip.driverUserId, (todayJobsByDriver.get(trip.driverUserId) ?? 0) + 1);
  });

  const stateRank: Record<DriverAvailabilityState, number> = {
    AVAILABLE: 0,
    SCHEDULED: 1,
    ON_ROAD: 2
  };

  return users
    .filter((user) => user.isActive && user.roles.includes("Driver"))
    .map<DriverAvailabilityItem>((driver) => {
      const running = runningByDriver.get(driver.id);
      if (running) {
        return {
          driverId: driver.id,
          driverName: driver.username,
          state: "ON_ROAD",
          tripId: running.id,
          tripReference: getTripReference(running),
          tripStatus: running.status,
          currentTruck: running.truckAssetCode ?? undefined,
          todayJobs: todayJobsByDriver.get(driver.id) ?? 0,
          lastActivityAt: running.latestDriverLocation?.recordedAt ?? running.updatedAt ?? running.createdAt
        };
      }

      const scheduled = scheduledByDriver.get(driver.id);
      if (scheduled) {
        return {
          driverId: driver.id,
          driverName: driver.username,
          state: "SCHEDULED",
          tripId: scheduled.tripId,
          tripReference: scheduled.tripReference,
          tripStatus: scheduled.status,
          nextStart: scheduled.plannedStart,
          currentTruck: scheduled.truckAssetCode ?? undefined,
          todayJobs: todayJobsByDriver.get(driver.id) ?? 0
        };
      }

      return {
        driverId: driver.id,
        driverName: driver.username,
        state: "AVAILABLE",
        todayJobs: todayJobsByDriver.get(driver.id) ?? 0
      };
    })
    .sort((a, b) => stateRank[a.state] - stateRank[b.state] || a.driverName.localeCompare(b.driverName));
}

function buildTruckAvailability(
  assets: AssetResponse[],
  activeTrips: DispatchTripListItem[]
): TruckAvailabilityItem[] {
  const runningByTruck = new Map<string, DispatchTripListItem>();
  activeTrips.forEach((trip) => {
    if (trip.truckAssetId && runningStatuses.has(trip.status) && !runningByTruck.has(trip.truckAssetId)) {
      runningByTruck.set(trip.truckAssetId, trip);
    }
  });

  const stateRank: Record<TruckAvailabilityState, number> = {
    AVAILABLE: 0,
    ON_ROAD: 1,
    INACTIVE: 2
  };

  return assets
    .filter((asset) => asset.assetType === "TRUCK")
    .map<TruckAvailabilityItem>((asset) => {
      const running = runningByTruck.get(asset.id);
      if (running) {
        return {
          truckId: asset.id,
          assetCode: asset.assetCode,
          plateNo: asset.plateNo,
          state: "ON_ROAD",
          tripId: running.id,
          tripReference: getTripReference(running),
          tripStatus: running.status,
          driverName: running.driverUsername ?? undefined,
          lastActivityAt: running.latestDriverLocation?.recordedAt ?? running.updatedAt ?? running.createdAt
        };
      }

      return {
        truckId: asset.id,
        assetCode: asset.assetCode,
        plateNo: asset.plateNo,
        state: asset.status === "ACTIVE" ? "AVAILABLE" : "INACTIVE"
      };
    })
    .sort((a, b) => stateRank[a.state] - stateRank[b.state] || a.assetCode.localeCompare(b.assetCode));
}

export async function fetchDispatcherOperations(): Promise<DispatcherOperationsSnapshot> {
  const day = toDateOnly(new Date());
  const [assignmentView, activeResult, users, assets, approvedRequests] = await Promise.all([
    api<DispatchAssignmentDayView>(
      `/api/dispatch/trips/assignment-day?day=${encodeURIComponent(day)}&groupBy=driver`,
      { method: "GET" }
    ),
    api<PagedResult<DispatchTripListItem>>("/api/dispatch/trips/active?page=1&pageSize=100", { method: "GET" }),
    api<UserSummary[]>("/api/users", { method: "GET" }),
    api<AssetResponse[]>("/api/assets", { method: "GET" }),
    api<PagedResult<DispatchRequestSummary>>(
      "/api/dispatch/requests?status=APPROVED&page=1&pageSize=5",
      { method: "GET" }
    )
  ]);

  const activeTrips = activeResult.items ?? [];
  const activeById = new Map(activeTrips.map((trip) => [trip.id, trip]));
  const timelineItems = (assignmentView.groups ?? []).flatMap((group) => group.trips ?? []);
  const uniqueTimeline = Array.from(new Map(timelineItems.map((trip) => [trip.tripId, trip])).values())
    .sort((a, b) => new Date(a.plannedStart).getTime() - new Date(b.plannedStart).getTime());

  return {
    timeline: uniqueTimeline.map((trip) => {
      const detail = activeById.get(trip.tripId);
      return {
        ...trip,
        pickupLocation: detail?.pickupLocation,
        dropoffLocation: detail?.dropoffLocation,
        isDelayed: Boolean(detail?.latePickup || detail?.lateDelivery)
      };
    }),
    alerts: buildAlerts(activeTrips),
    drivers: buildDriverAvailability(users ?? [], activeTrips, uniqueTimeline),
    trucks: buildTruckAvailability(assets ?? [], activeTrips),
    upcomingBookings: (approvedRequests.items ?? []).map((request) => ({
      requestId: request.id,
      customerName: request.customerName,
      pickupLocation: request.pickupLocation,
      dropoffLocation: request.dropoffLocation,
      requestedPickupTime: request.requestedPickupTime,
      containerSize: request.containerSize,
      tripType: request.tripType,
      containerNumber: request.containerNumber,
      bookingNumber: request.bookingNumber,
      documentsCount: request.documentsCount
    })),
    loadedAt: new Date().toISOString()
  };
}
