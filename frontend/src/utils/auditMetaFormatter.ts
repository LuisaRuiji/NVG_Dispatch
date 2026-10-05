export type AuditMetadata = Record<string, unknown>;

const actionAliases: Record<string, string> = {
  DISPATCH_TRIP_STATUS_CHANGED: "TRIP_STATUS_CHANGED",
  DISPATCH_TRIP_ON_HOLD: "TRIP_STATUS_CHANGED",
  DISPATCH_TRIP_CANCELLED: "TRIP_STATUS_CHANGED",
  DISPATCH_TRIP_CLOSED: "TRIP_STATUS_CHANGED",
  DISPATCH_TRIP_DOCUMENT_UPLOADED: "DOCUMENT_UPLOADED",
  DISPATCH_TRIP_DOCUMENT_VERIFIED: "DOCUMENT_VERIFIED",
  DISPATCH_TRIP_DOCUMENT_REJECTED: "DOCUMENT_REJECTED",
  SHIPMENT_REQUEST_CONVERTED: "SHIPMENT_REQUEST_APPROVED",
  ADJUSTMENT_MANAGER_APPROVED: "INVENTORY_ITEM_UPDATED"
};

const actionLabels: Record<string, string> = {
  REQUEST_CREATED: "Request created",
  REQUEST_SUBMITTED: "Request submitted",
  REQUEST_IO_REVIEW_APPROVED: "Request approved for review",
  REQUEST_IO_REVIEW_REJECTED: "Request returned for changes",
  REQUEST_MANAGER_APPROVED: "Request approved",
  REQUEST_MANAGER_REJECTED: "Request rejected",
  REQUEST_ISSUED: "Request issued",
  LOAN_RETURN: "Item returned",
  PO_DRAFT_CREATED: "Purchase order draft created",
  PO_SUBMITTED: "Purchase order submitted",
  PO_APPROVED: "Purchase order approved",
  PO_REJECTED: "Purchase order rejected",
  PO_RECEIVED: "Purchase order received",
  ADJUSTMENT_DRAFT_CREATED: "Stock adjustment created",
  ADJUSTMENT_SUBMITTED: "Stock adjustment submitted",
  ADJUSTMENT_MANAGER_APPROVED: "Stock adjustment approved",
  ADJUSTMENT_MANAGER_REJECTED: "Stock adjustment rejected",
  SUPPLIER_CREATED: "Supplier created",
  SUPPLIER_DEACTIVATED: "Supplier deactivated",
  USER_CREATED: "User created",
  USER_PROFILE_UPDATED: "User profile updated",
  USER_ROLE_ASSIGNED: "User role assigned",
  USER_ROLES_REPLACED: "User roles updated",
  USER_STATUS_UPDATED: "User status updated",
  USER_PASSWORD_RESET: "Password reset",
  USER_PASSWORD_CHANGED: "Password changed",
  USER_DEACTIVATED: "User deactivated",
  DISPATCH_TRIP_CREATED: "Trip created",
  DISPATCH_TRIP_UPDATED: "Trip details updated",
  DISPATCH_TRIP_DISPATCHED: "Trip dispatched",
  DISPATCH_TRIP_STATUS_CHANGED: "Trip status changed",
  DISPATCH_TRIP_STATUS_CORRECTED: "Trip status corrected",
  DISPATCH_TRIP_CANCELLED: "Trip cancelled",
  DISPATCH_TRIP_ON_HOLD: "Trip placed on hold",
  DISPATCH_TRIP_CLOSED: "Trip closed",
  DISPATCH_TRIP_CONFLICT_OVERRIDE: "Schedule conflict overridden",
  DISPATCH_TRIP_DOCUMENT_UPLOADED: "Trip document uploaded",
  DISPATCH_TRIP_DOCUMENT_VERIFIED: "Trip document verified",
  DISPATCH_TRIP_DOCUMENT_REJECTED: "Trip document rejected",
  RECOMMENDATION_ACCEPTED: "Trip recommendation accepted",
  RECOMMENDATION_IGNORED: "Trip recommendation dismissed",
  WAYBILL_GENERATED: "Waybill generated",
  TRIP_RECEIPT_GENERATED: "Trip receipt generated",
  FINANCIAL_FIELD_ACCESSED: "Financial information viewed",
  MODULE_SETTING_UPDATED: "Module setting changed",
  OPTIMIZATION_WEIGHT_SETTINGS_UPDATED: "Dispatch scoring settings updated",
  OPTIMIZATION_PLAN_APPROVED: "Dispatch plan approved",
  OPTIMIZATION_PLAN_DISPATCHED: "Dispatch plan released",
  OPTIMIZATION_PLAN_OVERRIDDEN: "Dispatch plan overridden",
  LOCATION_TRACKING_STARTED: "Location sharing started",
  LOCATION_TRACKING_STOPPED: "Location sharing stopped"
};

const tripStatusLabels = [
  "Draft",
  "Ready for dispatch",
  "Dispatched",
  "En route to pickup",
  "At pickup",
  "Loaded",
  "En route to drop-off",
  "At drop-off",
  "Delivered",
  "Closed",
  "Cancelled",
  "On hold",
  "Failed attempt"
] as const;

const knownActions = new Set([
  "USER_PASSWORD_RESET",
  "USER_PROFILE_UPDATED",
  "TRIP_STATUS_CHANGED",
  "TRIP_ASSIGNED",
  "DOCUMENT_UPLOADED",
  "DOCUMENT_VERIFIED",
  "DOCUMENT_REJECTED",
  "SHIPMENT_REQUEST_APPROVED",
  "INVENTORY_ITEM_UPDATED",
  "FINANCIAL_FIELD_ACCESSED",
  "DISPATCH_TRIP_DISPATCHED",
  "DISPATCH_TRIP_STATUS_CORRECTED",
  ...Object.keys(actionAliases)
]);

export function formatAuditActionLabel(action: string) {
  return actionLabels[action] ?? sentenceCase(action);
}

export function isKnownAuditMetadataAction(action: string) {
  return knownActions.has(action);
}

export function formatAuditMetadata(action: string, metadata: AuditMetadata) {
  const normalizedAction = actionAliases[action] ?? action;

  switch (normalizedAction) {
    case "USER_PASSWORD_RESET":
      return "Password was reset";
    case "USER_PROFILE_UPDATED":
      return joinDetails([
        optionalDetail(metadata, "Username", "username"),
        optionalDetail(metadata, "Email", "email")
      ]);
    case "TRIP_STATUS_CHANGED":
    case "DISPATCH_TRIP_STATUS_CORRECTED": {
      const previous = statusValue(metadata, "previousStatus", "fromStatus");
      const next = statusValue(metadata, "newStatus", "toStatus", "status");
      if (previous && next) return `Moved from ${previous} to ${next}`;
      if (next) return `Moved to ${next}`;
      return "Trip status was updated";
    }
    case "DISPATCH_TRIP_DISPATCHED":
      return "Trip is ready for the driver to begin";
    case "TRIP_ASSIGNED":
      return joinDetails([
        optionalDetail(metadata, "Driver", "driverName", "driverUsername", "driverUserId"),
        optionalDetail(metadata, "Truck", "truckPlate", "truckAssetCode", "truckAssetId")
      ]);
    case "DOCUMENT_UPLOADED":
      return `${valueOr(metadata, "Document", "documentType", "type")} uploaded${versionSuffix(metadata)}`;
    case "DOCUMENT_VERIFIED":
      return `${valueOr(metadata, "Document", "documentType", "type")} verified`;
    case "DOCUMENT_REJECTED": {
      const reason = readableValue(readValue(metadata, "reason") ?? readValue(metadata, "remarks"));
      return reason ? `${valueOr(metadata, "Document", "documentType", "type")} rejected: ${reason}` : "Document rejected";
    }
    case "SHIPMENT_REQUEST_APPROVED":
      return "Shipment request approved and converted to a trip";
    case "INVENTORY_ITEM_UPDATED": {
      const before = readableValue(readValue(metadata, "previousQty") ?? readValue(metadata, "beforeQty"));
      const after = readableValue(readValue(metadata, "newQty") ?? readValue(metadata, "quantity"));
      if (before && after) return `Quantity changed from ${before} to ${after}`;
      return summarizeMetadata(metadata);
    }
    case "FINANCIAL_FIELD_ACCESSED":
      return `${valueOr(metadata, "Financial field", "fieldName")} viewed`;
    default:
      return summarizeMetadata(metadata);
  }
}

export function parseAuditMetadata(raw?: string | null): AuditMetadata | null {
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as unknown;
    return parsed && typeof parsed === "object" && !Array.isArray(parsed)
      ? parsed as AuditMetadata
      : { value: parsed };
  } catch {
    return { value: raw };
  }
}

export function stringifyMetadata(metadata: AuditMetadata) {
  return JSON.stringify(metadata, null, 2);
}

function summarizeMetadata(metadata: AuditMetadata) {
  const candidates: Array<[string, string[]]> = [
    ["Status", ["status", "newStatus", "toStatus"]],
    ["Driver", ["driverName", "driverUsername"]],
    ["Truck", ["truckPlate", "truckAssetCode"]],
    ["Document", ["documentType", "type"]],
    ["Reason", ["reason", "remarks"]],
    ["Notes", ["notes"]],
    ["Quantity", ["quantity", "newQty"]]
  ];

  const details = candidates.flatMap(([label, keys]) => {
    const value = keys.map((key) => readValue(metadata, key)).find((item) => item !== undefined && item !== null && item !== "");
    if (value === undefined) return [];
    const display = label === "Status" ? formatTripStatus(value) : readableValue(value);
    return display ? [`${label}: ${display}`] : [];
  });

  return details.slice(0, 3).join(" · ") || "Change recorded";
}

function optionalDetail(metadata: AuditMetadata, label: string, ...keys: string[]) {
  const value = keys.map((key) => readValue(metadata, key)).find((item) => item !== undefined && item !== null && item !== "");
  const display = readableValue(value);
  return display ? `${label}: ${display}` : null;
}

function joinDetails(details: Array<string | null>) {
  return details.filter(Boolean).join(" · ") || "Change recorded";
}

function versionSuffix(metadata: AuditMetadata) {
  const version = readableValue(readValue(metadata, "version"));
  return version ? ` · Version ${version}` : "";
}

function valueOr(metadata: AuditMetadata, fallback: string, ...keys: string[]) {
  const value = keys.map((key) => readValue(metadata, key)).find((item) => item !== undefined && item !== null && item !== "");
  return readableValue(value) ?? fallback;
}

function statusValue(metadata: AuditMetadata, ...keys: string[]) {
  const value = keys.map((key) => readValue(metadata, key)).find((item) => item !== undefined && item !== null && item !== "");
  return value === undefined ? null : formatTripStatus(value);
}

function formatTripStatus(value: unknown) {
  if (typeof value === "number" || (typeof value === "string" && /^\d+$/.test(value))) {
    const label = tripStatusLabels[Number(value)];
    return label ?? `Status ${value}`;
  }

  const normalized = String(value)
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/_/g, " ")
    .trim()
    .toLowerCase();
  return normalized
    .replace(/^enroute /, "en route ")
    .replace(/dropoff/g, "drop-off")
    .replace(/^./, (char) => char.toUpperCase());
}

function readableValue(value: unknown): string | null {
  if (value === undefined || value === null || value === "") return null;
  if (typeof value === "boolean") return value ? "Yes" : "No";
  if (typeof value === "object") return null;
  return String(value).replace(/_/g, " ");
}

function readValue(metadata: AuditMetadata, key: string) {
  const direct = metadata[key] ?? metadata[toPascalCase(key)];
  if (direct !== undefined) return direct;

  for (const containerKey of ["after", "After", "before", "Before"]) {
    const nested = metadata[containerKey];
    if (nested && typeof nested === "object" && !Array.isArray(nested)) {
      const nestedMetadata = nested as AuditMetadata;
      const value = nestedMetadata[key] ?? nestedMetadata[toPascalCase(key)];
      if (value !== undefined) return value;
    }
  }

  return undefined;
}

function sentenceCase(value: string) {
  const words = value.toLowerCase().split("_").filter(Boolean);
  if (words[0] === "dispatch") words.shift();
  const sentence = words.join(" ");
  return sentence ? sentence.charAt(0).toUpperCase() + sentence.slice(1) : "Activity recorded";
}

function toPascalCase(value: string) {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
