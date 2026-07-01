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
  ...Object.keys(actionAliases)
]);

export function isKnownAuditMetadataAction(action: string) {
  return knownActions.has(action);
}

export function formatAuditMetadata(action: string, metadata: AuditMetadata) {
  const normalizedAction = actionAliases[action] ?? action;

  switch (normalizedAction) {
    case "USER_PASSWORD_RESET":
      return "Password was reset";
    case "USER_PROFILE_UPDATED":
      return `Username changed to ${text(metadata, "username")}, email changed to ${text(metadata, "email")}`;
    case "TRIP_STATUS_CHANGED":
      return `Status changed from ${text(metadata, "previousStatus", "fromStatus")} to ${text(metadata, "newStatus", "toStatus", "status")}`;
    case "TRIP_ASSIGNED":
      return `Assigned to driver ${text(metadata, "driverName", "driverUsername", "driverUserId")} with truck ${text(metadata, "truckPlate", "truckAssetCode", "truckAssetId")}`;
    case "DOCUMENT_UPLOADED":
      return `${text(metadata, "documentType", "type")} uploaded (version ${text(metadata, "version")})`;
    case "DOCUMENT_VERIFIED":
      return `${text(metadata, "documentType", "type")} verified by ${text(metadata, "actorName", "verifiedByUsername")}`;
    case "DOCUMENT_REJECTED":
      return `${text(metadata, "documentType", "type")} rejected - reason: ${text(metadata, "reason", "remarks")}`;
    case "SHIPMENT_REQUEST_APPROVED":
      return "Shipment request approved and converted to trip";
    case "INVENTORY_ITEM_UPDATED":
      return `${text(metadata, "itemName", "inventoryName")} quantity changed from ${text(metadata, "previousQty", "beforeQty")} to ${text(metadata, "newQty", "quantity")}`;
    case "FINANCIAL_FIELD_ACCESSED":
      return `${text(metadata, "fieldName")} viewed by ${text(metadata, "actorRole")}`;
    default:
      return stringifyMetadata(metadata);
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

function text(metadata: AuditMetadata, ...keys: string[]) {
  for (const key of keys) {
    const value = readValue(metadata, key);
    if (value !== undefined && value !== null && value !== "") {
      return String(value);
    }
  }
  return "unknown";
}

function readValue(metadata: AuditMetadata, key: string) {
  const direct = metadata[key] ?? metadata[toPascalCase(key)];
  if (direct !== undefined) return direct;

  const after = metadata.after;
  if (after && typeof after === "object" && !Array.isArray(after)) {
    return (after as AuditMetadata)[key] ?? (after as AuditMetadata)[toPascalCase(key)];
  }

  return undefined;
}

function toPascalCase(value: string) {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
