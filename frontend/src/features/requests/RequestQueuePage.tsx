import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "@/lib/api";
import type { RequestListItem } from "./types";
import { getMe } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import StatusBadge from "@/components/StatusBadge";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import type { PagedResult } from "@/lib/paging";

type RequestQueueProps = {
  title: string;
  statuses?: string[];
  requesterUserId?: string;
  requireRole?: string;
};

function buildQuery(params: Record<string, string | undefined>) {
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value && value.trim().length > 0) {
      search.set(key, value);
    }
  });
  return search.toString();
}

export default function RequestQueuePage({
  title,
  statuses,
  requesterUserId,
  requireRole
}: RequestQueueProps) {
  const nav = useNavigate();
  const me = getMe();
  const { toasts, show } = useToast();

  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [searchId, setSearchId] = useState("");
  const [paged, setPaged] = useState<PagedResult<RequestListItem> | null>(null);
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
    if (requireRole && !me.roles?.includes(requireRole)) {
      show("Access denied.", "error");
      return;
    }

    (async () => {
      try {
        setLoading(true);
        const query = buildQuery({
          status: statuses && statuses.length > 0 ? statuses.join(",") : undefined,
          requesterUserId,
          page: page.toString(),
          pageSize: pageSize.toString()
        });
        const result = await api<PagedResult<RequestListItem>>(`/api/requests?${query}`, { method: "GET" });
        setPaged(result);
      } catch (e: any) {
        console.error(e);
        show(e?.message ?? "Failed to load requests.", "error");
      } finally {
        setLoading(false);
      }
    })();
  }, [page, requesterUserId, requireRole, statuses?.join(","), me?.userId]);

  const items = paged?.items ?? [];

  const handleSearch = () => {
    const trimmed = searchId.trim();
    if (!trimmed) return;
    nav(`/requests/${trimmed}`);
  };

  const formatDate = (value?: string | null) => {
    if (!value) return "-";
    const dt = new Date(value);
    return Number.isNaN(dt.getTime()) ? value : dt.toLocaleString();
  };

  return (
    <div>
      <ToastHost toasts={toasts} />
      <PageHeader title={title} description="Requests awaiting action." />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <input
          placeholder="Search by request id"
          value={searchId}
          onChange={(e) => setSearchId(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              handleSearch();
            }
          }}
          className="h-9 w-72 rounded-lg border border-border bg-white px-3 text-sm"
        />
        <button
          onClick={handleSearch}
          disabled={!searchId.trim()}
          className="rounded-lg border border-border px-3 py-2 text-sm"
        >
          Go
        </button>
      </div>

      {loading && items.length === 0 ? (
        <LoadingSkeleton rows={5} />
      ) : (
        <DataTable>
          <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left">Id</th>
              <th className="px-4 py-3 text-left">Type</th>
              <th className="px-4 py-3 text-left">Status</th>
              <th className="px-4 py-3 text-left">Asset</th>
              <th className="px-4 py-3 text-left">Requester</th>
              <th className="px-4 py-3 text-left">Submitted</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => {
              const submittedAt = item.submittedAt ?? item.createdAt;
              return (
                <tr key={item.id} className="border-t border-border">
                  <td className="px-4 py-3 text-sm text-primary">
                    <Link to={`/requests/${item.id}`} className="hover:underline">
                      {item.id}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-sm">{item.requestType}</td>
                  <td className="px-4 py-3 text-sm">
                    <StatusBadge status={item.status} />
                  </td>
                  <td className="px-4 py-3 text-sm">{item.assetCode ?? "-"}</td>
                  <td className="px-4 py-3 text-sm">{item.requesterUsername ?? "-"}</td>
                  <td className="px-4 py-3 text-sm">{formatDate(submittedAt)}</td>
                </tr>
              );
            })}
          </tbody>
        </DataTable>
      )}

      {items.length === 0 && !loading ? (
        <EmptyState title="No requests found." description="Try adjusting filters or search." />
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

export function IoQueuePage() {
  return (
    <RequestQueuePage
      title="IO Queue"
      statuses={["PENDING_IO"]}
      requireRole="InventoryOfficer"
    />
  );
}

export function IssueQueuePage() {
  return (
    <RequestQueuePage
      title="Issue Queue"
      statuses={["APPROVED"]}
      requireRole="InventoryOfficer"
    />
  );
}

export function ManagerQueuePage() {
  return (
    <RequestQueuePage
      title="Manager Queue"
      statuses={["PENDING_MANAGER"]}
      requireRole="Manager"
    />
  );
}

export function MyRequestsPage() {
  const me = getMe();
  return (
    <RequestQueuePage
      title="My Requests"
      requesterUserId={me?.userId}
      requireRole="Driver"
    />
  );
}
