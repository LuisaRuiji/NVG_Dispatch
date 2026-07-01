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

type RequestStatus = "SUBMITTED" | "APPROVED" | "REJECTED" | "CONVERTED_TO_TRIP";
type ContainerSize = "TWENTY_FT" | "FORTY_FT" | "FORTY_HC";
type TripType = "PORT_PICKUP" | "PORT_DROPOFF" | "YARD_TRANSFER" | "LONG_HAUL";

type DispatchRequestItem = {
  id: string;
  customerId: string;
  customerName: string;
  pickupLocation: string;
  dropoffLocation: string;
  requestedPickupTime?: string | null;
  containerSize: ContainerSize;
  tripType: TripType;
  containerNumber?: string | null;
  shippingLine?: string | null;
  bookingNumber?: string | null;
  documentsCount: number;
  createdAt: string;
  status: RequestStatus;
  tripId?: string | null;
};

type RejectModal = { id: string; remarks: string } | null;

const containerSizeLabels: Record<ContainerSize, string> = {
  TWENTY_FT: "20 ft",
  FORTY_FT: "40 ft",
  FORTY_HC: "40 HC"
};

const tripTypeLabels: Record<TripType, string> = {
  PORT_PICKUP: "Port Pickup",
  PORT_DROPOFF: "Port Dropoff",
  YARD_TRANSFER: "Yard Transfer",
  LONG_HAUL: "Long Haul"
};

export default function DispatchRequestsPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const [loading, setLoading] = useState(false);
  const [requests, setRequests] = useState<DispatchRequestItem[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);
  const [rejectModal, setRejectModal] = useState<RejectModal>(null);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const loadQueue = async () => {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      params.set("page", String(page));
      params.set("pageSize", String(pageSize));
      const result = await api<PagedResult<DispatchRequestItem>>(
        `/api/dispatch/requests?${params.toString()}`,
        { method: "GET" }
      );
      const items = (result.items ?? []).map((item) => ({ ...item, status: "SUBMITTED" as RequestStatus }));
      setRequests(items);
      setTotalCount(result.totalCount ?? 0);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load queue.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadQueue();
  }, [page]);

  const updateRequest = (id: string, patch: Partial<DispatchRequestItem>) => {
    setRequests((prev) => prev.map((item) => (item.id === id ? { ...item, ...patch } : item)));
  };

  const handleApprove = async (item: DispatchRequestItem) => {
    try {
      await api(`/api/dispatch/requests/${item.id}/approve`, { method: "POST" });
      updateRequest(item.id, { status: "APPROVED" });
      show("Request approved.", "success");
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to approve request.", "error");
    }
  };

  const handleConvert = async (item: DispatchRequestItem) => {
    try {
      const result = await api<{ requestId: string; tripId: string; status: RequestStatus }>(
        `/api/dispatch/requests/${item.id}/convert`,
        { method: "POST" }
      );
      updateRequest(item.id, { status: "CONVERTED_TO_TRIP", tripId: result.tripId });
      show("Request converted to trip.", "success");
      nav(`/dispatch/trips/${result.tripId}`);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to convert request.", "error");
    }
  };

  const handleReject = async () => {
    if (!rejectModal) return;
    if (!rejectModal.remarks.trim()) {
      show("Remarks are required.", "error");
      return;
    }
    try {
      await api(`/api/dispatch/requests/${rejectModal.id}/reject`, {
        method: "POST",
        body: JSON.stringify({ remarks: rejectModal.remarks.trim() })
      });
      setRequests((prev) => prev.filter((item) => item.id !== rejectModal.id));
      show("Request rejected.", "success");
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to reject request.", "error");
    } finally {
      setRejectModal(null);
    }
  };

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Shipment Requests Queue"
        description="Review and convert customer requests."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/dispatch/board" className="text-muted-foreground hover:text-foreground">
              Dispatch
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">Requests</span>
          </nav>
        }
        actions={
          <Button variant="outline" size="sm" onClick={loadQueue} disabled={loading}>
            Refresh
          </Button>
        }
      />

      {loading && requests.length === 0 ? (
        <LoadingSkeleton rows={6} />
      ) : requests.length === 0 ? (
        <EmptyState title="Queue is clear" description="No submitted requests waiting for review." />
      ) : (
        <div className="surface-card p-4">
          <DataTable>
            <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-4 py-3 text-left">Request</th>
                <th className="px-4 py-3 text-left">Customer</th>
                <th className="px-4 py-3 text-left">Pickup</th>
                <th className="px-4 py-3 text-left">Dropoff</th>
                <th className="px-4 py-3 text-left">Requested</th>
                <th className="px-4 py-3 text-left">Docs</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {requests.map((item) => (
                <tr key={item.id} className="border-t border-border/60">
                  <td className="px-4 py-3 text-sm font-semibold text-foreground">
                    <div>{item.id.slice(0, 8)}</div>
                    <div className="mt-1 text-xs font-normal text-muted-foreground">
                      {containerSizeLabels[item.containerSize]} / {tripTypeLabels[item.tripType]}
                    </div>
                    {item.containerNumber ? (
                      <div className="mt-0.5 text-xs font-normal text-muted-foreground">
                        {item.containerNumber}
                      </div>
                    ) : null}
                    {item.bookingNumber ? (
                      <div className="mt-0.5 text-xs font-normal text-muted-foreground">
                        Booking {item.bookingNumber}
                      </div>
                    ) : null}
                  </td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">{item.customerName}</td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">{item.pickupLocation}</td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">{item.dropoffLocation}</td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">
                    {item.requestedPickupTime ? new Date(item.requestedPickupTime).toLocaleString() : "Unscheduled"}
                  </td>
                  <td className="px-4 py-3 text-sm text-muted-foreground">{item.documentsCount}</td>
                  <td className="px-4 py-3">
                    <StatusBadge status={item.status} />
                  </td>
                  <td className="px-4 py-3 text-right">
                    <div className="flex flex-wrap justify-end gap-2">
                      {item.status === "SUBMITTED" ? (
                        <Button variant="outline" size="sm" onClick={() => handleApprove(item)}>
                          Approve
                        </Button>
                      ) : null}
                      {item.status === "APPROVED" ? (
                        <Button variant="outline" size="sm" onClick={() => handleConvert(item)}>
                          Convert
                        </Button>
                      ) : null}
                      {item.status === "CONVERTED_TO_TRIP" && item.tripId ? (
                        <Button variant="outline" size="sm" onClick={() => nav(`/dispatch/trips/${item.tripId}`)}>
                          View Trip
                        </Button>
                      ) : null}
                      {item.status === "SUBMITTED" ? (
                        <Button variant="outline" size="sm" onClick={() => setRejectModal({ id: item.id, remarks: "" })}>
                          Reject
                        </Button>
                      ) : null}
                    </div>
                  </td>
                </tr>
              ))}
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

      {rejectModal ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,520px)] rounded-2xl border border-slate-200 bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Reject</p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">Reject Request</h2>
              </div>
              <button
                onClick={() => setRejectModal(null)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>
            <div className="mt-5 space-y-2 text-sm">
              <label className="text-xs uppercase text-slate-500">Remarks</label>
              <textarea
                value={rejectModal.remarks}
                onChange={(e) => setRejectModal({ ...rejectModal, remarks: e.target.value })}
                className="mt-2 min-h-[100px] w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm"
                placeholder="Provide rejection reason"
              />
            </div>
            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setRejectModal(null)}>
                Cancel
              </Button>
              <Button onClick={handleReject}>Reject</Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
