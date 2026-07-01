import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import type { CustomerShipmentListItem } from "./types";
import { statusLabels } from "@/features/dispatch/types";

export default function PortalShipmentsPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const [loading, setLoading] = useState(false);
  const [shipments, setShipments] = useState<CustomerShipmentListItem[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const loadShipments = async () => {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      params.set("page", String(page));
      params.set("pageSize", String(pageSize));
      const result = await api<PagedResult<CustomerShipmentListItem>>(
        `/api/portal/shipments?${params.toString()}`,
        { method: "GET" }
      );
      setShipments(result.items ?? []);
      setTotalCount(result.totalCount ?? 0);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load shipments.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadShipments();
  }, [page]);

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Shipments"
        description="Track dispatched and delivered trips."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/portal/dashboard" className="text-muted-foreground hover:text-foreground">
              Portal
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">Shipments</span>
          </nav>
        }
        actions={
          <Button variant="outline" size="sm" onClick={loadShipments} disabled={loading}>
            Refresh
          </Button>
        }
      />

      {loading && shipments.length === 0 ? (
        <LoadingSkeleton rows={6} />
      ) : shipments.length === 0 ? (
        <EmptyState title="No shipments yet" description="Converted trips will appear here." />
      ) : (
        <div className="surface-card p-3 md:p-4">
          <div className="grid gap-3 md:hidden">
            {shipments.map((shipment) => (
              <button
                key={shipment.tripId}
                className="rounded-2xl border border-border bg-card p-4 text-left shadow-card transition-all duration-200 active:scale-[0.99]"
                onClick={() => nav(`/portal/shipments/${shipment.tripId}`)}
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">Container</p>
                    <p className="mt-1 font-mono text-lg font-semibold text-foreground">
                      {shipment.containerNumber ?? "Container pending"}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">Trip {shipment.tripId.slice(0, 8)}</p>
                  </div>
                  <StatusBadge status={statusLabels[shipment.status] ?? shipment.status} />
                </div>
                <div className="mt-3 rounded-xl border border-border/60 bg-muted/20 px-3 py-2 text-sm text-muted-foreground">
                  <p className="font-medium text-foreground">{shipment.pickupLocation}</p>
                  <p className="mt-1">{shipment.dropoffLocation}</p>
                </div>
                <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
                  <span className="rounded-full border border-border bg-muted px-2 py-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    POD {shipment.podState}
                  </span>
                  <span className="inline-flex h-10 items-center rounded-full bg-primary px-4 text-sm font-semibold text-primary-foreground">
                    View Tracking
                  </span>
                </div>
              </button>
            ))}
          </div>
          <div className="hidden md:block">
          <DataTable>
            <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-4 py-3 text-left">Shipment</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-left">Pickup</th>
                <th className="px-4 py-3 text-left">Dropoff</th>
                <th className="px-4 py-3 text-left">Pickup Time</th>
                <th className="px-4 py-3 text-left">Delivered</th>
                <th className="px-4 py-3 text-left">POD</th>
                <th className="px-4 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              {shipments.map((shipment) => (
                <tr key={shipment.tripId} className="border-t border-border/60">
                  <td className="px-4 py-3 text-sm font-semibold text-foreground">
                    <div>{shipment.containerNumber ?? shipment.tripId.slice(0, 8)}</div>
                    <div className="text-xs font-normal text-muted-foreground">{shipment.tripId.slice(0, 8)}</div>
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge status={statusLabels[shipment.status] ?? shipment.status} />
                  </td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">{shipment.pickupLocation}</td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">{shipment.dropoffLocation}</td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">
                    {shipment.pickupTime ? new Date(shipment.pickupTime).toLocaleString() : "--"}
                  </td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">
                    {shipment.deliveredTime ? new Date(shipment.deliveredTime).toLocaleString() : "--"}
                  </td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">{shipment.podState}</td>
                  <td className="px-4 py-3 text-right">
                    <Button variant="outline" size="sm" onClick={() => nav(`/portal/shipments/${shipment.tripId}`)}>
                      View
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </DataTable>
          </div>

          <div className="mt-4 flex items-center justify-between text-xs text-muted-foreground">
            <span>
              Page {page} of {totalPages}
            </span>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                Prev
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
