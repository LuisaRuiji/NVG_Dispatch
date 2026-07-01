import { useEffect, useMemo, useState } from "react";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import { useAuditLogColumns, type AuditLogColumnKey, type AuditLogFilterKey } from "@/hooks/useAuditLogColumns";
import {
  formatAuditMetadata,
  isKnownAuditMetadataAction,
  parseAuditMetadata,
  stringifyMetadata,
  type AuditMetadata
} from "@/utils/auditMetaFormatter";

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
  actorRole?: string | null;
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
  const me = getMe();
  const currentRole = getPrimaryAuditRole(me?.roles ?? []);
  const auditLayout = useAuditLogColumns(currentRole);
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

  const hasFilter = (filter: AuditLogFilterKey) => auditLayout.filters.includes(filter);

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

  function formatAction(log: AuditLogRow) {
    if (log.action === "MODULE_SETTING_UPDATED") {
      const meta = parseAuditMetadata(log.metadata) as ModuleSettingMeta | null;
      const enabled = meta?.IsEnabled ?? meta?.isEnabled;
      const status = enabled ? "Enabled" : "Disabled";
      return `Module ${status}`;
    }
    return log.action;
  }

  function formatMetadata(log: AuditLogRow): { text: string; raw: string; isKnown: boolean } {
    const parsed = parseAuditMetadata(log.metadata);
    const raw = parsed ? stringifyMetadata(parsed) : "";

    if (log.action === "MODULE_SETTING_UPDATED") {
      const meta = parsed as ModuleSettingMeta | null;
      if (!meta) return { text: "-", raw, isKnown: true };

      const key = meta.ModuleKey ?? meta.moduleKey ?? "";
      const name = key ? moduleNameMap[key] ?? key : "Unknown module";
      const enabled = meta.IsEnabled ?? meta.isEnabled;
      const status = enabled ? "Enabled" : "Disabled";
      const notes = meta.Notes ?? meta.notes ? `Notes: ${meta.Notes ?? meta.notes}` : null;
      return { text: [name, status, notes].filter(Boolean).join(" | "), raw, isKnown: true };
    }

    if (!parsed) {
      return { text: "-", raw: "", isKnown: true };
    }

    const isKnown = isKnownAuditMetadataAction(log.action);
    return {
      text: formatAuditMetadata(log.action, parsed as AuditMetadata),
      raw,
      isKnown
    };
  }

  function formatFriendlyEntity(log: AuditLogRow, preferred: "entity" | "item" | "trip") {
    const meta = parseAuditMetadata(log.metadata);
    if (preferred === "trip") {
      return textFromMeta(meta, "tripReference", "tripNumber", "tripNo", "reference", "TripReference")
        ?? tripReference(log.entityId);
    }

    if (preferred === "item") {
      return textFromMeta(meta, "itemName", "inventoryName", "name", "ItemName", "InventoryName")
        ?? friendlyEntityFallback(log);
    }

    return textFromMeta(
      meta,
      "tripReference",
      "tripNumber",
      "driverName",
      "documentType",
      "paymentReference",
      "itemName",
      "inventoryName",
      "name",
      "reference"
    ) ?? friendlyEntityFallback(log);
  }

  function renderCell(column: AuditLogColumnKey, log: AuditLogRow) {
    const metadata = formatMetadata(log);

    switch (column) {
      case "time":
        return <span className="text-xs text-slate-500">{new Date(log.createdAt).toLocaleString()}</span>;
      case "action":
        return (
          <div className="text-sm text-slate-900">
            <div className="font-medium">{formatAction(log)}</div>
            {(currentRole === "SuperAdmin" || currentRole === "Admin") ? (
              <div className="text-xs text-slate-400">{log.action}</div>
            ) : null}
          </div>
        );
      case "entityType":
        return <span className="text-sm text-slate-600">{log.entityType}</span>;
      case "entityId":
        return <span className="font-mono text-xs text-slate-500">{log.entityId}</span>;
      case "entity":
        return (
          <div className="text-sm text-slate-600">
            <div className="font-medium">{formatFriendlyEntity(log, "entity")}</div>
            <div className="text-xs text-slate-400">{formatEntityType(log.entityType)}</div>
          </div>
        );
      case "item":
        return (
          <div className="text-sm text-slate-600">
            <div className="font-medium">{formatFriendlyEntity(log, "item")}</div>
            <div className="text-xs text-slate-400">{formatEntityType(log.entityType)}</div>
          </div>
        );
      case "trip":
        return <span className="text-sm font-medium text-slate-600">{formatFriendlyEntity(log, "trip")}</span>;
      case "actor":
        return (
          <div className="text-sm text-slate-600">
            <div className="font-medium">{log.actorUsername ?? "-"}</div>
            {currentRole === "SuperAdmin" || currentRole === "Admin" ? (
              <div className="text-xs text-slate-400">{log.actorUserId}</div>
            ) : null}
          </div>
        );
      case "actorRole":
        return <span className="text-xs text-slate-500">{log.actorRole ?? "-"}</span>;
      case "metadata":
        return metadata.isKnown ? (
          <span className="text-xs text-slate-500" title={auditLayout.showRawMetadataTooltip ? metadata.raw : undefined}>
            {metadata.text}
          </span>
        ) : (
          <code
            title={auditLayout.showRawMetadataTooltip ? metadata.raw : undefined}
            className="block max-w-md whitespace-pre-wrap rounded-md bg-slate-50 px-2 py-1 font-mono text-[11px] text-slate-600"
          >
            {metadata.text}
          </code>
        );
      default:
        return null;
    }
  }

  async function loadLogs(targetPage: number) {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      params.set("page", targetPage.toString());
      params.set("pageSize", pageSize.toString());
      if (hasFilter("action") && action.trim()) params.set("action", action.trim());
      if (hasFilter("entityType") && entityType.trim()) params.set("entityType", entityType.trim());
      if (hasFilter("entityId") && entityId.trim()) params.set("entityId", entityId.trim());
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
          {hasFilter("action") ? (
            <div>
              <label className="text-xs uppercase text-slate-500">Action</label>
              <input
                value={action}
                onChange={(e) => setAction(e.target.value)}
                placeholder="MODULE_SETTING_UPDATED"
                className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
              />
            </div>
          ) : null}
          {hasFilter("entityType") ? (
            <div>
              <label className="text-xs uppercase text-slate-500">Entity Type</label>
              <input
                value={entityType}
                onChange={(e) => setEntityType(e.target.value)}
                placeholder="module_setting"
                className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
              />
            </div>
          ) : null}
          {hasFilter("entityId") ? (
            <div>
              <label className="text-xs uppercase text-slate-500">Entity Id</label>
              <input
                value={entityId}
                onChange={(e) => setEntityId(e.target.value)}
                placeholder="GUID"
                className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
              />
            </div>
          ) : null}
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
              {auditLayout.columns.map((column) => (
                <th key={column.key} className="px-4 py-3 text-left">
                  {column.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {logs.map((log) => (
              <tr key={log.id} className="border-t border-slate-100">
                {auditLayout.columns.map((column) => (
                  <td key={column.key} className="px-4 py-3">
                    {renderCell(column.key, log)}
                  </td>
                ))}
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

function getPrimaryAuditRole(roles: string[]) {
  const priority = ["SuperAdmin", "Admin", "Manager", "Dispatcher", "HeadOfFinance", "InventoryOfficer", "Driver"];
  return priority.find((role) => roles.includes(role)) ?? roles[0] ?? "User";
}

function textFromMeta(metadata: AuditMetadata | null, ...keys: string[]) {
  if (!metadata) return null;

  for (const key of keys) {
    const value = readMetaValue(metadata, key);
    if (value !== undefined && value !== null && value !== "") {
      return String(value);
    }
  }

  return null;
}

function readMetaValue(metadata: AuditMetadata, key: string): unknown {
  const direct = metadata[key] ?? metadata[toPascalCase(key)];
  if (direct !== undefined) return direct;

  const after = metadata.after ?? metadata.After;
  if (after && typeof after === "object" && !Array.isArray(after)) {
    const afterMeta = after as AuditMetadata;
    return afterMeta[key] ?? afterMeta[toPascalCase(key)];
  }

  return undefined;
}

function friendlyEntityFallback(log: AuditLogRow) {
  if (log.entityType === "dispatch_trip" || log.entityType === "trip") {
    return tripReference(log.entityId);
  }

  return `${formatEntityType(log.entityType)} ${shortId(log.entityId)}`;
}

function tripReference(entityId: string) {
  return `Trip #${shortId(entityId)}`;
}

function shortId(id: string) {
  return id.replace(/-/g, "").slice(0, 8).toUpperCase();
}

function formatEntityType(entityType: string) {
  return entityType
    .split("_")
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

function toPascalCase(value: string) {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
