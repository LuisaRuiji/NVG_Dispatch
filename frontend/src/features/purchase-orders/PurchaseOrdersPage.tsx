import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import type { PurchaseOrderListItem } from "./types";
import type { PagedResult } from "@/lib/paging";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import StatusBadge from "@/components/StatusBadge";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import ToastHost from "@/components/ToastHost";
import { useToast } from "@/lib/useToast";

const statuses = [
  "DRAFT",
  "SUBMITTED",
  "PENDING_MANAGER",
  "PENDING_FINANCE",
  "PENDING_CEO",
  "APPROVED",
  "PARTIALLY_RECEIVED",
  "CLOSED",
  "REJECTED"
];

export default function PurchaseOrdersPage() {
  const nav = useNavigate();
  const me = getMe();
  const { toasts, show } = useToast();

  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [selectedStatus, setSelectedStatus] = useState("ALL");
  const [data, setData] = useState<PagedResult<PurchaseOrderListItem> | null>(null);
  const [loading, setLoading] = useState(false);

  const totalPages = useMemo(() => {
    if (!data) return 1;
    return Math.max(1, Math.ceil(data.totalCount / pageSize));
  }, [data, pageSize]);

  useEffect(() => {
    if (!me) {
      nav("/login");
      return;
    }
  }, [me?.userId, nav]);

  useEffect(() => {
    if (!me) return;
    (async () => {
      try {
        setLoading(true);
        const params = new URLSearchParams();
        if (selectedStatus !== "ALL") {
          params.set("status", selectedStatus);
        }
        params.set("page", page.toString());
        params.set("pageSize", pageSize.toString());
        const result = await api<PagedResult<PurchaseOrderListItem>>(
          `/api/purchase-orders?${params.toString()}`,
          { method: "GET" }
        );
        setData(result);
      } catch (e: any) {
        console.error(e);
        show(e?.message ?? "Failed to load purchase orders.", "error");
      } finally {
        setLoading(false);
      }
    })();
  }, [page, selectedStatus, me?.userId]);

  const formatDate = (value?: string | null) => {
    if (!value) return "-";
    const dt = new Date(value);
    return Number.isNaN(dt.getTime()) ? value : dt.toLocaleDateString();
  };

  return (
    <div>
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Purchase Orders"
        description="Approval workflow and receiving control."
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div>
          <label className="text-xs uppercase text-muted-foreground">Status</label>
          <select
            value={selectedStatus}
            onChange={(e) => {
              setSelectedStatus(e.target.value);
              setPage(1);
            }}
            className="mt-1 h-9 rounded-lg border border-border bg-white px-3 text-sm"
          >
            <option value="ALL">All</option>
            {statuses.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
        </div>
      </div>

      {loading && (data?.items.length ?? 0) === 0 ? (
        <LoadingSkeleton rows={5} />
      ) : (
        <DataTable>
          <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left">PO</th>
              <th className="px-4 py-3 text-left">Supplier</th>
              <th className="px-4 py-3 text-left">Status</th>
              <th className="px-4 py-3 text-left">Created By</th>
              <th className="px-4 py-3 text-left">Created</th>
              <th className="px-4 py-3 text-left">Submitted</th>
              <th className="px-4 py-3 text-left">Approved</th>
            </tr>
          </thead>
          <tbody>
            {(data?.items ?? []).map((po) => (
              <tr key={po.id} className="border-t border-border">
                <td className="px-4 py-3 text-sm text-primary">
                  <Link to={`/purchase-orders/${po.id}`} className="hover:underline">
                    {po.id}
                  </Link>
                </td>
                <td className="px-4 py-3 text-sm">{po.supplierName}</td>
                <td className="px-4 py-3 text-sm">
                  <StatusBadge status={po.status} />
                </td>
                <td className="px-4 py-3 text-sm">{po.createdByUsername}</td>
                <td className="px-4 py-3 text-sm">{formatDate(po.createdAt)}</td>
                <td className="px-4 py-3 text-sm">{formatDate(po.submittedAt)}</td>
                <td className="px-4 py-3 text-sm">{formatDate(po.approvedAt)}</td>
              </tr>
            ))}
          </tbody>
        </DataTable>
      )}

      {data?.items.length === 0 && !loading ? (
        <EmptyState title="No purchase orders found." description="Adjust filters or create a new PO." />
      ) : null}

      <div className="mt-4 flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
        <button
          onClick={() => setPage((p) => Math.max(1, p - 1))}
          disabled={page <= 1 || loading}
          className="rounded-lg border border-border px-3 py-2 text-sm text-foreground"
        >
          Prev
        </button>
        <span>
          Page {data?.page ?? page} of {totalPages}
        </span>
        <button
          onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
          disabled={page >= totalPages || loading}
          className="rounded-lg border border-border px-3 py-2 text-sm text-foreground"
        >
          Next
        </button>
        <span>Total: {data?.totalCount ?? 0}</span>
      </div>
    </div>
  );
}
