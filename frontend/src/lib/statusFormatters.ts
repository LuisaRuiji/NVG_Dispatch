const explicitStatusLabels: Record<string, string> = {
  draft: "Draft",
  submitted: "Submitted",
  pending: "Pending",
  pending_io: "Pending IO",
  pending_manager: "Pending manager",
  pending_finance: "Pending finance",
  pending_ceo: "Pending CEO",
  approved: "Approved",
  issued: "Issued",
  rejected: "Rejected",
  converted_to_trip: "Converted to trip",
  closed: "Closed",
  open: "Open",
  partially_received: "Partially received",
  partially_returned: "Partially returned",
  dispatched: "Dispatched",
  enroute_pickup: "Enroute pickup",
  at_pickup: "At pickup",
  loaded: "Loaded",
  enroute_dropoff: "Enroute dropoff",
  at_dropoff: "At dropoff",
  delivered: "Delivered",
  cancelled: "Cancelled",
  on_hold: "On hold",
  failed_attempt: "Failed attempt",
  uploaded: "Pending verification",
  verified: "Verified",
  missing: "Missing",
  ready: "Ready"
};

export function normalizeStatusKey(status?: string | null) {
  return (status ?? "unknown")
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, "$1_$2")
    .replace(/[\s-]+/g, "_")
    .replace(/_+/g, "_")
    .toLowerCase();
}

export function formatStatusLabel(status?: string | null) {
  const raw = status?.trim();
  if (!raw) return "Unknown";

  const key = normalizeStatusKey(raw);
  const explicit = explicitStatusLabels[key];
  if (explicit) return explicit;

  const words = key
    .split("_")
    .filter(Boolean)
    .map((word, index) => {
      if (word.length <= 3) return word.toUpperCase();
      return index === 0 ? word.charAt(0).toUpperCase() + word.slice(1) : word;
    });

  if (words.length === 0) return raw;
  return words.join(" ");
}
