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
};

export function useAuditLogColumns(role: string): AuditLogLayoutConfig {
  return useMemo(() => {
    if (role === "SuperAdmin" || role === "Admin") {
      return {
        columns: [
          { key: "time", label: "Date & time" },
          { key: "action", label: "Activity" },
          { key: "entityType", label: "Record type" },
          { key: "entityId", label: "Record ID" },
          { key: "actor", label: "Performed by" },
          { key: "actorRole", label: "Role" },
          { key: "metadata", label: "Details" }
        ],
        filters: ["action", "entityType", "entityId", "dateRange"]
      };
    }

    if (role === "InventoryOfficer") {
      return {
        columns: [
          { key: "time", label: "Date & time" },
          { key: "action", label: "Activity" },
          { key: "item", label: "Item" },
          { key: "metadata", label: "Details" },
          { key: "actor", label: "Performed by" }
        ],
        filters: ["action", "dateRange"]
      };
    }

    if (role === "Driver") {
      return {
        columns: [
          { key: "time", label: "Date & time" },
          { key: "action", label: "Activity" },
          { key: "trip", label: "Trip" },
          { key: "metadata", label: "Details" }
        ],
        filters: ["dateRange"]
      };
    }

    return {
      columns: [
        { key: "time", label: "Date & time" },
        { key: "action", label: "Activity" },
        { key: "entity", label: "Record" },
        { key: "metadata", label: "Details" },
        { key: "actor", label: "Performed by" }
      ],
      filters: ["action", "dateRange"]
    };
  }, [role]);
}
