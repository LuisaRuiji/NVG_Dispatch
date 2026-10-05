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
  formatAuditActionLabel,
  formatAuditMetadata,
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

type AuditFilters = {
  action: string;
  entityType: string;
  entityId: string;
  from: string;
  to: string;
};

const auditActionOptions = [
  ["DISPATCH_TRIP_CREATED", "Trip created"],
  ["DISPATCH_TRIP_UPDATED", "Trip details updated"],
  ["DISPATCH_TRIP_DISPATCHED", "Trip dispatched"],
  ["DISPATCH_TRIP_STATUS_CHANGED", "Trip status changed"],
  ["DISPATCH_TRIP_STATUS_CORRECTED", "Trip status corrected"],
  ["DISPATCH_TRIP_ON_HOLD", "Trip placed on hold"],
  ["DISPATCH_TRIP_CANCELLED", "Trip cancelled"],
  ["DISPATCH_TRIP_CLOSED", "Trip closed"],
  ["DISPATCH_TRIP_DOCUMENT_UPLOADED", "Trip document uploaded"],
  ["DISPATCH_TRIP_DOCUMENT_VERIFIED", "Trip document verified"],
  ["DISPATCH_TRIP_DOCUMENT_REJECTED", "Trip document rejected"],
  ["RECOMMENDATION_ACCEPTED", "Trip recommendation accepted"],
  ["RECOMMENDATION_IGNORED", "Trip recommendation dismissed"],
  ["WAYBILL_GENERATED", "Waybill generated"],
  ["MODULE_SETTING_UPDATED", "Module setting changed"]
] as const;

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
    return formatAuditActionLabel(log.action);
  }

  function formatMetadata(log: AuditLogRow): { text: string; raw: string } {
    const parsed = parseAuditMetadata(log.metadata);
    const raw = parsed ? stringifyMetadata(parsed) : "";

    if (log.action === "MODULE_SETTING_UPDATED") {
      const meta = parsed as ModuleSettingMeta | null;
      if (!meta) return { text: "No additional details", raw };

      const key = meta.ModuleKey ?? meta.moduleKey ?? "";
      const name = key ? moduleNameMap[key] ?? key : "Unknown module";
      const enabled = meta.IsEnabled ?? meta.isEnabled;
      const status = enabled ? "Enabled" : "Disabled";
      const noteValue = meta.Notes ?? meta.notes;
      const notes = noteValue ? `Notes: ${noteValue}` : null;
      return { text: [name, status, notes].filter(Boolean).join(" · "), raw };
    }

    if (!parsed) {
      return { text: "No additional details", raw: "" };
    }

    return {
      text: formatAuditMetadata(log.action, parsed as AuditMetadata),
      raw
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
      case "time": {
        const timestamp = formatAuditTimestamp(log.createdAt);
        return (
          <time dateTime={log.createdAt} className="block min-w-32">
            <span className="block text-sm font-medium text-foreground">{timestamp.date}</span>
            <span className="mt-0.5 block text-xs text-muted-foreground">{timestamp.time}</span>
          </time>
        );
      }
      case "action":
        return (
          <div className="min-w-44 text-sm text-foreground">
            <div className="font-semibold">{formatAction(log)}</div>
            {(currentRole === "SuperAdmin" || currentRole === "Admin") ? (
              <div className="mt-0.5 font-mono text-[10px] text-muted-foreground">{log.action}</div>
            ) : null}
          </div>
        );
      case "entityType":
        return <span className="text-sm text-foreground">{formatEntityType(log.entityType)}</span>;
      case "entityId":
        return <span className="font-mono text-xs text-muted-foreground">{log.entityId}</span>;
      case "entity":
        return (
          <div className="min-w-36 text-sm text-foreground">
            <div className="font-semibold">{formatFriendlyEntity(log, "entity")}</div>
            <div className="mt-0.5 text-xs text-muted-foreground">{formatEntityType(log.entityType)}</div>
          </div>
        );
      case "item":
        return (
          <div className="text-sm text-foreground">
            <div className="font-semibold">{formatFriendlyEntity(log, "item")}</div>
            <div className="mt-0.5 text-xs text-muted-foreground">{formatEntityType(log.entityType)}</div>
          </div>
        );
      case "trip":
        return <span className="font-mono text-sm font-semibold text-foreground">{formatFriendlyEntity(log, "trip")}</span>;
      case "actor":
        return (
          <div className="min-w-32 text-sm text-foreground">
            <div className="font-medium">{log.actorUsername ?? "System"}</div>
            <div className="mt-0.5 text-xs text-muted-foreground">{log.actorRole ?? "Automated activity"}</div>
            {currentRole === "SuperAdmin" || currentRole === "Admin" ? (
              <div className="mt-0.5 font-mono text-[10px] text-muted-foreground">{log.actorUserId}</div>
            ) : null}
          </div>
        );
      case "actorRole":
        return <span className="text-xs text-muted-foreground">{log.actorRole ?? "System"}</span>;
      case "metadata":
        return (
          <div className="max-w-lg text-sm text-muted-foreground">
            <span>{metadata.text}</span>
            {(currentRole === "SuperAdmin" || currentRole === "Admin") && metadata.raw ? (
              <details className="mt-1.5">
                <summary className="w-fit cursor-pointer text-xs font-medium text-muted-foreground hover:text-foreground">
                  Technical details
                </summary>
                <pre className="mt-2 max-h-48 overflow-auto whitespace-pre-wrap rounded-lg border border-border bg-muted/50 p-3 font-mono text-[10px] leading-4 text-muted-foreground">
                  {metadata.raw}
                </pre>
              </details>
            ) : null}
          </div>
        );
      default:
        return null;
    }
  }

  async function loadLogs(targetPage: number, filterOverrides?: AuditFilters) {
    try {
      setLoading(true);
      const filters = filterOverrides ?? { action, entityType, entityId, from, to };
      const params = new URLSearchParams();
      params.set("page", targetPage.toString());
      params.set("pageSize", pageSize.toString());
      if (hasFilter("action") && filters.action.trim()) params.set("action", filters.action.trim());
      if (hasFilter("entityType") && filters.entityType.trim()) params.set("entityType", filters.entityType.trim());
      if (hasFilter("entityId") && filters.entityId.trim()) params.set("entityId", filters.entityId.trim());
      if (filters.from) params.set("from", localDateBoundary(filters.from, "start").toISOString());
      if (filters.to) params.set("to", localDateBoundary(filters.to, "end").toISOString());

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
        title="Activity history"
        description="See what changed, who made the change, and when it happened."
      />

      <section aria-label="Activity filters" className="rounded-[10px] border border-border bg-card p-5">
        <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="text-sm font-semibold text-foreground">Find an activity</h2>
            <p className="mt-1 text-xs text-muted-foreground">Narrow the history by activity type or date range.</p>
          </div>
          <span className="rounded-md bg-muted px-2.5 py-1 text-xs font-medium tabular-nums text-muted-foreground">
            {totalCount.toLocaleString()} {totalCount === 1 ? "entry" : "entries"}
          </span>
        </div>

        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-5">
          {hasFilter("action") ? (
            <div>
              <label htmlFor="audit-action" className="text-xs font-semibold uppercase tracking-[0.08em] text-muted-foreground">
                Activity type
              </label>
              <select
                id="audit-action"
                value={action}
                onChange={(e) => setAction(e.target.value)}
                className="mt-2 h-10 w-full rounded-lg border border-border bg-background px-3 text-sm text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30"
              >
                <option value="">All activity types</option>
                {auditActionOptions.map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </select>
            </div>
          ) : null}
          {hasFilter("entityType") ? (
            <div>
              <label htmlFor="audit-record-type" className="text-xs font-semibold uppercase tracking-[0.08em] text-muted-foreground">Record type</label>
              <input
                id="audit-record-type"
                value={entityType}
                onChange={(e) => setEntityType(e.target.value)}
                placeholder="e.g. dispatch trip"
                className="mt-2 h-10 w-full rounded-lg border border-border bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30"
              />
            </div>
          ) : null}
          {hasFilter("entityId") ? (
            <div>
              <label htmlFor="audit-record-id" className="text-xs font-semibold uppercase tracking-[0.08em] text-muted-foreground">Record ID</label>
              <input
                id="audit-record-id"
                value={entityId}
                onChange={(e) => setEntityId(e.target.value)}
                placeholder="Paste a record ID"
                className="mt-2 h-10 w-full rounded-lg border border-border bg-background px-3 font-mono text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30"
              />
            </div>
          ) : null}
          <div>
            <label htmlFor="audit-from" className="text-xs font-semibold uppercase tracking-[0.08em] text-muted-foreground">From date</label>
            <input
              id="audit-from"
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
              className="mt-2 h-10 w-full rounded-lg border border-border bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30"
            />
          </div>
          <div>
            <label htmlFor="audit-to" className="text-xs font-semibold uppercase tracking-[0.08em] text-muted-foreground">To date</label>
            <input
              id="audit-to"
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
              className="mt-2 h-10 w-full rounded-lg border border-border bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30"
            />
          </div>
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={() => loadLogs(1)}
            disabled={loading}
            className="h-10 rounded-lg bg-primary px-4 text-sm font-semibold text-primary-foreground transition-colors hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 disabled:opacity-60"
          >
            {loading ? "Loading…" : "Apply filters"}
          </button>
          <button
            type="button"
            onClick={() => {
              const clearedFilters = { action: "", entityType: "", entityId: "", from: "", to: "" };
              setAction("");
              setEntityType("");
              setEntityId("");
              setFrom("");
              setTo("");
              loadLogs(1, clearedFilters);
            }}
            disabled={loading}
            className="h-10 rounded-lg border border-border bg-background px-4 text-sm font-medium text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30 disabled:opacity-60"
          >
            Clear filters
          </button>
        </div>
      </section>

      <section aria-labelledby="activity-list-heading" className="mt-6 rounded-[10px] border border-border bg-card p-5">
        <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 id="activity-list-heading" className="text-base font-semibold text-foreground">Recorded activity</h2>
            <p className="mt-1 text-xs text-muted-foreground">Newest entries appear first. Audit records cannot be edited.</p>
          </div>
          <span className="text-xs text-muted-foreground">Page {page} of {totalPages}</span>
        </div>

        <DataTable className="rounded-[10px]">
          <thead className="bg-muted/60 text-xs uppercase tracking-[0.06em] text-muted-foreground">
            <tr>
              {auditLayout.columns.map((column) => (
                <th key={column.key} scope="col" className="whitespace-nowrap px-4 py-3 text-left font-semibold">
                  {column.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {logs.map((log) => (
              <tr key={log.id} className="border-t border-border align-top transition-colors hover:bg-muted/30">
                {auditLayout.columns.map((column) => (
                  <td key={column.key} className="px-4 py-4">
                    {renderCell(column.key, log)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </DataTable>
        {logs.length === 0 && !loading ? (
          <div className="mt-6">
            <EmptyState title="No activity found" description="Clear a filter or choose a wider date range." />
          </div>
        ) : null}

        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 text-sm text-muted-foreground">
          <span>
            Showing {logs.length.toLocaleString()} of {totalCount.toLocaleString()} entries
          </span>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => loadLogs(Math.max(1, page - 1))}
              disabled={loading || page <= 1}
              className="h-9 rounded-lg border border-border bg-background px-3 text-xs font-medium text-foreground hover:bg-muted disabled:opacity-50"
            >
              Previous
            </button>
            <button
              type="button"
              onClick={() => loadLogs(Math.min(totalPages, page + 1))}
              disabled={loading || page >= totalPages}
              className="h-9 rounded-lg border border-border bg-background px-3 text-xs font-medium text-foreground hover:bg-muted disabled:opacity-50"
            >
              Next
            </button>
          </div>
        </div>
      </section>
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

function formatAuditTimestamp(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return { date: "Unknown date", time: "" };

  return {
    date: new Intl.DateTimeFormat(undefined, {
      month: "short",
      day: "numeric",
      year: "numeric"
    }).format(date),
    time: new Intl.DateTimeFormat(undefined, {
      hour: "numeric",
      minute: "2-digit",
      second: "2-digit"
    }).format(date)
  };
}

function localDateBoundary(value: string, boundary: "start" | "end") {
  return new Date(`${value}T${boundary === "start" ? "00:00:00.000" : "23:59:59.999"}`);
}
