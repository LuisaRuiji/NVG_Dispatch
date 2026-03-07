import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import type { DispatchTripListItem } from "./types";
import { statusLabels } from "./types";

type StatusScope = "ACTIVE" | "ALL";

type TabOption = {
  key: StatusScope;
  label: string;
};

export default function MyTripsPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();

  const [loading, setLoading] = useState(false);
  const [trips, setTrips] = useState<DispatchTripListItem[]>([]);
  const [statusScope, setStatusScope] = useState<StatusScope>("ACTIVE");

  const tabs: TabOption[] = useMemo(
    () => [
      { key: "ACTIVE", label: "Active" },
      { key: "ALL", label: "All" }
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
  }, [statusScope]);

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="My Trips"
        description="Your assigned trips and next steps."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/dispatch/board" className="text-muted-foreground hover:text-foreground">
              Dispatch
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">My Trips</span>
          </nav>
        }
      />

      <div className="surface-card p-4">
        <div className="flex flex-wrap items-center gap-3">
          {tabs.map((tab) => (
            <button
              key={tab.key}
              onClick={() => setStatusScope(tab.key)}
              className={`rounded-full border px-4 py-2 text-xs font-semibold uppercase tracking-[0.15em] transition ${
                statusScope === tab.key
                  ? "border-slate-900 bg-slate-900 text-white"
                  : "border-border/60 text-muted-foreground hover:border-slate-400 hover:text-foreground"
              }`}
            >
              {tab.label}
            </button>
          ))}
          <Button variant="outline" size="sm" onClick={loadTrips} disabled={loading}>
            Refresh
          </Button>
        </div>
      </div>

      {loading && trips.length === 0 ? (
        <LoadingSkeleton rows={6} />
      ) : trips.length === 0 ? (
        <EmptyState title="No trips assigned" description="You currently have no trips." />
      ) : (
        <div className="grid gap-4">
          {trips.map((trip) => (
            <div key={trip.id} className="surface-card p-5">
              <div className="flex items-start justify-between gap-3">
                <div className="space-y-2">
                  <div className="flex items-center gap-3">
                    <h3 className="text-base font-semibold text-foreground">
                      Trip {trip.id.slice(0, 8)}
                    </h3>
                    <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
                  </div>
                  <p className="text-sm text-muted-foreground">
                    Customer: {trip.customer?.name ?? "-"}
                  </p>
                </div>
                <Button variant="outline" size="sm" onClick={() => nav(`/dispatch/my-trips/${trip.id}`)}>
                  Open
                </Button>
              </div>

              <div className="mt-4 grid gap-3 text-sm">
                <div className="rounded-lg border border-border/60 bg-muted/20 px-4 py-3">
                  <p className="text-xs uppercase text-muted-foreground">Pickup</p>
                  <p className="mt-1 text-foreground">{trip.pickupLocation ?? "-"}</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {trip.pickupScheduledAt ? new Date(trip.pickupScheduledAt).toLocaleString() : "Unscheduled"}
                  </p>
                </div>
                <div className="rounded-lg border border-border/60 bg-muted/20 px-4 py-3">
                  <p className="text-xs uppercase text-muted-foreground">Dropoff</p>
                  <p className="mt-1 text-foreground">{trip.dropoffLocation ?? "-"}</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {trip.dropoffScheduledAt ? new Date(trip.dropoffScheduledAt).toLocaleString() : "Unscheduled"}
                  </p>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
