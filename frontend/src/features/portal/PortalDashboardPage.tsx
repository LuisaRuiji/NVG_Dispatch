import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import { Button, buttonVariants } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import type { CustomerShipmentListItem, ShipmentRequestListItem } from "./types";
import { statusLabels } from "@/features/dispatch/types";

export default function PortalDashboardPage() {
  const { toasts, show } = useToast();
  const [loading, setLoading] = useState(true);
  const [requestCount, setRequestCount] = useState(0);
  const [shipmentCount, setShipmentCount] = useState(0);
  const [recentShipments, setRecentShipments] = useState<CustomerShipmentListItem[]>([]);

  const loadDashboard = async () => {
    try {
      setLoading(true);
      const [requests, shipments] = await Promise.all([
        api<PagedResult<ShipmentRequestListItem>>("/api/portal/requests?page=1&pageSize=1", { method: "GET" }),
        api<PagedResult<CustomerShipmentListItem>>("/api/portal/shipments?page=1&pageSize=5", { method: "GET" })
      ]);
      setRequestCount(requests.totalCount ?? 0);
      setShipmentCount(shipments.totalCount ?? 0);
      setRecentShipments(shipments.items ?? []);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load dashboard.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadDashboard();
  }, []);

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Customer Portal"
        description="Track shipment requests and active deliveries."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/portal/dashboard" className="text-muted-foreground hover:text-foreground">
              Portal
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">Dashboard</span>
          </nav>
        }
        actions={
          <Button variant="outline" size="sm" onClick={loadDashboard} disabled={loading}>
            Refresh
          </Button>
        }
      />

      <div className="grid gap-4 md:grid-cols-2">
        <div className="surface-card p-5">
          <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">Shipment Requests</p>
          <p className="mt-3 text-3xl font-semibold text-foreground">
            {loading ? "—" : requestCount}
          </p>
          <p className="mt-2 text-sm text-muted-foreground">Drafts and submitted requests.</p>
          <div className="mt-4 flex items-center gap-3">
            <Link to="/portal/requests" className={buttonVariants({ size: "sm" })}>
              View Requests
            </Link>
            <Link to="/portal/requests/new" className={buttonVariants({ variant: "outline", size: "sm" })}>
              New Request
            </Link>
          </div>
        </div>
        <div className="surface-card p-5">
          <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">Shipments</p>
          <p className="mt-3 text-3xl font-semibold text-foreground">
            {loading ? "—" : shipmentCount}
          </p>
          <p className="mt-2 text-sm text-muted-foreground">Active and delivered shipments.</p>
          <div className="mt-4">
            <Link to="/portal/shipments" className={buttonVariants({ size: "sm" })}>
              View Shipments
            </Link>
          </div>
        </div>
      </div>

      <div className="surface-card p-6">
        <div className="flex items-center justify-between gap-3">
          <div>
            <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">Recent Shipments</p>
            <h3 className="mt-2 text-lg font-semibold text-foreground">Latest activity</h3>
          </div>
          <Link to="/portal/shipments" className={buttonVariants({ variant: "outline", size: "sm" })}>
            View all
          </Link>
        </div>

        {loading ? (
          <div className="mt-4">
            <LoadingSkeleton rows={4} />
          </div>
        ) : recentShipments.length === 0 ? (
          <div className="mt-6">
            <EmptyState title="No shipments yet" description="Your converted requests will appear here." />
          </div>
        ) : (
          <div className="mt-4 grid gap-3">
            {recentShipments.map((shipment) => (
              <div key={shipment.tripId} className="rounded-lg border border-border/50 bg-muted/10 px-4 py-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <p className="text-sm font-semibold text-foreground">
                      Shipment {shipment.tripId.slice(0, 8)}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {shipment.pickupLocation} → {shipment.dropoffLocation}
                    </p>
                  </div>
                  <div className="flex items-center gap-3">
                    <StatusBadge status={statusLabels[shipment.status] ?? shipment.status} />
                    <Link
                      to={`/portal/shipments/${shipment.tripId}`}
                      className={buttonVariants({ variant: "outline", size: "sm" })}
                    >
                      View
                    </Link>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
