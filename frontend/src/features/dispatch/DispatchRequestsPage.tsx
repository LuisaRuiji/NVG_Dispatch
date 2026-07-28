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
type ConvertModalState = {
  item: DispatchRequestItem;
  rescheduledTime: string;
  isPriority: boolean;
} | null;

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

const parseLocalComponents = (dateStr?: string | null) => {
  if (!dateStr) return null;
  const clean = dateStr.replace("Z", "");
  const [datePart, timePart] = clean.split("T");
  if (!datePart || !timePart) return null;
  const [year, month, day] = datePart.split("-").map(Number);
  const [hour, minute] = timePart.split(":").map(Number);
  if (isNaN(year) || isNaN(month) || isNaN(day) || isNaN(hour) || isNaN(minute)) return null;
  return new Date(year, month - 1, day, hour, minute);
};

const formatUtcDateTime = (dateStr?: string | null) => {
  const d = parseLocalComponents(dateStr);
  if (!d) return dateStr ? dateStr.replace("T", " ").slice(0, 16) : "Unscheduled";
  return d.toLocaleString(undefined, {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: true
  });
};

const isOverdue = (dateStr?: string | null) => {
  const d = parseLocalComponents(dateStr);
  return d ? d.getTime() < Date.now() : false;
};

const getNowLocalInput = () => {
  const date = new Date();
  const offset = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
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
  const [overdueModal, setOverdueModal] = useState<DispatchRequestItem | null>(null);
  const [convertModal, setConvertModal] = useState<ConvertModalState>(null);

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
      const items = result.items ?? [];
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

  const handleApprove = async (item: DispatchRequestItem, skipOverdueCheck = false) => {
    if (!skipOverdueCheck && isOverdue(item.requestedPickupTime)) {
      setOverdueModal(item);
      return;
    }
    try {
      await api(`/api/dispatch/requests/${item.id}/approve`, { method: "POST" });
      updateRequest(item.id, { status: "APPROVED" });
      show("Request approved.", "success");
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to approve request.", "error");
    } finally {
      setOverdueModal(null);
    }
  };

  const handleConvert = async (item: DispatchRequestItem, skipCheck = false) => {
    if (!skipCheck && isOverdue(item.requestedPickupTime)) {
      setConvertModal({
        item,
        rescheduledTime: getNowLocalInput(),
        isPriority: true
      });
      return;
    }
    try {
      const result = await api<{ requestId: string; tripId: string; status: RequestStatus }>(
        `/api/dispatch/requests/${item.id}/convert`,
        { method: "POST" }
      );
      updateRequest(item.id, { status: "CONVERTED_TO_TRIP", tripId: result.tripId });
      show(skipCheck ? "Request converted with priority schedule." : "Request converted to trip.", "success");
      nav(`/dispatch/trips/${result.tripId}`);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to convert request.", "error");
    } finally {
      setConvertModal(null);
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
              {requests.map((item) => {
                const overdue = item.status === "SUBMITTED" && isOverdue(item.requestedPickupTime);
                const approvedOverdue = item.status === "APPROVED" && isOverdue(item.requestedPickupTime);
                return (
                  <tr
                    key={item.id}
                    className={`border-t border-border/60 ${
                      overdue ? "bg-red-50/30" : approvedOverdue ? "bg-amber-50/20" : ""
                    }`}
                  >
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
                      <div>
                        {formatUtcDateTime(item.requestedPickupTime)}
                      </div>
                      {overdue ? (
                        <span className="mt-1 inline-flex items-center gap-1 rounded-md border border-red-200 bg-red-50 px-1.5 py-0.5 text-[10px] font-semibold text-red-700">
                          <span className="h-1.5 w-1.5 rounded-full bg-red-600 animate-pulse"></span>
                          Past Due (Dispatch Delay)
                        </span>
                      ) : null}
                      {approvedOverdue ? (
                        <span className="mt-1 inline-flex items-center gap-1 rounded-md border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-[10px] font-semibold text-amber-800">
                          <span className="h-1.5 w-1.5 rounded-full bg-amber-600 animate-pulse"></span>
                          Pending Conversion (Past Schedule)
                        </span>
                      ) : null}
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

      {overdueModal ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,520px)] rounded-2xl border border-amber-200 bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <span className="inline-flex items-center gap-1 rounded-md border border-amber-200 bg-amber-50 px-2 py-0.5 text-xs font-semibold text-amber-800">
                  Dispatch Review Delay
                </span>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">Approve Past-Due Request?</h2>
              </div>
              <button
                onClick={() => setOverdueModal(null)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>
            <div className="mt-4 space-y-2 text-sm text-slate-600">
              <p>
                This request was scheduled for{" "}
                <strong>
                  {formatUtcDateTime(overdueModal.requestedPickupTime)}
                </strong>
                , which is past today's date.
              </p>
              <p className="text-xs bg-slate-50 border border-slate-200 rounded-lg p-3 text-slate-700">
                <strong>Internal Delay Notice:</strong> This request elapsed while pending dispatcher review. Approving will mark it as priority for immediate trip conversion and dispatch allocation.
              </p>
            </div>
            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setOverdueModal(null)}>
                Cancel
              </Button>
              <Button onClick={() => handleApprove(overdueModal, true)}>
                Confirm Priority Approval
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {convertModal ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,540px)] rounded-2xl border border-amber-200 bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <span className="inline-flex items-center gap-1 rounded-md border border-amber-300 bg-amber-50 px-2 py-0.5 text-xs font-semibold text-amber-800">
                  Reschedule & Convert Trip
                </span>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">Convert Past-Due Approved Request</h2>
              </div>
              <button
                onClick={() => setConvertModal(null)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>
            <div className="mt-4 space-y-4 text-sm text-slate-600">
              <p>
                Request <strong>{convertModal.item.id.slice(0, 8)}</strong> was approved for{" "}
                <strong>
                  {formatUtcDateTime(convertModal.item.requestedPickupTime)}
                </strong>
                , which has elapsed. Please confirm an updated schedule for this trip.
              </p>
              <div className="space-y-1.5">
                <label className="text-xs font-semibold uppercase text-slate-500">Updated Scheduled Pickup Time</label>
                <input
                  type="datetime-local"
                  value={convertModal.rescheduledTime}
                  onChange={(e) => setConvertModal({ ...convertModal, rescheduledTime: e.target.value })}
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-indigo-500 focus:outline-none"
                />
              </div>
              <div className="flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50/60 p-3 text-xs text-amber-900">
                <input
                  type="checkbox"
                  id="priorityCheck"
                  checked={convertModal.isPriority}
                  onChange={(e) => setConvertModal({ ...convertModal, isPriority: e.target.checked })}
                  className="h-4 w-4 rounded border-amber-300 text-amber-600 focus:ring-amber-500"
                />
                <label htmlFor="priorityCheck" className="font-medium cursor-pointer">
                  Mark trip as <strong>High Priority / Priority Expedited Dispatch</strong>
                </label>
              </div>
            </div>
            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setConvertModal(null)}>
                Cancel
              </Button>
              <Button onClick={() => handleConvert(convertModal.item, true)}>
                Confirm & Convert to Trip
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
