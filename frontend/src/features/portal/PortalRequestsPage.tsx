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
import { containerSizeLabels, tripTypeLabels, type ShipmentRequestListItem } from "./types";

export default function PortalRequestsPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const [loading, setLoading] = useState(false);
  const [requests, setRequests] = useState<ShipmentRequestListItem[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const loadRequests = async () => {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      params.set("page", String(page));
      params.set("pageSize", String(pageSize));
      const result = await api<PagedResult<ShipmentRequestListItem>>(
        `/api/portal/requests?${params.toString()}`,
        { method: "GET" }
      );
      setRequests(result.items ?? []);
      setTotalCount(result.totalCount ?? 0);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load requests.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRequests();
  }, [page]);

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Shipment Requests"
        description="Create and track your shipment requests."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/portal/dashboard" className="text-muted-foreground hover:text-foreground">
              Portal
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">Requests</span>
          </nav>
        }
        actions={
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" onClick={loadRequests} disabled={loading}>
              Refresh
            </Button>
            <Button size="sm" onClick={() => nav("/portal/requests/new")}>
              New Request
            </Button>
          </div>
        }
      />

      {loading && requests.length === 0 ? (
        <LoadingSkeleton rows={6} />
      ) : requests.length === 0 ? (
        <EmptyState
          title="No shipment requests"
          description="Create your first shipment request to get started."
        />
      ) : (
        <div className="surface-card p-4">
          <DataTable>
            <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-4 py-3 text-left">Request</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-left">Pickup</th>
                <th className="px-4 py-3 text-left">Dropoff</th>
                <th className="px-4 py-3 text-left">Requested</th>
                <th className="px-4 py-3 text-left">Docs</th>
                <th className="px-4 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              {requests.map((request) => {
                const rawTime = request.requestedPickupTime;
                let reqDate: Date | null = null;
                if (rawTime) {
                  const clean = rawTime.replace("Z", "");
                  const [datePart, timePart] = clean.split("T");
                  if (datePart && timePart) {
                    const [y, m, d] = datePart.split("-").map(Number);
                    const [h, min] = timePart.split(":").map(Number);
                    if (!isNaN(y) && !isNaN(m) && !isNaN(d) && !isNaN(h) && !isNaN(min)) {
                      reqDate = new Date(y, m - 1, d, h, min);
                    }
                  }
                }
                const isPastDue = reqDate ? reqDate.getTime() < Date.now() : false;
                const approvedOverdue = request.status === "APPROVED" && isPastDue;
                const submittedOverdue = request.status === "SUBMITTED" && isPastDue;
                return (
                  <tr key={request.id} className="border-t border-border/60">
                    <td className="px-4 py-3 text-sm font-semibold text-foreground">
                      <div>{request.id.slice(0, 8)}</div>
                      <div className="mt-1 text-xs font-normal text-muted-foreground">
                        {containerSizeLabels[request.containerSize]} / {tripTypeLabels[request.tripType]}
                      </div>
                      {request.containerNumber ? (
                        <div className="mt-0.5 text-xs font-normal text-muted-foreground">
                          {request.containerNumber}
                        </div>
                      ) : null}
                    </td>
                    <td className="px-4 py-3">
                      <StatusBadge status={request.status} />
                    </td>
                    <td className="px-4 py-3 text-sm text-muted-foreground">{request.pickupLocation}</td>
                    <td className="px-4 py-3 text-sm text-muted-foreground">{request.dropoffLocation}</td>
                    <td className="px-4 py-3 text-sm text-muted-foreground">
                      <div>
                        {reqDate
                          ? reqDate.toLocaleString(undefined, {
                              year: "numeric",
                              month: "2-digit",
                              day: "2-digit",
                              hour: "2-digit",
                              minute: "2-digit",
                              second: "2-digit",
                              hour12: true
                            })
                          : "Unscheduled"}
                      </div>
                      {submittedOverdue ? (
                        <span className="mt-1 inline-flex items-center gap-1 rounded-md border border-amber-200 bg-amber-50 px-1.5 py-0.5 text-[10px] font-medium text-amber-800">
                          <span className="h-1.5 w-1.5 rounded-full bg-amber-500 animate-pulse"></span>
                          Review In Progress (Priority Queue)
                        </span>
                      ) : null}
                      {approvedOverdue ? (
                        <span className="mt-1 inline-flex items-center gap-1 rounded-md border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-[10px] font-semibold text-amber-800">
                          <span className="h-1.5 w-1.5 rounded-full bg-amber-600 animate-pulse"></span>
                          Priority Driver Re-allocation
                        </span>
                      ) : null}
                    </td>
                    <td className="px-4 py-3 text-sm text-muted-foreground">{request.documentsCount}</td>
                    <td className="px-4 py-3 text-right">
                      <Button variant="outline" size="sm" onClick={() => nav(`/portal/requests/${request.id}`)}>
                        View
                      </Button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </DataTable>

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
