import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";
import type { PagedResult } from "@/lib/paging";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import StatusBadge from "@/components/StatusBadge";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";

type LoanListItem = {
  id: string;
  requestId: string;
  status: string;
  borrowerUsername: string;
  assetCode?: string | null;
  issuedAt: string;
};

export default function LoanListPage() {
  const nav = useNavigate();
  const me = getMe();
  const { toasts, show } = useToast();
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [paged, setPaged] = useState<PagedResult<LoanListItem> | null>(null);
  const [statusFilter, setStatusFilter] = useState("OPEN,PARTIALLY_RETURNED");
  const [loading, setLoading] = useState(false);

  const totalPages = useMemo(() => {
    if (!paged) return 1;
    return Math.max(1, Math.ceil(paged.totalCount / pageSize));
  }, [paged, pageSize]);

  useEffect(() => {
    if (!me) {
      nav("/login");
      return;
    }
    if (!me.roles?.includes("InventoryOfficer") && !me.roles?.includes("Manager")) {
      show("Access denied.", "error");
      return;
    }

    (async () => {
      try {
        setLoading(true);
        const result = await api<PagedResult<LoanListItem>>(
          `/api/loans?status=${encodeURIComponent(statusFilter)}&page=${page}&pageSize=${pageSize}`,
          { method: "GET" }
        );
        setPaged(result);
      } catch (e: any) {
        console.error(e);
        show(e?.message ?? "Failed to load loans.", "error");
      } finally {
        setLoading(false);
      }
    })();
  }, [page, statusFilter]);

  const formatDate = (value?: string | null) => {
    if (!value) return "-";
    const dt = new Date(value);
    return Number.isNaN(dt.getTime()) ? value : dt.toLocaleString();
  };

  return (
    <div>
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Loans"
        description="Track active borrowings and returns."
        actions={
          <button onClick={() => nav("/requests")} className="rounded-lg border border-border px-3 py-2 text-sm">
            Back
          </button>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div>
          <label className="text-xs uppercase text-muted-foreground">Status</label>
          <select
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setPage(1);
            }}
            className="mt-1 h-9 rounded-lg border border-border bg-white px-3 text-sm"
          >
            <option value="OPEN,PARTIALLY_RETURNED">Open + Partial</option>
            <option value="OPEN">Open</option>
            <option value="PARTIALLY_RETURNED">Partially Returned</option>
            <option value="CLOSED">Closed</option>
          </select>
        </div>
      </div>

      {loading && (paged?.items.length ?? 0) === 0 ? <LoadingSkeleton rows={5} /> : null}

      <DataTable>
        <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
          <tr>
            <th className="px-4 py-3 text-left">Loan</th>
            <th className="px-4 py-3 text-left">Status</th>
            <th className="px-4 py-3 text-left">Borrower</th>
            <th className="px-4 py-3 text-left">Asset</th>
            <th className="px-4 py-3 text-left">Issued</th>
          </tr>
        </thead>
        <tbody>
          {(paged?.items ?? []).map((loan) => (
            <tr key={loan.id} className="border-t border-border">
              <td className="px-4 py-3 text-sm text-primary">
                <Link to={`/loans/${loan.id}`} className="hover:underline">
                  {loan.id}
                </Link>
              </td>
              <td className="px-4 py-3 text-sm">
                <StatusBadge status={loan.status} />
              </td>
              <td className="px-4 py-3 text-sm">{loan.borrowerUsername}</td>
              <td className="px-4 py-3 text-sm">{loan.assetCode ?? "-"}</td>
              <td className="px-4 py-3 text-sm">{formatDate(loan.issuedAt)}</td>
            </tr>
          ))}
        </tbody>
      </DataTable>

      {paged?.items.length === 0 ? (
        <EmptyState title="No loans found." description="Try another status filter." />
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
          Page {paged?.page ?? page} of {totalPages}
        </span>
        <button
          onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
          disabled={page >= totalPages || loading}
          className="rounded-lg border border-border px-3 py-2 text-sm text-foreground"
        >
          Next
        </button>
        <span>Total: {paged?.totalCount ?? 0}</span>
      </div>
    </div>
  );
}
