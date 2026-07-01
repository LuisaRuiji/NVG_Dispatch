import { useMemo } from "react";

export type AuditLogColumnKey =
  | "time"
  | "action"
  | "entityType"
  | "entityId"
  | "entity"
  | "item"
  | "trip"
  | "actor"
  | "actorRole"
  | "metadata";

export type AuditLogFilterKey = "action" | "entityType" | "entityId" | "dateRange";

export type AuditLogColumnConfig = {
  key: AuditLogColumnKey;
  label: string;
};

export type AuditLogLayoutConfig = {
  columns: AuditLogColumnConfig[];
  filters: AuditLogFilterKey[];
  showRawMetadataTooltip: boolean;
};

export function useAuditLogColumns(role: string): AuditLogLayoutConfig {
  return useMemo(() => {
    if (role === "SuperAdmin" || role === "Admin") {
      return {
        columns: [
          { key: "time", label: "Time" },
          { key: "action", label: "Action" },
          { key: "entityType", label: "Entity Type" },
          { key: "entityId", label: "Entity Id" },
          { key: "actor", label: "Actor" },
          { key: "actorRole", label: "Actor Role" },
          { key: "metadata", label: "Metadata" }
        ],
        filters: ["action", "entityType", "entityId", "dateRange"],
        showRawMetadataTooltip: role === "SuperAdmin"
      };
    }

    if (role === "InventoryOfficer") {
      return {
        columns: [
          { key: "time", label: "Time" },
          { key: "action", label: "Action" },
          { key: "item", label: "Item" },
          { key: "metadata", label: "Metadata" }
        ],
        filters: ["action", "dateRange"],
        showRawMetadataTooltip: false
      };
    }

    if (role === "Driver") {
      return {
        columns: [
          { key: "time", label: "Time" },
          { key: "action", label: "Action" },
          { key: "trip", label: "Trip" },
          { key: "metadata", label: "Metadata" }
        ],
        filters: ["dateRange"],
        showRawMetadataTooltip: false
      };
    }

    return {
      columns: [
        { key: "time", label: "Time" },
        { key: "action", label: "Action" },
        { key: "entity", label: "Entity" },
        { key: "metadata", label: "Metadata" }
      ],
      filters: ["action", "dateRange"],
      showRawMetadataTooltip: false
    };
  }, [role]);
}
