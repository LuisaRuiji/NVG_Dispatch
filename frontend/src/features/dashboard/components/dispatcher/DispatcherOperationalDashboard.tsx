import { Link } from "react-router-dom";
import type { LucideIcon } from "lucide-react";
import {
  AlertTriangle,
  ArrowRight,
  CalendarClock,
  CheckCircle2,
  CircleDot,
  ClipboardCheck,
  FileCheck2,
  FileWarning,
  Gauge,
  MapPin,
  Route,
  ShieldAlert,
  Truck,
  UserCheck,
  Users,
  Warehouse
} from "lucide-react";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import StatusBadge from "@/components/StatusBadge";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { cn } from "@/lib/utils";
import { statusLabels } from "@/features/dispatch/types";
import type { DispatchDashboardKpis } from "../../kpis";
import type {
  DispatcherAlertTone,
  DispatcherOperationsSnapshot,
  DriverAvailabilityState,
  TruckAvailabilityState
} from "../../dispatcherOperations";

type Props = {
  kpis: DispatchDashboardKpis;
  snapshot: DispatcherOperationsSnapshot | null;
  loading: boolean;
  error: string | null;
  onRetry: () => void;
};

type MetricTone = "neutral" | "info" | "attention";

const metricToneClasses: Record<MetricTone, string> = {
  neutral: "text-[#475569]",
  info: "text-[#1D4ED8]",
  attention: "text-[#92400E]"
};

const alertToneClasses: Record<DispatcherAlertTone, string> = {
  danger: "border-destructive/30 bg-destructive/5 text-destructive",
  attention: "border-amber-300/70 bg-amber-50/70 text-amber-700 dark:border-amber-900 dark:bg-amber-950/30 dark:text-amber-300"
};

const availabilityLabels: Record<DriverAvailabilityState, string> = {
  AVAILABLE: "Available",
  SCHEDULED: "Scheduled",
  ON_ROAD: "On road"
};

const availabilityClasses: Record<DriverAvailabilityState, string> = {
  AVAILABLE: "border-emerald-200 bg-emerald-50 text-emerald-800 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300",
  SCHEDULED: "border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300",
  ON_ROAD: "border-blue-200 bg-blue-50 text-blue-800 dark:border-blue-900 dark:bg-blue-950/40 dark:text-blue-300"
};

const truckAvailabilityLabels: Record<TruckAvailabilityState, string> = {
  AVAILABLE: "Available",
  ON_ROAD: "On road",
  INACTIVE: "Inactive"
};

const truckAvailabilityClasses: Record<TruckAvailabilityState, string> = {
  AVAILABLE: availabilityClasses.AVAILABLE,
  ON_ROAD: availabilityClasses.ON_ROAD,
  INACTIVE: "border-border bg-muted text-muted-foreground"
};

const containerSizeLabels = {
  TWENTY_FT: "20 ft",
  FORTY_FT: "40 ft",
  FORTY_HC: "40 HC"
} as const;

const tripTypeLabels = {
  PORT_PICKUP: "Port pickup",
  PORT_DROPOFF: "Port dropoff",
  YARD_TRANSFER: "Yard transfer",
  LONG_HAUL: "Long haul"
} as const;

function formatTime(value: string) {
  return new Date(value).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
}

function formatDateTime(value?: string | null) {
  if (!value) return "Schedule pending";
  return new Date(value).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function formatRelativeTime(value?: string | null) {
  if (!value) return "No recent activity";
  const elapsedMinutes = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 60_000));
  if (elapsedMinutes < 1) return "Updated now";
  if (elapsedMinutes < 60) return `Updated ${elapsedMinutes}m ago`;
  const elapsedHours = Math.round(elapsedMinutes / 60);
  if (elapsedHours < 24) return `Updated ${elapsedHours}h ago`;
  return `Updated ${new Date(value).toLocaleDateString(undefined, { month: "short", day: "numeric" })}`;
}

function formatUpdatedAt(value?: string) {
  if (!value) return "Waiting for operational data";
  return `Snapshot ${new Date(value).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" })}`;
}

function OperationalStrip({ kpis }: { kpis: DispatchDashboardKpis }) {
  const metrics: {
    label: string;
    value: number;
    hint: string;
    to: string;
    tone: MetricTone;
    icon: LucideIcon;
  }[] = [
    { label: "Today's dispatches", value: kpis.todayDispatches, hint: "Scheduled movements", to: "/dispatch/board", tone: "info", icon: CalendarClock },
    { label: "Waiting approval", value: kpis.pendingShipmentRequests, hint: "Submitted bookings", to: "/dispatch/requests", tone: "attention", icon: FileWarning },
    { label: "Need scheduling", value: kpis.approvedShipmentRequests, hint: "Approved bookings", to: "/dispatch/planning", tone: "attention", icon: Route },
    { label: "Running trips", value: kpis.activeTrips, hint: "Currently in execution", to: "/dispatch/trips", tone: "neutral", icon: CircleDot }
  ];

  return (
    <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4" aria-label="Dispatch workload summary">
      <div className="contents">
        {metrics.map((metric) => (
          <Link
            key={metric.label}
            to={metric.to}
            className="operations-kpi group min-w-0 transition-colors hover:bg-muted/50 focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring motion-reduce:transition-none"
          >
            <div className={cn("operations-kpi__icon", metricToneClasses[metric.tone])}>
              <metric.icon className="h-5 w-5" aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <p className="operations-kpi__value font-mono tabular-nums">{metric.value.toLocaleString()}</p>
              <p className="operations-kpi__label" title={metric.label}>{metric.label}</p>
              <p className="mt-1 truncate text-[11px] text-muted-foreground">{metric.hint}</p>
            </div>
          </Link>
        ))}
      </div>
    </section>
  );
}

type AttentionItem = {
  key: string;
  title: string;
  detail: string;
  count?: number;
  to: string;
  tone: DispatcherAlertTone;
};

function NeedsAttention({ snapshot, kpis }: { snapshot: DispatcherOperationsSnapshot; kpis: DispatchDashboardKpis }) {
  const tripItems: AttentionItem[] = snapshot.alerts.slice(0, 2).map((alert) => ({
    key: `${alert.tripId}:${alert.title}`,
    title: `${alert.reference} · ${alert.title}`,
    detail: alert.detail,
    to: `/dispatch/trips/${alert.tripId}`,
    tone: alert.tone
  }));

  const queueItems = ([
    {
      key: "submitted-requests",
      title: "Review submitted bookings",
      detail: "Confirm request details and approve or reject the customer submission.",
      count: kpis.pendingShipmentRequests,
      to: "/dispatch/requests",
      tone: "attention"
    },
    {
      key: "approved-requests",
      title: "Prepare approved bookings",
      detail: "Convert approved work into trips before assigning resources.",
      count: kpis.approvedShipmentRequests,
      to: "/dispatch/planning",
      tone: "attention"
    },
    {
      key: "delayed-trips",
      title: "Recover delayed trips",
      detail: "Review missed pickup or delivery windows and update the operating plan.",
      count: kpis.delayedTrips,
      to: "/dispatch/trips",
      tone: "danger"
    },
    {
      key: "document-blockers",
      title: "Resolve document blockers",
      detail: "Complete or replace required paperwork before trips can progress.",
      count: kpis.incompleteDocumentAlerts,
      to: "/dispatch/documents",
      tone: "attention"
    }
  ] satisfies AttentionItem[]).filter((item) => (item.count ?? 0) > 0);

  const items = [...tripItems, ...queueItems].slice(0, 6);
  const totalWork = kpis.pendingShipmentRequests + kpis.approvedShipmentRequests + kpis.openIssues;

  return (
    <Card className="min-w-0" aria-labelledby="needs-attention-title">
      <CardHeader className="flex-row items-start justify-between gap-4 space-y-0 p-5 sm:p-6">
        <div>
          <div className="flex items-center gap-2">
            <ShieldAlert className="h-4 w-4 text-destructive" aria-hidden="true" />
            <h3 id="needs-attention-title" className="text-base font-semibold leading-none text-foreground">Needs attention</h3>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">Prioritized exceptions and intake work for the dispatch team.</p>
        </div>
        <Badge variant="outline" className={cn(totalWork > 0 && "border-destructive/30 bg-destructive/5 text-destructive")}>
          {totalWork} items
        </Badge>
      </CardHeader>
      <CardContent className="p-0">
        {items.length === 0 ? (
          <div className="flex items-start gap-3 border-t border-border px-5 py-5 sm:px-6">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-emerald-500/10 text-emerald-700 dark:text-emerald-300">
              <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
            </div>
            <div>
              <p className="text-sm font-semibold text-foreground">The dispatch queue is clear.</p>
              <p className="mt-1 text-sm text-muted-foreground">New submissions and trip exceptions will be prioritized here.</p>
            </div>
          </div>
        ) : (
          <div className="divide-y divide-border border-t border-border">
            {items.map((item) => (
              <Link
                key={item.key}
                to={item.to}
                className="group grid grid-cols-[auto_minmax(0,1fr)] gap-3 px-5 py-4 transition-colors duration-200 hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring motion-reduce:transition-none sm:grid-cols-[auto_minmax(0,1fr)_auto] sm:items-center sm:px-6"
              >
                <div className={cn("flex h-9 w-9 items-center justify-center rounded-full border", alertToneClasses[item.tone])}>
                  <AlertTriangle className="h-4 w-4" aria-hidden="true" />
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-semibold text-foreground">{item.title}</p>
                  <p className="mt-1 line-clamp-2 text-xs leading-5 text-muted-foreground">{item.detail}</p>
                </div>
                <span className="col-start-2 inline-flex items-center gap-2 text-xs font-semibold text-primary sm:col-start-auto">
                  {item.count != null ? <span className="font-mono text-base tabular-nums text-foreground">{item.count}</span> : null}
                  Review
                  <ArrowRight className="h-3.5 w-3.5 transition-transform duration-200 group-hover:translate-x-0.5 motion-reduce:transition-none" aria-hidden="true" />
                </span>
              </Link>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function DispatchHealth({ kpis }: { kpis: DispatchDashboardKpis }) {
  const onSchedule = Math.max(kpis.activeTrips - kpis.delayedTrips, 0);
  const onSchedulePercent = kpis.activeTrips > 0 ? Math.round((onSchedule / kpis.activeTrips) * 100) : 100;
  const hasCriticalRisk = kpis.tripsFailedAttempt > 0;
  const needsIntervention = kpis.delayedTrips > 0 || kpis.tripsOnHold > 0 || kpis.incompleteDocumentAlerts > 0;
  const healthLabel = hasCriticalRisk ? "At risk" : needsIntervention ? "Needs attention" : "Healthy";
  const healthClass = hasCriticalRisk
    ? "border-destructive/30 bg-destructive/5 text-destructive"
    : needsIntervention
      ? "border-amber-300/70 bg-amber-50 text-amber-800 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300"
      : "border-emerald-200 bg-emerald-50 text-emerald-800 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300";

  const signals = [
    { label: "On schedule", value: onSchedule },
    { label: "Delayed", value: kpis.delayedTrips },
    { label: "On hold", value: kpis.tripsOnHold },
    { label: "Failed attempts", value: kpis.tripsFailedAttempt },
    { label: "Document blockers", value: kpis.incompleteDocumentAlerts }
  ];

  return (
    <Card className="min-w-0" aria-labelledby="dispatch-health-title">
      <CardHeader className="p-5 sm:p-6">
        <div className="flex items-start justify-between gap-4">
          <div>
            <div className="flex items-center gap-2">
              <Gauge className="h-4 w-4 text-primary" aria-hidden="true" />
              <h3 id="dispatch-health-title" className="text-base font-semibold leading-none text-foreground">Dispatch health</h3>
            </div>
            <p className="mt-1 text-sm text-muted-foreground">Execution risk across active movements.</p>
          </div>
          <Badge variant="outline" className={healthClass}>{healthLabel}</Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-5 px-5 pb-5 sm:px-6 sm:pb-6">
        <div>
          <div className="flex items-end justify-between gap-3">
            <div>
              <p className="font-mono text-3xl font-semibold tabular-nums text-foreground">{onSchedulePercent}%</p>
              <p className="mt-1 text-xs text-muted-foreground">of running trips on schedule</p>
            </div>
            <p className="font-mono text-xs tabular-nums text-muted-foreground">{onSchedule}/{kpis.activeTrips}</p>
          </div>
          <div className="mt-3 h-2 overflow-hidden rounded-full bg-muted" role="progressbar" aria-label="Trips on schedule" aria-valuemin={0} aria-valuemax={100} aria-valuenow={onSchedulePercent}>
            <div className={cn("h-full rounded-full", hasCriticalRisk ? "bg-destructive" : needsIntervention ? "bg-amber-500" : "bg-emerald-500")} style={{ width: `${onSchedulePercent}%` }} />
          </div>
        </div>
        <dl className="divide-y divide-border border-y border-border">
          {signals.map((signal) => (
            <div key={signal.label} className="flex items-center justify-between gap-3 py-2.5 text-sm">
              <dt className="text-muted-foreground">{signal.label}</dt>
              <dd className={cn("font-mono font-semibold tabular-nums", signal.value > 0 && signal.label !== "On schedule" ? "text-destructive" : "text-foreground")}>{signal.value}</dd>
            </div>
          ))}
        </dl>
        <Link to="/dispatch/trips" className="inline-flex items-center gap-1.5 text-xs font-semibold text-primary hover:text-primary/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
          Review trip execution <ArrowRight className="h-3.5 w-3.5" aria-hidden="true" />
        </Link>
      </CardContent>
    </Card>
  );
}

function TodayTimeline({ snapshot }: { snapshot: DispatcherOperationsSnapshot }) {
  const visibleTrips = snapshot.timeline.slice(0, 7);

  return (
    <Card className="min-w-0" aria-labelledby="today-timeline-title">
      <CardHeader className="flex-row items-start justify-between gap-4 space-y-0 p-5 sm:p-6">
        <div>
          <h3 id="today-timeline-title" className="text-base font-semibold leading-none text-foreground">Today's timeline</h3>
          <p className="mt-1 text-sm text-muted-foreground">Scheduled movements in pickup order.</p>
        </div>
        <Link to="/dispatch/board" className="shrink-0 text-xs font-semibold text-primary hover:text-primary/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
          Open board
        </Link>
      </CardHeader>
      <CardContent className="p-0">
        {visibleTrips.length === 0 ? (
          <div className="border-t border-border px-5 py-5 sm:px-6">
            <EmptyState title="No trips scheduled today" description="Approved bookings will appear here after they receive a schedule." />
          </div>
        ) : (
          <div className="divide-y divide-border border-t border-border">
            {visibleTrips.map((trip) => (
              <Link
                key={trip.tripId}
                to={`/dispatch/trips/${trip.tripId}`}
                className="grid gap-3 px-5 py-4 transition-colors duration-200 hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring motion-reduce:transition-none sm:grid-cols-[5rem_minmax(0,1fr)_auto] sm:items-center sm:px-6"
              >
                <div className="flex items-center gap-2 sm:block">
                  <p className="font-mono text-sm font-semibold tabular-nums text-foreground">{formatTime(trip.plannedStart)}</p>
                  <p className="font-mono text-xs tabular-nums text-muted-foreground">{formatTime(trip.plannedEnd)}</p>
                </div>
                <div className="min-w-0 border-l-2 border-primary/30 pl-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="truncate font-mono text-xs font-semibold text-foreground">{trip.tripReference}</p>
                    {trip.isDelayed ? <Badge className="border-destructive/30 bg-destructive/5 text-destructive">Delayed</Badge> : null}
                  </div>
                  <p className="mt-1 truncate text-sm font-medium text-foreground" title={trip.customer.name}>{trip.customer.name}</p>
                  <p className="mt-0.5 truncate text-xs text-muted-foreground" title={`${trip.pickupLocation ?? "Pickup pending"} to ${trip.dropoffLocation ?? "Dropoff pending"}`}>
                    {trip.pickupLocation ?? "Pickup pending"} <span aria-hidden="true">→</span> {trip.dropoffLocation ?? "Dropoff pending"}
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-2 sm:justify-end">
                  <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
                  <span className="text-xs text-muted-foreground">{trip.driverUsername ?? "Unassigned"}</span>
                </div>
              </Link>
            ))}
          </div>
        )}
        {snapshot.timeline.length > visibleTrips.length ? (
          <div className="border-t border-border px-5 py-3 text-xs text-muted-foreground sm:px-6">
            +{snapshot.timeline.length - visibleTrips.length} more scheduled movement{snapshot.timeline.length - visibleTrips.length === 1 ? "" : "s"}
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function UpcomingBookings({ snapshot }: { snapshot: DispatcherOperationsSnapshot }) {
  return (
    <Card className="min-w-0" aria-labelledby="upcoming-bookings-title">
      <CardHeader className="flex-row items-start justify-between gap-4 space-y-0 p-5 sm:p-6">
        <div>
          <div className="flex items-center gap-2">
            <CalendarClock className="h-4 w-4 text-primary" aria-hidden="true" />
            <h3 id="upcoming-bookings-title" className="text-base font-semibold leading-none text-foreground">Upcoming bookings</h3>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">Approved requests waiting to enter the operating plan.</p>
        </div>
        <Link to="/dispatch/planning" className="shrink-0 text-xs font-semibold text-primary hover:text-primary/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
          View queue
        </Link>
      </CardHeader>
      <CardContent className="p-0">
        {snapshot.upcomingBookings.length === 0 ? (
          <div className="border-t border-border px-5 py-5 sm:px-6">
            <EmptyState title="No approved bookings" description="Approved shipment requests will appear here before scheduling." />
          </div>
        ) : (
          <div className="divide-y divide-border border-t border-border">
            {snapshot.upcomingBookings.map((booking) => {
              const isPastDue = booking.requestedPickupTime
                ? new Date(booking.requestedPickupTime).getTime() < Date.now()
                : false;

              return (
                <Link
                  key={booking.requestId}
                  to="/dispatch/planning"
                  className="group block px-5 py-4 transition-colors duration-200 hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring motion-reduce:transition-none sm:px-6"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold text-foreground">{booking.customerName}</p>
                      <p className="mt-1 truncate text-xs text-muted-foreground" title={`${booking.pickupLocation} to ${booking.dropoffLocation}`}>
                        {booking.pickupLocation} <span aria-hidden="true">→</span> {booking.dropoffLocation}
                      </p>
                    </div>
                    <ArrowRight className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground transition-transform duration-200 group-hover:translate-x-0.5 group-hover:text-primary motion-reduce:transition-none" aria-hidden="true" />
                  </div>
                  <div className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-2 text-[11px] text-muted-foreground">
                    <span className={cn("font-mono tabular-nums", isPastDue && "font-semibold text-destructive")}>{formatDateTime(booking.requestedPickupTime)}</span>
                    <span>{containerSizeLabels[booking.containerSize]}</span>
                    <span>{tripTypeLabels[booking.tripType]}</span>
                    {booking.bookingNumber ? <span className="font-mono">{booking.bookingNumber}</span> : null}
                    {isPastDue ? <Badge className="border-destructive/30 bg-destructive/5 text-destructive">Past requested time</Badge> : null}
                  </div>
                </Link>
              );
            })}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function DriverAvailability({ snapshot }: { snapshot: DispatcherOperationsSnapshot }) {
  const visibleDrivers = snapshot.drivers.slice(0, 6);

  return (
    <Card className="min-w-0" aria-labelledby="driver-availability-title">
      <CardHeader className="flex-row items-start justify-between gap-4 space-y-0 p-5 sm:p-6">
        <div>
          <div className="flex items-center gap-2">
            <Users className="h-4 w-4 text-primary" aria-hidden="true" />
            <h3 id="driver-availability-title" className="text-base font-semibold leading-none text-foreground">Driver availability</h3>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">Assignment, workload, vehicle, and last activity.</p>
        </div>
        <Badge variant="outline">{snapshot.drivers.filter((driver) => driver.state === "AVAILABLE").length} ready</Badge>
      </CardHeader>
      <CardContent className="p-0">
        {visibleDrivers.length === 0 ? (
          <div className="border-t border-border px-5 py-5 sm:px-6">
            <EmptyState title="No active drivers" description="Active Driver accounts will appear in this availability list." />
          </div>
        ) : (
          <div className="divide-y divide-border border-t border-border">
            {visibleDrivers.map((driver) => {
              const row = (
                <>
                  <div className="flex min-w-0 items-start gap-3">
                    <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-muted text-foreground">
                      <span className="text-xs font-bold uppercase">{driver.driverName.slice(0, 2)}</span>
                    </div>
                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold text-foreground">{driver.driverName}</p>
                      <p className="mt-0.5 truncate text-xs text-muted-foreground">
                        {driver.tripReference ?? (driver.nextStart ? `Next trip ${formatTime(driver.nextStart)}` : "Ready for assignment")}
                      </p>
                      <div className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-[11px] text-muted-foreground">
                        <span>{driver.todayJobs} job{driver.todayJobs === 1 ? "" : "s"} today</span>
                        <span>{driver.currentTruck ?? "No truck assigned"}</span>
                        <span>{driver.lastActivityAt ? formatRelativeTime(driver.lastActivityAt) : driver.nextStart ? `Starts ${formatTime(driver.nextStart)}` : "No current trip"}</span>
                      </div>
                    </div>
                  </div>
                  <span className={cn("inline-flex w-fit rounded-full border px-2 py-1 text-[10px] font-semibold uppercase tracking-wide", availabilityClasses[driver.state])}>
                    {availabilityLabels[driver.state]}
                  </span>
                </>
              );

              return driver.tripId ? (
                <Link
                  key={driver.driverId}
                  to={`/dispatch/trips/${driver.tripId}`}
                  className="grid gap-3 px-5 py-3.5 transition-colors duration-200 hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring motion-reduce:transition-none sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center sm:px-6"
                >
                  {row}
                </Link>
              ) : (
                <div key={driver.driverId} className="grid gap-3 px-5 py-3.5 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center sm:px-6">
                  {row}
                </div>
              );
            })}
          </div>
        )}
        {snapshot.drivers.length > visibleDrivers.length ? (
          <div className="border-t border-border px-5 py-3 text-xs text-muted-foreground sm:px-6">
            Showing 6 of {snapshot.drivers.length} active drivers
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function TruckAvailability({ snapshot }: { snapshot: DispatcherOperationsSnapshot }) {
  const visibleTrucks = snapshot.trucks.slice(0, 6);

  return (
    <Card className="min-w-0" aria-labelledby="truck-availability-title">
      <CardHeader className="flex-row items-start justify-between gap-4 space-y-0 p-5 sm:p-6">
        <div>
          <div className="flex items-center gap-2">
            <Truck className="h-4 w-4 text-primary" aria-hidden="true" />
            <h3 id="truck-availability-title" className="text-base font-semibold leading-none text-foreground">Truck availability</h3>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">Fleet assignment and current operating state.</p>
        </div>
        <Badge variant="outline">{snapshot.trucks.filter((truck) => truck.state === "AVAILABLE").length} ready</Badge>
      </CardHeader>
      <CardContent className="p-0">
        {visibleTrucks.length === 0 ? (
          <div className="border-t border-border px-5 py-5 sm:px-6">
            <EmptyState title="No trucks found" description="Truck assets will appear here when they are available to dispatch." />
          </div>
        ) : (
          <div className="divide-y divide-border border-t border-border">
            {visibleTrucks.map((truck) => {
              const row = (
                <>
                  <div className="flex min-w-0 items-start gap-3">
                    <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-muted text-foreground">
                      <Truck className="h-4 w-4" aria-hidden="true" />
                    </div>
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                        <p className="font-mono text-sm font-semibold text-foreground">{truck.assetCode}</p>
                        {truck.plateNo && truck.plateNo !== truck.assetCode ? <span className="text-xs text-muted-foreground">{truck.plateNo}</span> : null}
                      </div>
                      <p className="mt-0.5 truncate text-xs text-muted-foreground">{truck.tripReference ?? "No active assignment"}</p>
                      <div className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-[11px] text-muted-foreground">
                        <span>{truck.driverName ?? "No driver assigned"}</span>
                        <span>{formatRelativeTime(truck.lastActivityAt)}</span>
                      </div>
                    </div>
                  </div>
                  <span className={cn("inline-flex w-fit rounded-full border px-2 py-1 text-[10px] font-semibold uppercase tracking-wide", truckAvailabilityClasses[truck.state])}>
                    {truckAvailabilityLabels[truck.state]}
                  </span>
                </>
              );

              return truck.tripId ? (
                <Link
                  key={truck.truckId}
                  to={`/dispatch/trips/${truck.tripId}`}
                  className="grid gap-3 px-5 py-3.5 transition-colors duration-200 hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring motion-reduce:transition-none sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center sm:px-6"
                >
                  {row}
                </Link>
              ) : (
                <div key={truck.truckId} className="grid gap-3 px-5 py-3.5 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center sm:px-6">
                  {row}
                </div>
              );
            })}
          </div>
        )}
        {snapshot.trucks.length > visibleTrucks.length ? (
          <div className="border-t border-border px-5 py-3 text-xs text-muted-foreground sm:px-6">
            Showing 6 of {snapshot.trucks.length} trucks
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function WorkflowActions({ kpis }: { kpis: DispatchDashboardKpis }) {
  const actions: {
    title: string;
    detail: string;
    to: string;
    icon: LucideIcon;
    count?: number;
  }[] = [
    {
      title: "Review and approve requests",
      detail: "Validate customer booking details and ATW readiness.",
      to: "/dispatch/requests",
      icon: ClipboardCheck,
      count: kpis.pendingShipmentRequests
    },
    {
      title: "Schedule and assign resources",
      detail: "Place approved work on the board and confirm driver and truck.",
      to: "/dispatch/board",
      icon: UserCheck,
      count: kpis.approvedShipmentRequests
    },
    {
      title: "Monitor trip execution",
      detail: "Follow active movements and intervene on schedule exceptions.",
      to: "/dispatch/trips",
      icon: MapPin,
      count: kpis.activeTrips
    },
    {
      title: "Verify trip documents",
      detail: "Clear rejected or incomplete paperwork before closeout.",
      to: "/dispatch/documents",
      icon: FileCheck2,
      count: kpis.incompleteDocumentAlerts
    }
  ];

  return (
    <Card aria-labelledby="workflow-actions-title">
      <CardHeader className="p-5 sm:p-6">
        <h3 id="workflow-actions-title" className="text-base font-semibold leading-none text-foreground">Workflow actions</h3>
        <p className="mt-1 text-sm text-muted-foreground">Move work through the dispatch lifecycle in operating order.</p>
      </CardHeader>
      <CardContent className="grid gap-3 px-5 pb-5 sm:grid-cols-2 sm:px-6 sm:pb-6 xl:grid-cols-4">
        {actions.map((action, index) => (
          <Link
            key={action.title}
            to={action.to}
            className="group flex min-h-32 flex-col rounded-xl border border-border bg-muted/20 p-4 transition-all duration-200 hover:border-primary/30 hover:bg-muted/50 hover:shadow-md hover:shadow-primary/5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring active:scale-[0.99] motion-reduce:transition-none"
          >
            <div className="flex items-start justify-between gap-3">
              <div className="flex items-center gap-2">
                <span className="font-mono text-[10px] font-semibold text-muted-foreground">0{index + 1}</span>
                <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-primary">
                  <action.icon className="h-4 w-4" aria-hidden="true" />
                </div>
              </div>
              <span className="font-mono text-sm font-semibold tabular-nums text-foreground">{action.count ?? 0}</span>
            </div>
            <p className="mt-4 text-sm font-semibold text-foreground">{action.title}</p>
            <p className="mt-1 text-xs leading-5 text-muted-foreground">{action.detail}</p>
            <ArrowRight className="mt-auto h-4 w-4 self-end text-muted-foreground transition-transform duration-200 group-hover:translate-x-1 group-hover:text-primary motion-reduce:transition-none" aria-hidden="true" />
          </Link>
        ))}
      </CardContent>
    </Card>
  );
}

export default function DispatcherOperationalDashboard({ kpis, snapshot, loading, error, onRetry }: Props) {
  return (
    <div className="space-y-4">
      {error ? (
        <div className="flex flex-col gap-3 rounded-2xl border border-destructive/30 bg-destructive/5 px-4 py-3 text-sm text-foreground sm:flex-row sm:items-center sm:justify-between" role="status">
          <span>{error}</span>
          <Button variant="outline" size="sm" onClick={onRetry}>Retry operational data</Button>
        </div>
      ) : null}

      {loading && !snapshot ? (
        <div className="surface-card p-6"><LoadingSkeleton rows={6} /></div>
      ) : snapshot ? (
        <>
          <OperationalStrip kpis={kpis} />
          <div className="grid min-w-0 gap-4 xl:grid-cols-[minmax(0,1.6fr)_minmax(20rem,0.8fr)]">
            <NeedsAttention snapshot={snapshot} kpis={kpis} />
            <DispatchHealth kpis={kpis} />
          </div>
          <div className="grid min-w-0 gap-4 xl:grid-cols-[minmax(0,1.4fr)_minmax(20rem,0.9fr)]">
            <TodayTimeline snapshot={snapshot} />
            <UpcomingBookings snapshot={snapshot} />
          </div>
          <div className="grid min-w-0 gap-4 xl:grid-cols-2">
            <DriverAvailability snapshot={snapshot} />
            <TruckAvailability snapshot={snapshot} />
          </div>
          <WorkflowActions kpis={kpis} />
          <div className="flex flex-wrap items-center justify-between gap-3 px-1 text-xs text-muted-foreground">
            <span className="inline-flex items-center gap-1.5"><Warehouse className="h-3.5 w-3.5" aria-hidden="true" />Database is the source of truth</span>
            <span>{formatUpdatedAt(snapshot.loadedAt)}</span>
          </div>
        </>
      ) : null}
    </div>
  );
}
