import { useEffect, useMemo, useState } from "react";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";

type PagedResult<T> = {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
};

type AuthEventRow = {
  id: string;
  eventType: string;
  outcome: string;
  reasonCode?: string | null;
  username?: string | null;
  userId?: string | null;
  rolesSnapshotJson?: string | null;
  authMethod: string;
  mfaPerformed: boolean;
  mfaMethod?: string | null;
  tokenJti?: string | null;
  correlationId?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  clientApp?: string | null;
  environment?: string | null;
  createdAt: string;
};

export default function AuthEventsPage() {
  const { toasts, show } = useToast();
  const [events, setEvents] = useState<AuthEventRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);

  const [eventType, setEventType] = useState("LOGIN");
  const [outcome, setOutcome] = useState("");
  const [username, setUsername] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  async function loadEvents(targetPage: number) {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      params.set("page", targetPage.toString());
      params.set("pageSize", pageSize.toString());
      if (eventType.trim()) params.set("eventType", eventType.trim());
      if (outcome.trim()) params.set("outcome", outcome.trim());
      if (username.trim()) params.set("username", username.trim());
      if (from) params.set("from", new Date(from).toISOString());
      if (to) params.set("to", new Date(to).toISOString());

      const result = await api<PagedResult<AuthEventRow>>(
        `/api/reports/auth-events?${params.toString()}`,
        { method: "GET" }
      );
      setEvents(result.items);
      setTotalCount(result.totalCount);
      setPage(result.page);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load auth events.", "error");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadEvents(1);
  }, []);

  return (
    <div className="pb-10">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Auth Events"
        description="Login success/failure events with source context."
      />

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 p-6">
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-5">
          <div>
            <label className="text-xs uppercase text-slate-500">Event</label>
            <input
              value={eventType}
              onChange={(e) => setEventType(e.target.value)}
              placeholder="LOGIN"
              className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-slate-500">Outcome</label>
            <select
              value={outcome}
              onChange={(e) => setOutcome(e.target.value)}
              className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
            >
              <option value="">All</option>
              <option value="SUCCESS">SUCCESS</option>
              <option value="FAILURE">FAILURE</option>
            </select>
          </div>
          <div>
            <label className="text-xs uppercase text-slate-500">Username</label>
            <input
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="Admin"
              className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-slate-500">From</label>
            <input
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
              className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-slate-500">To</label>
            <input
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
              className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
            />
          </div>
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <button
            onClick={() => loadEvents(1)}
            disabled={loading}
            className="rounded-lg bg-[#175C99] px-4 py-2 text-sm font-semibold text-white hover:bg-[#144c7f] disabled:opacity-60"
          >
            Apply Filters
          </button>
          <button
            onClick={() => {
              setEventType("LOGIN");
              setOutcome("");
              setUsername("");
              setFrom("");
              setTo("");
              loadEvents(1);
            }}
            disabled={loading}
            className="rounded-lg border border-slate-200 px-4 py-2 text-sm text-slate-600 hover:text-slate-900 disabled:opacity-60"
          >
            Reset
          </button>
          <span className="text-xs text-slate-500">
            {totalCount} entries
          </span>
        </div>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 p-6 mt-6">
        <DataTable>
          <thead className="bg-slate-50/50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3 text-left">Time</th>
              <th className="px-4 py-3 text-left">Outcome</th>
              <th className="px-4 py-3 text-left">User</th>
              <th className="px-4 py-3 text-left">Reason</th>
              <th className="px-4 py-3 text-left">Source</th>
            </tr>
          </thead>
          <tbody>
            {events.map((ev) => (
              <tr key={ev.id} className="border-t border-slate-100">
                <td className="px-4 py-3 text-xs text-slate-500">
                  {new Date(ev.createdAt).toLocaleString()}
                </td>
                <td className="px-4 py-3 text-sm">
                  <span
                    className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${
                      ev.outcome === "SUCCESS"
                        ? "bg-emerald-50 text-emerald-700 border border-emerald-200"
                        : "bg-rose-50 text-rose-700 border border-rose-200"
                    }`}
                  >
                    {ev.outcome}
                  </span>
                </td>
                <td className="px-4 py-3 text-sm text-slate-600">
                  <div className="font-medium">{ev.username ?? "—"}</div>
                  <div className="text-xs text-slate-400">{ev.userId ?? ""}</div>
                </td>
                <td className="px-4 py-3 text-xs text-slate-500">
                  {ev.reasonCode ?? "—"}
                </td>
                <td className="px-4 py-3 text-xs text-slate-500">
                  <div>{ev.ipAddress ?? "—"}</div>
                  <div className="truncate max-w-[280px]">{ev.userAgent ?? ""}</div>
                </td>
              </tr>
            ))}
          </tbody>
        </DataTable>
        {events.length === 0 && !loading ? (
          <div className="mt-6">
            <EmptyState title="No auth events found." description="Adjust filters or try another date range." />
          </div>
        ) : null}

        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 text-sm text-slate-500">
          <span>
            Page {page} of {totalPages}
          </span>
          <div className="flex gap-2">
            <button
              onClick={() => loadEvents(Math.max(1, page - 1))}
              disabled={loading || page <= 1}
              className="rounded-lg border border-slate-200 px-3 py-2 text-xs text-slate-600 hover:text-slate-900 disabled:opacity-60"
            >
              Prev
            </button>
            <button
              onClick={() => loadEvents(Math.min(totalPages, page + 1))}
              disabled={loading || page >= totalPages}
              className="rounded-lg border border-slate-200 px-3 py-2 text-xs text-slate-600 hover:text-slate-900 disabled:opacity-60"
            >
              Next
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
