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

type AuditLogRow = {
  id: string;
  action: string;
  entityType: string;
  entityId: string;
  actorUserId: string;
  actorUsername?: string | null;
  createdAt: string;
  metadata?: string | null;
};

type ModuleSettingMeta = {
  ModuleKey?: string;
  IsEnabled?: boolean;
  Notes?: string | null;
  moduleKey?: string;
  isEnabled?: boolean;
  notes?: string | null;
};

export default function AuditLogsPage() {
  const { toasts, show } = useToast();
  const [logs, setLogs] = useState<AuditLogRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);

  const [action, setAction] = useState("");
  const [entityType, setEntityType] = useState("");
  const [entityId, setEntityId] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const moduleNameMap: Record<string, string> = {
    auth: "Auth",
    users: "Users",
    assets: "Assets",
    inventory: "Inventory",
    "inventory-adjustments": "Inventory Adjustments",
    loans: "Loans",
    "purchase-orders": "Purchase Orders",
    reports: "Reports",
    requests: "Requests",
    suppliers: "Suppliers",
    approvals: "Approvals",
    dispatch: "Dispatching"
  };

  function parseMetadata(raw?: string | null): ModuleSettingMeta | null {
    if (!raw) return null;
    try {
      const parsed = JSON.parse(raw) as ModuleSettingMeta;
      return parsed && typeof parsed === "object" ? parsed : null;
    } catch {
      return null;
    }
  }

  function formatAction(log: AuditLogRow) {
    if (log.action === "MODULE_SETTING_UPDATED") {
      const meta = parseMetadata(log.metadata);
      const enabled = meta?.IsEnabled ?? meta?.isEnabled;
      const status = enabled ? "Enabled" : "Disabled";
      return `Module ${status}`;
    }
    return log.action;
  }

  function formatMetadata(log: AuditLogRow) {
    if (log.action === "MODULE_SETTING_UPDATED") {
      const meta = parseMetadata(log.metadata);
      if (!meta) return "—";
      const key = meta.ModuleKey ?? meta.moduleKey ?? "";
      const name = key ? moduleNameMap[key] ?? key : "Unknown module";
      const enabled = meta.IsEnabled ?? meta.isEnabled;
      const status = enabled ? "Enabled" : "Disabled";
      const notes = meta.Notes ?? meta.notes ? `Notes: ${meta.Notes ?? meta.notes}` : null;
      return [name, status, notes].filter(Boolean).join(" • ");
    }
    return log.metadata ? log.metadata : "—";
  }

  async function loadLogs(targetPage: number) {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      params.set("page", targetPage.toString());
      params.set("pageSize", pageSize.toString());
      if (action.trim()) params.set("action", action.trim());
      if (entityType.trim()) params.set("entityType", entityType.trim());
      if (entityId.trim()) params.set("entityId", entityId.trim());
      if (from) params.set("from", new Date(from).toISOString());
      if (to) params.set("to", new Date(to).toISOString());

      const result = await api<PagedResult<AuditLogRow>>(
        `/api/reports/audit?${params.toString()}`,
        { method: "GET" }
      );
      setLogs(result.items);
      setTotalCount(result.totalCount);
      setPage(result.page);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load audit logs.", "error");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadLogs(1);
  }, []);

  return (
    <div className="pb-10">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Audit Logs"
        description="Review system actions across modules and users."
      />

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 p-6">
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-5">
          <div>
            <label className="text-xs uppercase text-slate-500">Action</label>
            <input
              value={action}
              onChange={(e) => setAction(e.target.value)}
              placeholder="MODULE_SETTING_UPDATED"
              className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-slate-500">Entity Type</label>
            <input
              value={entityType}
              onChange={(e) => setEntityType(e.target.value)}
              placeholder="module_setting"
              className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-slate-500">Entity Id</label>
            <input
              value={entityId}
              onChange={(e) => setEntityId(e.target.value)}
              placeholder="GUID"
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
            onClick={() => loadLogs(1)}
            disabled={loading}
            className="rounded-lg bg-[#175C99] px-4 py-2 text-sm font-semibold text-white hover:bg-[#144c7f] disabled:opacity-60"
          >
            Apply Filters
          </button>
          <button
            onClick={() => {
              setAction("");
              setEntityType("");
              setEntityId("");
              setFrom("");
              setTo("");
              loadLogs(1);
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
              <th className="px-4 py-3 text-left">Action</th>
              <th className="px-4 py-3 text-left">Entity</th>
              <th className="px-4 py-3 text-left">Actor</th>
              <th className="px-4 py-3 text-left">Metadata</th>
            </tr>
          </thead>
          <tbody>
            {logs.map((log) => (
              <tr key={log.id} className="border-t border-slate-100">
                <td className="px-4 py-3 text-xs text-slate-500">
                  {new Date(log.createdAt).toLocaleString()}
                </td>
                <td className="px-4 py-3 text-sm text-slate-900">
                  <div className="font-medium">{formatAction(log)}</div>
                  <div className="text-xs text-slate-400">{log.action}</div>
                </td>
                <td className="px-4 py-3 text-sm text-slate-600">
                  <div className="font-medium">{log.entityType}</div>
                  <div className="text-xs text-slate-400">{log.entityId}</div>
                </td>
                <td className="px-4 py-3 text-sm text-slate-600">
                  <div className="font-medium">{log.actorUsername ?? "—"}</div>
                  <div className="text-xs text-slate-400">{log.actorUserId}</div>
                </td>
                <td className="px-4 py-3 text-xs text-slate-500">
                  {formatMetadata(log)}
                </td>
              </tr>
            ))}
          </tbody>
        </DataTable>
        {logs.length === 0 && !loading ? (
          <div className="mt-6">
            <EmptyState title="No audit logs found." description="Adjust filters or try another date range." />
          </div>
        ) : null}

        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 text-sm text-slate-500">
          <span>
            Page {page} of {totalPages}
          </span>
          <div className="flex gap-2">
            <button
              onClick={() => loadLogs(Math.max(1, page - 1))}
              disabled={loading || page <= 1}
              className="rounded-lg border border-slate-200 px-3 py-2 text-xs text-slate-600 hover:text-slate-900 disabled:opacity-60"
            >
              Prev
            </button>
            <button
              onClick={() => loadLogs(Math.min(totalPages, page + 1))}
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
