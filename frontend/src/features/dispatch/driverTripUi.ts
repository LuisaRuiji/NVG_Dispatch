import type {
  DispatchTripDetail,
  DispatchTripDocumentChecklist,
  DispatchTripListItem,
  TripDocumentState,
  TripDocumentType,
  TripStatus
} from "./types";

type DocumentStateLike = TripDocumentState | "PENDING" | "READY";
type DocumentChecklistLike = {
  type: TripDocumentType;
  state?: DocumentStateLike | null;
};

const documentLabels: Record<TripDocumentType, string> = {
  ATW: "ATW",
  EIR: "EIR",
  GATE_PASS: "Gate Pass",
  DR: "DR",
  POD: "POD",
  WAYBILL: "Waybill"
};

// ATW is supplied and managed by dispatch. Drivers can view it but must never upload it.
const driverUploadableDocTypes: TripDocumentType[] = ["EIR", "GATE_PASS", "DR", "POD"];
const lockedStatuses: TripStatus[] = ["CLOSED", "CANCELLED", "FAILED_ATTEMPT", "ON_HOLD"];

export function formatDocumentLabel(type: TripDocumentType | string) {
  if (type in documentLabels) {
    return documentLabels[type as TripDocumentType];
  }

  return type
    .split("_")
    .filter(Boolean)
    .map((word) => (word.length <= 3 ? word.toUpperCase() : word.charAt(0) + word.slice(1).toLowerCase()))
    .join(" ");
}

export function formatDocumentStateLabel(state?: DocumentStateLike | null) {
  switch (state) {
    case "MISSING":
      return "Missing";
    case "UPLOADED":
      return "Pending verification";
    case "VERIFIED":
      return "Verified";
    case "REJECTED":
      return "Rejected";
    case "PENDING":
      return "Pending";
    case "READY":
      return "Ready";
    default:
      return "Missing";
  }
}

export function getDocumentState(
  documents: DocumentChecklistLike[] | undefined,
  type: TripDocumentType
): DocumentStateLike {
  return documents?.find((doc) => doc.type === type)?.state ?? "MISSING";
}

export function isDocumentAttentionState(state?: DocumentStateLike | null) {
  return state === "MISSING" || state === "UPLOADED" || state === "REJECTED" || state === "PENDING";
}

export function getDocumentBlockerLabel(doc: DocumentChecklistLike) {
  const label = formatDocumentLabel(doc.type);
  switch (doc.state ?? "MISSING") {
    case "MISSING":
      return `${label} missing`;
    case "REJECTED":
      return `${label} rejected`;
    case "UPLOADED":
      return `${label} pending verification`;
    case "PENDING":
      return `${label} pending`;
    default:
      return null;
  }
}

export function getDriverDocumentBlockers(
  documents: DocumentChecklistLike[] | undefined,
  fallbackCount = 0
) {
  const blockers =
    documents
      ?.map((doc) => getDocumentBlockerLabel(doc))
      .filter((label): label is string => Boolean(label)) ?? [];

  if (blockers.length > 0) {
    return blockers;
  }

  if (fallbackCount > 0) {
    return [`${fallbackCount} ${fallbackCount === 1 ? "document needs" : "documents need"} attention`];
  }

  return [];
}

export function summarizeDocumentBlockers(blockers: string[], maxSpecific = 2) {
  if (blockers.length <= maxSpecific) {
    return blockers;
  }

  return [...blockers.slice(0, maxSpecific), `${blockers.length} documents need attention`];
}

export function canDriverUploadDocument(docType: TripDocumentType, status: TripStatus) {
  if (!driverUploadableDocTypes.includes(docType) || lockedStatuses.includes(status)) {
    return false;
  }

  if ((docType === "EIR" || docType === "GATE_PASS") && !isStatusAtLeast(status, "AT_PICKUP")) {
    return false;
  }

  if (docType === "DR" && !isStatusAtLeast(status, "AT_DROPOFF")) {
    return false;
  }

  if (docType === "POD" && !isStatusAtLeast(status, "AT_DROPOFF")) {
    return false;
  }

  return true;
}

export function getPrimaryDriverUploadType(trip: DispatchTripDetail) {
  const priority: TripDocumentType[] = ["EIR", "GATE_PASS", "DR", "POD"];
  return (
    priority.find((type) => {
      const state = getDocumentState(trip.documents, type);
      return canDriverUploadDocument(type, trip.status) && isDocumentAttentionState(state);
    }) ??
    priority.find((type) => canDriverUploadDocument(type, trip.status)) ??
    null
  );
}

export function getDeliveryDocumentBlockers(documents: DocumentChecklistLike[] | undefined) {
  const requiredBeforeDelivery: TripDocumentType[] = ["EIR", "GATE_PASS", "DR", "POD"];
  return requiredBeforeDelivery
    .filter((type) => {
      const state = getDocumentState(documents, type);
      return state === "MISSING" || state === "REJECTED";
    })
    .map((type) => `${formatDocumentLabel(type)} must be uploaded before delivery`);
}

export function getDriverTripNextAction(
  trip: Pick<DispatchTripListItem, "status" | "documents" | "podPending"> | DispatchTripDetail
) {
  const documents = "documents" in trip ? (trip.documents as DispatchTripDocumentChecklist[]) : [];
  const atwState = getDocumentState(documents, "ATW");
  const eirState = getDocumentState(documents, "EIR");
  const gatePassState = getDocumentState(documents, "GATE_PASS");
  const drState = getDocumentState(documents, "DR");
  const podState = getDocumentState(documents, "POD");

  switch (trip.status) {
    case "DRAFT":
      return "Waiting for dispatch";
    case "DISPATCHED":
      return "Next: Start pickup";
    case "ENROUTE_PICKUP":
      return "Next: Arrive at pickup";
    case "AT_PICKUP":
      if (isDocumentAttentionState(atwState)) return "ATW needs dispatch attention";
      if (isDocumentAttentionState(eirState)) return "Next: Upload EIR";
      if (isDocumentAttentionState(gatePassState)) return "Next: Upload Gate Pass";
      return "Next: Confirm loaded";
    case "LOADED":
      return "Next: Depart pickup";
    case "ENROUTE_DROPOFF":
      return "Next: Arrive at dropoff";
    case "AT_DROPOFF":
      if (isDocumentAttentionState(podState)) return "Next: Upload POD";
      if (isDocumentAttentionState(drState)) return "Next: Upload DR";
      return "Next: Confirm delivery";
    case "DELIVERED":
      if (trip.podPending || isDocumentAttentionState(podState)) return "POD needs dispatcher verification";
      return "Waiting for closure";
    case "ON_HOLD":
      return "Trip is on hold";
    case "FAILED_ATTEMPT":
      return "Waiting for dispatcher resolution";
    case "CANCELLED":
      return "Trip cancelled";
    case "CLOSED":
      return "Trip closed";
    default:
      return "Open trip for next step";
  }
}

function isStatusAtLeast(current: TripStatus, required: TripStatus) {
  const order: TripStatus[] = [
    "DRAFT",
    "READY_FOR_DISPATCH",
    "DISPATCHED",
    "ENROUTE_PICKUP",
    "AT_PICKUP",
    "LOADED",
    "ENROUTE_DROPOFF",
    "AT_DROPOFF",
    "DELIVERED",
    "CLOSED"
  ];
  return order.indexOf(current) >= order.indexOf(required);
}
