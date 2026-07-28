import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import type { DispatchTripListItem, TripStatus } from "./types";
import {
  Calendar,
  Clock,
  MapPin,
  RefreshCw,
  Truck,
  ChevronRight,
  Play
} from "lucide-react";

type StatusScope = "ACTIVE" | "ALL";

type TabOption = {
  key: StatusScope;
  label: string;
};

const driverActionMap: Record<TripStatus, { endpoint: string; label: string } | null> = {
  DRAFT: null,
  DISPATCHED: { endpoint: "start", label: "Start Pickup" },
  ENROUTE_PICKUP: { endpoint: "arrive-pickup", label: "Arrive Pickup" },
  AT_PICKUP: { endpoint: "confirm-loaded", label: "Confirm Loaded" },
  LOADED: { endpoint: "depart-pickup", label: "Depart Pickup" },
  ENROUTE_DROPOFF: { endpoint: "arrive-dropoff", label: "Arrive Dropoff" },
  AT_DROPOFF: { endpoint: "confirm-delivery", label: "Confirm Delivery" },
  DELIVERED: null,
  CLOSED: null,
  CANCELLED: null,
  ON_HOLD: null,
  FAILED_ATTEMPT: null
};

export default function MyTripsPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();

  const [loading, setLoading] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);

  const [trips, setTrips] = useState<DispatchTripListItem[]>([]);
  const [statusScope, setStatusScope] = useState<StatusScope>("ACTIVE");
  const [selectedDate, setSelectedDate] = useState<string>(() => {
    return new Date().toISOString().split("T")[0];
  });

  const tabs: TabOption[] = useMemo(
    () => [
      { key: "ACTIVE", label: "Active Duties" },
      { key: "ALL", label: "All Schedules" }
    ],
    []
  );

  const loadTrips = async () => {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      params.set("scope", statusScope);
      params.set("page", "1");
      params.set("pageSize", "50");

      if (statusScope === "ALL" && selectedDate) {
        params.set("from", `${selectedDate}T00:00:00Z`);
        params.set("to", `${selectedDate}T23:59:59.999Z`);
      }

      const result = await api<PagedResult<DispatchTripListItem>>(
        `/api/dispatch/my-trips?${params.toString()}`,
        { method: "GET" }
      );
      setTrips(result.items ?? []);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load trips.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTrips();
  }, [statusScope, selectedDate]);

  const handleQuickAction = async (tripId: string, endpoint: string, rowVersion: string) => {
    try {
      setActionLoading(true);
      await api(`/api/dispatch/trips/${tripId}/${endpoint}`, {
        method: "POST",
        body: JSON.stringify({
          eventAt: new Date().toISOString(),
          rowVersion,
          remarks: null
        })
      });
      show("Trip status updated successfully.", "success");
      void loadTrips();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to update trip status.", "error");
      void loadTrips(); // Automatically pull down latest database state and rowVersion
    } finally {
      setActionLoading(false);
    }
  };

  // KPIs
  const kpis = useMemo(() => {
    const total = trips.length;
    const inProgress = trips.filter(
      (t) =>
        t.status === "DISPATCHED" ||
        t.status === "ENROUTE_PICKUP" ||
        t.status === "AT_PICKUP" ||
        t.status === "LOADED" ||
        t.status === "ENROUTE_DROPOFF" ||
        t.status === "AT_DROPOFF"
    ).length;
    const completed = trips.filter((t) => t.status === "DELIVERED" || t.status === "CLOSED").length;
    return { total, inProgress, completed };
  }, [trips]);

  return (
    <div className="space-y-6 max-w-5xl mx-auto">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="My Trips"
        description="View your assigned schedules, track pickups, and manage transit lifecycles."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <span className="text-muted-foreground">Driver Portal</span>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground font-medium">My Trips</span>
          </nav>
        }
      />

      {/* Date and scope filters */}
      <div className="grid gap-4 sm:flex sm:items-center sm:justify-between p-4 rounded-2xl border bg-card shadow-sm">
        <div className="flex gap-2.5">
          {tabs.map((tab) => (
            <button
              key={tab.key}
              onClick={() => setStatusScope(tab.key)}
              className={`rounded-xl px-4 py-2 text-xs font-semibold uppercase tracking-wider transition-all duration-200 ${
                statusScope === tab.key
                  ? "bg-slate-900 text-white shadow-sm"
                  : "border text-muted-foreground hover:bg-slate-50 hover:text-foreground"
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2 rounded-xl border bg-slate-50/50 px-3 py-1.5 shadow-inner">
            <Calendar className="h-4 w-4 text-slate-500" />
            <input
              type="date"
              value={selectedDate}
              onChange={(e) => setSelectedDate(e.target.value)}
              className="bg-transparent text-sm font-semibold text-slate-700 outline-none border-none cursor-pointer"
            />
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setSelectedDate(new Date().toISOString().split("T")[0])}
            className="rounded-xl font-medium"
          >
            Today
          </Button>
          <Button
            variant="outline"
            size="icon"
            onClick={loadTrips}
            disabled={loading}
            className="rounded-xl"
          >
            <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
          </Button>
        </div>
      </div>



      {/* KPI summaries */}
      <div className="grid grid-cols-3 gap-4">
        <Card className="rounded-2xl border bg-card shadow-sm">
          <CardHeader className="p-4 pb-2">
            <CardTitle className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Total Trips</CardTitle>
          </CardHeader>
          <CardContent className="p-4 pt-0">
            <p className="text-2xl font-bold text-slate-900">{kpis.total}</p>
          </CardContent>
        </Card>

        <Card className="rounded-2xl border bg-card shadow-sm">
          <CardHeader className="p-4 pb-2">
            <CardTitle className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">In Progress</CardTitle>
          </CardHeader>
          <CardContent className="p-4 pt-0">
            <p className="text-2xl font-bold text-indigo-600">{kpis.inProgress}</p>
          </CardContent>
        </Card>

        <Card className="rounded-2xl border bg-card shadow-sm">
          <CardHeader className="p-4 pb-2">
            <CardTitle className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Completed</CardTitle>
          </CardHeader>
          <CardContent className="p-4 pt-0">
            <p className="text-2xl font-bold text-emerald-600">{kpis.completed}</p>
          </CardContent>
        </Card>
      </div>

      {/* Trip Lists */}
      {loading && trips.length === 0 ? (
        <LoadingSkeleton rows={6} />
      ) : trips.length === 0 ? (
        <EmptyState
          title="No Trips Found"
          description={`You do not have any trips scheduled on ${new Date(selectedDate).toLocaleDateString(undefined, { dateStyle: "long" })}.`}
        />
      ) : (
        <div className="grid gap-4">
          {trips.map((trip) => {
            const nextAction = driverActionMap[trip.status];
            return (
              <div
                key={trip.id}
                className="group relative rounded-2xl border bg-card p-5 hover:border-slate-300 hover:shadow-md transition-all duration-300 flex flex-col justify-between"
              >
                <div>
                  <div className="flex flex-wrap items-center justify-between gap-3 border-b pb-3.5 mb-4">
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <span className="h-2 w-2 rounded-full bg-primary animate-pulse" />
                        <h3 className="text-sm font-bold text-slate-800">
                          Trip {trip.id.slice(0, 8).toUpperCase()}
                        </h3>
                        <StatusBadge status={trip.status} />
                      </div>
                      <p className="text-xs text-muted-foreground">
                        Customer: <span className="font-semibold text-slate-700">{trip.customer?.name ?? "-"}</span>
                      </p>
                    </div>

                    {/* Quick action button directly in the list card */}
                    {nextAction && !actionLoading && (
                      <Button
                        size="sm"
                        onClick={(e) => {
                          e.stopPropagation();
                          void handleQuickAction(trip.id, nextAction.endpoint, trip.rowVersion);
                        }}
                        className="bg-primary text-primary-foreground font-semibold rounded-xl text-xs gap-1.5 shadow-sm shadow-primary/10 transition hover:bg-primary/90"
                      >
                        <Play className="h-3 w-3 fill-current" />
                        {nextAction.label}
                      </Button>
                    )}
                  </div>

                  {/* Horizontal visual route path */}
                  <div className="grid gap-4 md:grid-cols-2 text-sm pt-1">
                    <div className="relative pl-6">
                      <div className="absolute left-0 top-1 text-blue-500">
                        <MapPin className="h-4 w-4" />
                      </div>
                      <p className="text-xs uppercase tracking-wider font-semibold text-muted-foreground">Pickup Location</p>
                      <p className="mt-0.5 font-bold text-slate-800 truncate">{trip.pickupLocation ?? "-"}</p>
                      <p className="text-xs text-muted-foreground flex items-center gap-1 mt-1">
                        <Clock className="h-3.5 w-3.5 shrink-0" />
                        {trip.pickupScheduledAt
                          ? new Date(trip.pickupScheduledAt).toLocaleString(undefined, { dateStyle: "short", timeStyle: "short" })
                          : "Unscheduled"}
                      </p>
                    </div>

                    <div className="relative pl-6 md:border-l">
                      <div className="absolute left-0 md:left-6 top-1 text-rose-500">
                        <MapPin className="h-4 w-4" />
                      </div>
                      <p className="text-xs uppercase tracking-wider font-semibold text-muted-foreground">Dropoff Location</p>
                      <p className="mt-0.5 font-bold text-slate-800 truncate">{trip.dropoffLocation ?? "-"}</p>
                      <p className="text-xs text-muted-foreground flex items-center gap-1 mt-1">
                        <Clock className="h-3.5 w-3.5 shrink-0" />
                        {trip.dropoffScheduledAt
                          ? new Date(trip.dropoffScheduledAt).toLocaleString(undefined, { dateStyle: "short", timeStyle: "short" })
                          : "Unscheduled"}
                      </p>
                    </div>
                  </div>
                </div>

                <div className="mt-5 pt-3.5 border-t flex items-center justify-between text-xs text-muted-foreground">
                  <div className="flex items-center gap-1.5 font-medium text-slate-700">
                    <Truck className="h-4 w-4 text-slate-500" />
                    <span>Truck: {trip.truckAssetCode ?? "-"}</span>
                  </div>

                  <button
                    onClick={() => nav(`/dispatch/my-trips/${trip.id}`)}
                    className="flex items-center gap-1 font-semibold text-primary hover:text-primary/80 transition-all uppercase tracking-wider"
                  >
                    Open View
                    <ChevronRight className="h-4 w-4 transition group-hover:translate-x-0.5" />
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
