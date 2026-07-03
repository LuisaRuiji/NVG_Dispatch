import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import DataTable from "@/components/DataTable";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import TripMap from "@/components/dispatch/TripMap";
import LocationPinPicker from "@/components/dispatch/LocationPinPicker";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { ApiRequestError, api, apiOptional } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import { getMe } from "@/features/auth/authStore";
import { useDispatchHub } from "@/hooks/useDispatchHub";
import type {
  DispatchTripDetail,
  DispatchTripDocument,
  DispatchTripDocumentLink,
  DispatchTripDocumentVersion,
  DispatchTripHistory,
  DispatchTripSummary,
  GeneratedWaybill,
  TripDocumentType,
  TripStatus
} from "./types";
import { operationalFlow, statusLabels } from "./types";
import { Check, ExternalLink, XCircle } from "lucide-react";

type CustomerOption = { id: string; name: string };
type DriverOption = { id: string; username: string };
type TruckOption = { id: string; assetCode: string };

type ScheduleForm = {
  customerId: string;
  pickupLocation: string;
  pickupLatitude?: number | null;
  pickupLongitude?: number | null;
  pickupScheduledAt: string;
  dropoffLocation: string;
  dropoffLatitude?: number | null;
  dropoffLongitude?: number | null;
  dropoffScheduledAt: string;
  driverUserId: string;
  truckAssetId: string;
  containerNumber: string;
  eirNumber: string;
  bookingNumber: string;
  shippingLine: string;
  notes: string;
  changeRemarks: string;
};

type CorrectStatusForm = {
  toStatus: TripStatus | "";
  eventAt: string;
  remarks: string;
};

type VersionStateFilter = "ALL" | "UPLOADED" | "VERIFIED" | "REJECTED";

type AssignmentConflictItem = {
  tripId?: string;
  tripReference?: string;
  driverUserId?: string | null;
  driverUsername?: string | null;
  truckAssetId?: string | null;
  truckAssetCode?: string | null;
  windowStart?: string;
  windowEnd?: string;
  message?: string;
};

type AssignmentConflictState = {
  summary: string;
  conflicts: AssignmentConflictItem[];
};

type ActionModal =
  | { type: "HOLD"; remarks: string; eventAt: string }
  | { type: "FAILED"; remarks: string; eventAt: string }
  | { type: "RESOLVE_FAILED"; remarks: string; eventAt: string }
  | { type: "CANCEL"; remarks: string; eventAt: string }
  | { type: "CLOSE" }
  | { type: "DOC_UPLOAD"; docType: TripDocumentType; storageKey: string }
  | { type: "DOC_REJECT"; docId: string; remarks: string }
  | null;

const docTypes: TripDocumentType[] = ["ATW", "EIR", "GATE_PASS", "DR", "POD", "WAYBILL"];
const failedAttemptEligible: TripStatus[] = [
  "ENROUTE_PICKUP",
  "AT_PICKUP",
  "ENROUTE_DROPOFF",
  "AT_DROPOFF"
];
const docStateClasses: Record<string, string> = {
  MISSING: "border-slate-200 text-slate-500",
  UPLOADED: "border-amber-200 text-amber-700",
  VERIFIED: "border-emerald-200 text-emerald-700",
  REJECTED: "border-rose-200 text-rose-700"
};

const toLocalInput = (iso?: string | null) => {
  if (!iso) return "";
  const date = new Date(iso);
  const offset = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
};

const fromLocalInput = (value: string) => {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
};

const toTitleCaseKey = (key: string) => key.charAt(0).toUpperCase() + key.slice(1);

const readValue = (source: Record<string, unknown>, key: string) =>
  source[key] ?? source[toTitleCaseKey(key)];

const formatWindowRange = (startRaw?: string, endRaw?: string) => {
  if (!startRaw || !endRaw) return null;
  const start = new Date(startRaw);
  const end = new Date(endRaw);
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return null;
  return `${start.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })} to ${end.toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit"
  })}`;
};

const buildConflictLine = (item: AssignmentConflictItem) => {
  if (item.message) {
    return item.message;
  }

  const driver = item.driverUsername ? `Driver ${item.driverUsername}` : null;
  const truck = item.truckAssetCode ? `truck ${item.truckAssetCode}` : null;
  const assignment = driver && truck ? `${driver} or ${truck}` : driver ?? truck ?? "Selected assignment";
  const tripRef = item.tripReference ? `Trip ${item.tripReference}` : "another trip";
  const window = formatWindowRange(item.windowStart, item.windowEnd);
  return window
    ? `${assignment} is assigned to ${tripRef} from ${window}.`
    : `${assignment} is assigned to ${tripRef}.`;
};

const parseAssignmentConflict = (error: unknown): AssignmentConflictState | null => {
  if (!(error instanceof ApiRequestError)) {
    return null;
  }

  if (error.errorCode !== "CONFLICT" || !error.details || typeof error.details !== "object") {
    return null;
  }

  const details = error.details as Record<string, unknown>;
  const rawConflicts = readValue(details, "conflicts");
  if (!Array.isArray(rawConflicts)) {
    return null;
  }

  const conflicts = rawConflicts.map((entry) => {
    const item = (entry ?? {}) as Record<string, unknown>;
    return {
      tripId: String(readValue(item, "tripId") ?? ""),
      tripReference: String(readValue(item, "tripReference") ?? ""),
      driverUserId: readValue(item, "driverUserId") ? String(readValue(item, "driverUserId")) : null,
      driverUsername: readValue(item, "driverUsername") ? String(readValue(item, "driverUsername")) : null,
      truckAssetId: readValue(item, "truckAssetId") ? String(readValue(item, "truckAssetId")) : null,
      truckAssetCode: readValue(item, "truckAssetCode") ? String(readValue(item, "truckAssetCode")) : null,
      windowStart: readValue(item, "windowStart") ? String(readValue(item, "windowStart")) : undefined,
      windowEnd: readValue(item, "windowEnd") ? String(readValue(item, "windowEnd")) : undefined,
      message: readValue(item, "message") ? String(readValue(item, "message")) : undefined
    } as AssignmentConflictItem;
  });

  const summaryRaw = readValue(details, "summary");
  const summary = typeof summaryRaw === "string" && summaryRaw.trim().length > 0
    ? summaryRaw
    : error.message;

  return { summary, conflicts };
};

export default function TripDetailPage() {
  const { id } = useParams();
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const me = getMe();
  const roles = me?.roles ?? [];
  const isDriver = roles.includes("Driver");
  const isManager = roles.includes("Manager");
  const isDispatcher = roles.includes("Dispatcher");
  const isFinance = roles.includes("HeadOfFinance");
  const isSuperAdmin = roles.includes("SuperAdmin");
  const isAdmin = roles.includes("Admin") || isSuperAdmin;

  const [loading, setLoading] = useState(true);
  const [trip, setTrip] = useState<DispatchTripDetail | null>(null);
  const [summary, setSummary] = useState<DispatchTripSummary | null>(null);
  const [documents, setDocuments] = useState<DispatchTripDocument[]>([]);
  const [generatedWaybill, setGeneratedWaybill] = useState<GeneratedWaybill | null>(null);
  const [timeline, setTimeline] = useState<DispatchTripHistory[]>([]);
  const [actionLoading, setActionLoading] = useState(false);
  const [modal, setModal] = useState<ActionModal>(null);
  const [scheduleSaving, setScheduleSaving] = useState(false);
  const [dispatching, setDispatching] = useState(false);
  const [customers, setCustomers] = useState<CustomerOption[]>([]);
  const [drivers, setDrivers] = useState<DriverOption[]>([]);
  const [trucks, setTrucks] = useState<TruckOption[]>([]);
  const [correctOpen, setCorrectOpen] = useState(false);
  const [versionsOpen, setVersionsOpen] = useState(false);
  const [versionsLoading, setVersionsLoading] = useState(false);
  const [versionItems, setVersionItems] = useState<DispatchTripDocumentVersion[]>([]);
  const [versionTotal, setVersionTotal] = useState(0);
  const [versionPage, setVersionPage] = useState(1);
  const [versionPageSize, setVersionPageSize] = useState(10);
  const [versionType, setVersionType] = useState<TripDocumentType>("POD");
  const [versionState, setVersionState] = useState<VersionStateFilter>("ALL");
  const [linkKey, setLinkKey] = useState<string | null>(null);
  const [assignmentConflict, setAssignmentConflict] = useState<AssignmentConflictState | null>(null);
  const [updatedJustNow, setUpdatedJustNow] = useState(false);
  const updatedTimerRef = useRef<number | null>(null);
  const [scheduleForm, setScheduleForm] = useState<ScheduleForm>({
    customerId: "",
    pickupLocation: "",
    pickupLatitude: null,
    pickupLongitude: null,
    pickupScheduledAt: "",
    dropoffLocation: "",
    dropoffLatitude: null,
    dropoffLongitude: null,
    dropoffScheduledAt: "",
    driverUserId: "",
    truckAssetId: "",
    containerNumber: "",
    eirNumber: "",
    bookingNumber: "",
    shippingLine: "",
    notes: "",
    changeRemarks: ""
  });
  const [correctForm, setCorrectForm] = useState<CorrectStatusForm>({
    toStatus: "",
    eventAt: toLocalInput(new Date().toISOString()),
    remarks: ""
  });

  const canVerifyDocs = isManager || isDispatcher || isAdmin;
  const canEditSchedule = isManager || isDispatcher;
  const canViewVersions = isManager || isFinance || isDispatcher;

  const fetchTrip = async () => {
    if (!id) return;
    try {
      setLoading(true);
      const [detail, summaryData, docs, history] = await Promise.all([
        api<DispatchTripDetail>(`/api/dispatch/trips/${id}`, { method: "GET" }),
        api<DispatchTripSummary>(`/api/dispatch/trips/${id}/summary`, { method: "GET" }),
        apiOptional<DispatchTripDocument[]>(`/api/dispatch/trips/${id}/documents`, { method: "GET" }),
        api<DispatchTripHistory[]>(`/api/dispatch/trips/${id}/history`, { method: "GET" })
      ]);
      setTrip(detail);
      setSummary(summaryData);
      setDocuments(docs ?? []);
      setTimeline(history ?? []);
      const waybill = await apiOptional<GeneratedWaybill>(`/api/dispatch/trips/${id}/waybill`, { method: "GET" });
      setGeneratedWaybill(waybill);
      setAssignmentConflict(null);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load trip detail.", "error");
    } finally {
      setLoading(false);
    }
  };

  const isCurrentTripEvent = (eventTripId: string) =>
    Boolean(id && eventTripId.toLowerCase() === id.toLowerCase());

  const markUpdatedJustNow = () => {
    setUpdatedJustNow(true);
    if (updatedTimerRef.current !== null) {
      window.clearTimeout(updatedTimerRef.current);
    }
    updatedTimerRef.current = window.setTimeout(() => {
      setUpdatedJustNow(false);
      updatedTimerRef.current = null;
    }, 3000);
  };

  useDispatchHub({
    onTripStatusChanged: (event) => {
      if (isCurrentTripEvent(event.tripId)) {
        markUpdatedJustNow();
        void fetchTrip();
      }
    },
    onDocumentUploaded: (event) => {
      if (isCurrentTripEvent(event.tripId)) {
        markUpdatedJustNow();
        void fetchTrip();
      }
    },
    onDocumentVerified: (event) => {
      if (isCurrentTripEvent(event.tripId)) {
        markUpdatedJustNow();
        void fetchTrip();
      }
    },
    onDriverLocationUpdated: (event) => {
      if (!isManager && !isDispatcher) return;
      if (isCurrentTripEvent(event.tripId)) {
        markUpdatedJustNow();
        setTrip((current) =>
          current
            ? {
                ...current,
                latestDriverLocation: {
                  latitude: event.latitude,
                  longitude: event.longitude,
                  accuracyMeters: event.accuracyMeters,
                  recordedAt: event.recordedAt
                }
              }
            : current
        );
      }
    }
  });

  useEffect(() => {
    return () => {
      if (updatedTimerRef.current !== null) {
        window.clearTimeout(updatedTimerRef.current);
      }
    };
  }, []);

  const loadReferenceData = async () => {
    try {
      const [customerData, userData, assetData] = await Promise.all([
        api<CustomerOption[]>("/api/dispatch/customers", { method: "GET" }),
        api<{ id: string; username: string; roles: string[] }[]>("/api/users", { method: "GET" }),
        api<{ id: string; assetCode: string; assetType: string; status: string }[]>("/api/assets", { method: "GET" })
      ]);
      setCustomers(customerData ?? []);
      setDrivers((userData ?? []).filter((u) => u.roles?.includes("Driver")));
      setTrucks((assetData ?? []).filter((asset) => asset.assetType === "TRUCK" && asset.status === "ACTIVE"));
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load dispatch references.", "error");
    }
  };

  useEffect(() => {
    fetchTrip();
  }, [id]);

  useEffect(() => {
    loadReferenceData();
  }, []);

  const tripId = summary?.id ?? trip?.id ?? "";
  const currentStatus = summary?.status ?? trip?.status ?? null;
  const currentRowVersion = summary?.rowVersion ?? trip?.rowVersion ?? "";
  const podDoc = documents.find((doc) => doc.type === "POD");
  const podState = podDoc?.state ?? "MISSING";
  const isOnHold = currentStatus === "ON_HOLD";
  const isFailedAttempt = currentStatus === "FAILED_ATTEMPT";
  const pickupDelayed = summary?.latePickup ?? false;
  const dropoffDelayed = summary?.lateDelivery ?? false;

  useEffect(() => {
    if (!versionsOpen) {
      setLinkKey(null);
      return;
    }
    loadVersions();
  }, [versionsOpen, versionPage, versionPageSize, versionType, versionState, tripId]);

  const plannedStops = useMemo(() => {
    if (!trip) return { pickup: null, dropoff: null };
    const pickup = trip.stops.find((stop) => stop.stopType === "PICKUP") ?? null;
    const dropoff = trip.stops.find((stop) => stop.stopType === "DROPOFF") ?? null;
    return { pickup, dropoff };
  }, [trip]);

  const generatedWaybillData = useMemo(() => {
    if (!generatedWaybill?.waybillDataJson) return null;
    try {
      return JSON.parse(generatedWaybill.waybillDataJson) as Record<string, unknown>;
    } catch {
      return null;
    }
  }, [generatedWaybill]);

  const actualTimes = useMemo(() => {
    if (!timeline.length) return { pickup: null, dropoff: null, delivered: null };
    const pickup = timeline.find((entry) => entry.toStatus === "AT_PICKUP")?.eventAt ?? null;
    const dropoff = timeline.find((entry) => entry.toStatus === "AT_DROPOFF")?.eventAt ?? null;
    const delivered = timeline.find((entry) => entry.toStatus === "DELIVERED")?.eventAt ?? null;
    return { pickup, dropoff, delivered };
  }, [timeline]);

  const timelineSorted = useMemo(() => {
    return [...timeline].sort((a, b) => {
      const primary = new Date(a.eventAt).getTime() - new Date(b.eventAt).getTime();
      if (primary !== 0) return primary;
      return new Date(a.recordedAt).getTime() - new Date(b.recordedAt).getTime();
    });
  }, [timeline]);

  useEffect(() => {
    if (!trip || !summary) return;
    setScheduleForm({
      customerId: summary.customer.id,
      pickupLocation: plannedStops.pickup?.locationText ?? "",
      pickupLatitude: plannedStops.pickup?.latitude ?? null,
      pickupLongitude: plannedStops.pickup?.longitude ?? null,
      pickupScheduledAt: toLocalInput(plannedStops.pickup?.scheduledAt),
      dropoffLocation: plannedStops.dropoff?.locationText ?? "",
      dropoffLatitude: plannedStops.dropoff?.latitude ?? null,
      dropoffLongitude: plannedStops.dropoff?.longitude ?? null,
      dropoffScheduledAt: toLocalInput(plannedStops.dropoff?.scheduledAt),
      driverUserId: summary.driverUserId ?? "",
      truckAssetId: summary.truckAssetId ?? "",
      containerNumber: trip.containerNumber ?? "",
      eirNumber: trip.eirNumber ?? "",
      bookingNumber: trip.bookingNumber ?? "",
      shippingLine: trip.shippingLine ?? "",
      notes: trip.notes ?? "",
      changeRemarks: ""
    });
    setCorrectForm({
      toStatus: "",
      eventAt: toLocalInput(new Date().toISOString()),
      remarks: ""
    });
  }, [trip, summary, plannedStops]);

  const resumeFromFailedAttempt = useMemo(() => {
    if (!timeline.length) return null;
    const last = [...timeline]
      .reverse()
      .find((entry) => entry.toStatus === "FAILED_ATTEMPT");
    return last?.fromStatus ?? null;
  }, [timeline]);

  const correctionBaseStatus = useMemo(() => {
    if (!currentStatus) return null;
    if (operationalFlow.includes(currentStatus)) return currentStatus;
    const lastOperational = [...timeline]
      .reverse()
      .find((entry) => operationalFlow.includes(entry.toStatus));
    return lastOperational?.toStatus ?? null;
  }, [currentStatus, timeline]);

  const correctionOptions = useMemo(() => {
    if (!correctionBaseStatus) return [];
    const idx = operationalFlow.indexOf(correctionBaseStatus);
    if (idx < 0) return [];
    return operationalFlow.slice(idx + 1);
  }, [correctionBaseStatus]);

  const buildStopsPayload = () => {
    return [
      {
        stopType: "PICKUP",
        locationText: scheduleForm.pickupLocation,
        latitude: scheduleForm.pickupLatitude ?? null,
        longitude: scheduleForm.pickupLongitude ?? null,
        scheduledAt: new Date(scheduleForm.pickupScheduledAt).toISOString()
      },
      {
        stopType: "DROPOFF",
        locationText: scheduleForm.dropoffLocation,
        latitude: scheduleForm.dropoffLatitude ?? null,
        longitude: scheduleForm.dropoffLongitude ?? null,
        scheduledAt: new Date(scheduleForm.dropoffScheduledAt).toISOString()
      }
    ];
  };

  const validateSchedule = () => {
    if (!scheduleForm.customerId) return "Customer is required.";
    if (!scheduleForm.pickupLocation || !scheduleForm.dropoffLocation) {
      return "Pickup and dropoff locations are required.";
    }
    if (!scheduleForm.pickupScheduledAt || !scheduleForm.dropoffScheduledAt) {
      return "Pickup and dropoff times are required.";
    }
    const assignmentChanged =
      scheduleForm.driverUserId !== (summary?.driverUserId ?? trip?.driverUserId ?? "") ||
      scheduleForm.truckAssetId !== (summary?.truckAssetId ?? trip?.truckAssetId ?? "");
    const loadedOrLater = currentStatus
      ? ["LOADED", "ENROUTE_DROPOFF", "AT_DROPOFF", "DELIVERED"].includes(currentStatus)
      : false;
    if (assignmentChanged && loadedOrLater && isManager && !scheduleForm.changeRemarks.trim()) {
      return "Remarks are required to reassign after loading.";
    }
    return null;
  };

  const handleSaveSchedule = async (silent?: boolean) => {
    if (!tripId) return false;
    if (!currentRowVersion) {
      show("Missing row version. Refresh and try again.", "error");
      return false;
    }
    const error = validateSchedule();
    if (error) {
      show(error, "error");
      return false;
    }
    try {
      setAssignmentConflict(null);
      setScheduleSaving(true);
      await api(`/api/dispatch/trips/${tripId}`, {
        method: "PUT",
        body: JSON.stringify({
          customerId: scheduleForm.customerId,
          driverUserId: scheduleForm.driverUserId || null,
          truckAssetId: scheduleForm.truckAssetId || null,
          notes: scheduleForm.notes || null,
          remarks: scheduleForm.changeRemarks || null,
          containerNumber: scheduleForm.containerNumber.trim() || null,
          eirNumber: scheduleForm.eirNumber.trim() || null,
          bookingNumber: scheduleForm.bookingNumber.trim() || null,
          shippingLine: scheduleForm.shippingLine.trim() || null,
          stops: buildStopsPayload(),
          rowVersion: currentRowVersion
        })
      });
      if (!silent) {
        show("Schedule updated.", "success");
      }
      await fetchTrip();
      return true;
    } catch (e: any) {
      console.error(e);
      const conflict = parseAssignmentConflict(e);
      if (conflict) {
        setAssignmentConflict(conflict);
        show(conflict.summary, "error");
      } else {
        show(e?.message ?? "Failed to update schedule.", "error");
      }
      return false;
    } finally {
      setScheduleSaving(false);
    }
  };

  const handleDispatch = async () => {
    if (!tripId) return;
    if (!currentRowVersion) {
      show("Missing row version. Refresh and try again.", "error");
      return;
    }
    if (!scheduleForm.driverUserId) {
      show("Assign a driver before dispatching.", "error");
      return;
    }
    if (!scheduleForm.containerNumber.trim()) {
      show("Container number is required before dispatching.", "error");
      return;
    }
    if (!scheduleForm.truckAssetId && (!isManager || !scheduleForm.changeRemarks.trim())) {
      show(isManager ? "Assign a truck or add override remarks before dispatching." : "Assign a truck before dispatching.", "error");
      return;
    }
    const saved = await handleSaveSchedule(true);
    if (!saved) return;
    try {
      setAssignmentConflict(null);
      setDispatching(true);
      await api(`/api/dispatch/trips/${tripId}/dispatch`, {
        method: "POST",
        body: JSON.stringify({
          driverUserId: scheduleForm.driverUserId,
          truckAssetId: scheduleForm.truckAssetId || null,
          remarks: scheduleForm.changeRemarks || null,
          rowVersion: currentRowVersion
        })
      });
      show("Trip dispatched.", "success");
      await fetchTrip();
    } catch (e: any) {
      console.error(e);
      const conflict = parseAssignmentConflict(e);
      if (conflict) {
        setAssignmentConflict(conflict);
        show(conflict.summary, "error");
      } else {
        show(e?.message ?? "Failed to dispatch trip.", "error");
      }
    } finally {
      setDispatching(false);
    }
  };

  const handleStatusChange = async (
    toStatus: TripStatus,
    remarks?: string | null,
    podPendingOverride?: boolean | null,
    eventAtOverride?: string | null
  ) => {
    if (!tripId) return;
    if (!currentRowVersion) {
      show("Missing row version. Refresh and try again.", "error");
      return;
    }
    try {
      setActionLoading(true);
      await api(`/api/dispatch/trips/${tripId}/status`, {
        method: "POST",
        body: JSON.stringify({
          toStatus,
          remarks: remarks ?? null,
          podPendingOverride: podPendingOverride ?? null,
          eventAt: eventAtOverride ?? new Date().toISOString(),
          rowVersion: currentRowVersion
        })
      });
      show("Trip updated.", "success");
      await fetchTrip();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to update trip.", "error");
    } finally {
      setActionLoading(false);
    }
  };

  const handleCorrectStatus = async () => {
    if (!tripId) return;
    if (!currentRowVersion) {
      show("Missing row version. Refresh and try again.", "error");
      return;
    }
    if (!correctForm.toStatus) {
      show("Select a status to correct to.", "error");
      return;
    }
    if (!correctForm.remarks.trim()) {
      show("Remarks are required.", "error");
      return;
    }
    const eventAt = fromLocalInput(correctForm.eventAt);
    if (!eventAt) {
      show("Event time is required.", "error");
      return;
    }
    try {
      setActionLoading(true);
      await api(`/api/dispatch/trips/${tripId}/correct-status`, {
        method: "POST",
        body: JSON.stringify({
          toStatus: correctForm.toStatus,
          eventAt,
          remarks: correctForm.remarks.trim(),
          rowVersion: currentRowVersion
        })
      });
      show("Status corrected.", "success");
      setCorrectOpen(false);
      await fetchTrip();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to correct status.", "error");
    } finally {
      setActionLoading(false);
    }
  };

  const handleUploadDoc = async (docType: TripDocumentType, storageKey: string) => {
    if (!tripId) return;
    try {
      setActionLoading(true);
      await api(`/api/dispatch/trips/${tripId}/documents`, {
        method: "POST",
        body: JSON.stringify({ type: docType, storageKey })
      });
      show("Document uploaded.", "success");
      await fetchTrip();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to upload document.", "error");
    } finally {
      setActionLoading(false);
    }
  };

  const handleGenerateWaybill = async () => {
    if (!tripId) return;
    try {
      setActionLoading(true);
      const waybill = await api<GeneratedWaybill>(`/api/dispatch/trips/${tripId}/generate-waybill`, {
        method: "POST"
      });
      setGeneratedWaybill(waybill);
      show("Waybill generated.", "success");
      await fetchTrip();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to generate waybill.", "error");
    } finally {
      setActionLoading(false);
    }
  };

  const canUploadDocument = (docType: TripDocumentType) => {
    if (docType === "WAYBILL") return false;
    if (docType === "ATW") return isDispatcher || isManager || isAdmin;
    if (docType === "POD") return isDispatcher || isDriver;
    return isDriver;
  };

  const documentUploadLabel = (docType: TripDocumentType) => {
    if (docType === "ATW") return "Upload ATW (received from customer)";
    return documents.find((doc) => doc.type === docType) ? "Replace" : "Upload";
  };

  const handleVerifyDoc = async (docId: string) => {
    if (!tripId) return;
    try {
      setActionLoading(true);
      await api(`/api/dispatch/trips/${tripId}/documents/${docId}/verify`, { method: "POST" });
      show("Document verified.", "success");
      await fetchTrip();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to verify document.", "error");
    } finally {
      setActionLoading(false);
    }
  };

  const handleRejectDoc = async (docId: string, remarks: string) => {
    if (!tripId) return;
    try {
      setActionLoading(true);
      await api(`/api/dispatch/trips/${tripId}/documents/${docId}/reject`, {
        method: "POST",
        body: JSON.stringify({ remarks })
      });
      show("Document rejected.", "success");
      await fetchTrip();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to reject document.", "error");
    } finally {
      setActionLoading(false);
    }
  };

  const loadVersions = async () => {
    if (!tripId) return;
    try {
      setVersionsLoading(true);
      const params = new URLSearchParams();
      params.set("page", String(versionPage));
      params.set("pageSize", String(versionPageSize));
      params.set("type", versionType);
      if (versionState !== "ALL") {
        params.set("state", versionState);
      }
      const result = await api<PagedResult<DispatchTripDocumentVersion>>(
        `/api/dispatch/trips/${tripId}/documents/versions?${params.toString()}`,
        { method: "GET" }
      );
      setVersionItems(result.items);
      setVersionTotal(result.totalCount);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load document versions.", "error");
    } finally {
      setVersionsLoading(false);
    }
  };

  const handleOpenDocument = async (docId: string) => {
    if (!tripId) return;
    try {
      const result = await api<DispatchTripDocumentLink>(
        `/api/dispatch/trips/${tripId}/documents/${docId}/link`,
        { method: "GET" }
      );
      const key = result.storageKey;
      if (/^https?:\/\//i.test(key)) {
        window.open(key, "_blank", "noopener,noreferrer");
        return;
      }
      setLinkKey(key);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to open document.", "error");
    }
  };

  const openVersionsDrawer = (type?: TripDocumentType) => {
    setVersionType(type ?? "POD");
    setVersionState("ALL");
    setVersionPage(1);
    setVersionsOpen(true);
  };

  if (loading) {
    return (
      <div className="space-y-6">
        <PageHeader title="Trip Detail" description="Loading trip detail..." />
        <LoadingSkeleton rows={8} />
      </div>
    );
  }

  if (!trip) {
    return <EmptyState title="Trip not found" description="The trip detail could not be loaded." />;
  }

  const isDraft = currentStatus === "DRAFT";
  const canEditCustomer = isManager || isDraft;
  const canEditLocations = isManager || isDraft;
  const dispatcherReassignLocked =
    isDispatcher &&
    !isManager &&
    !isDraft &&
    !["DISPATCHED", "ENROUTE_PICKUP"].includes(currentStatus ?? "");
  const canCorrectStatus =
    (isManager || isDispatcher) &&
    currentStatus !== "CLOSED" &&
    currentStatus !== "CANCELLED" &&
    currentStatus !== "DRAFT";
  const canViewDispatchMap = isManager || isDispatcher;
  const latestLocation = trip.latestDriverLocation;
  const versionTotalPages = Math.max(1, Math.ceil(versionTotal / versionPageSize));

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title={`Trip ${tripId ? tripId.slice(0, 8) : "-"}`}
        description={`Customer: ${summary?.customer?.name ?? trip.customer?.name ?? "-"}`}
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/dispatch/board" className="text-muted-foreground hover:text-foreground">
              Dispatch
            </Link>
            <span className="text-muted-foreground">/</span>
            <Link to="/dispatch/trips" className="text-muted-foreground hover:text-foreground">
              Trips
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">Trip {tripId ? tripId.slice(0, 8) : "-"}</span>
          </nav>
        }
        actions={
          <div className="flex items-center gap-2">
            {updatedJustNow ? (
              <span className="rounded-full border border-emerald-200 bg-emerald-50 px-2 py-1 text-xs font-medium text-emerald-700">
                Updated just now
              </span>
            ) : null}
            <Button variant="outline" onClick={() => nav(-1)}>
              Back
            </Button>
          </div>
        }
      />

      <div className="sticky top-0 z-20 -mx-6 border-b border-border/60 bg-background/95 px-6 py-4 backdrop-blur">
        <div className="surface-card p-5">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="space-y-3">
              <div className="flex flex-wrap items-center gap-3">
                <span className="text-xs uppercase tracking-[0.2em] text-muted-foreground">
                  Trip {tripId ? tripId.slice(0, 8) : "-"}
                </span>
                <StatusBadge status={currentStatus ? statusLabels[currentStatus] ?? currentStatus : "Unknown"} />
                <span
                  className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
                    docStateClasses[podState]
                  }`}
                >
                  POD {podState}
                </span>
                {isOnHold ? (
                  <span className="rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-[10px] font-semibold uppercase text-amber-700">
                    On Hold
                  </span>
                ) : null}
                {isFailedAttempt ? (
                  <span className="rounded-full border border-rose-200 bg-rose-50 px-2 py-0.5 text-[10px] font-semibold uppercase text-rose-700">
                    Failed Attempt
                  </span>
                ) : null}
                {summary?.podPending ?? trip.podPending ? (
                  <span className="rounded-full border border-orange-200 bg-orange-50 px-2 py-0.5 text-[10px] font-semibold uppercase text-orange-700">
                    POD Pending
                  </span>
                ) : null}
              </div>
              <div className="grid gap-3 text-sm text-muted-foreground md:grid-cols-2 xl:grid-cols-4">
                <div>
                  <p className="text-xs uppercase">Customer</p>
                  <p className="mt-1 text-foreground">{summary?.customer?.name ?? trip.customer?.name ?? "-"}</p>
                </div>
                <div>
                  <p className="text-xs uppercase">Driver</p>
                  <p className="mt-1 text-foreground">{summary?.driverUsername ?? trip.driverUsername ?? "-"}</p>
                </div>
                <div>
                  <p className="text-xs uppercase">Truck</p>
                  <p className="mt-1 text-foreground">{summary?.truckAssetCode ?? trip.truckAssetCode ?? "-"}</p>
                </div>
                <div>
                  <p className="text-xs uppercase">Pickup</p>
                  <p className="mt-1 text-foreground">
                    {plannedStops.pickup?.scheduledAt
                      ? new Date(plannedStops.pickup.scheduledAt).toLocaleString()
                      : "Unscheduled"}
                  </p>
                </div>
                <div>
                  <p className="text-xs uppercase">Dropoff</p>
                  <p className="mt-1 text-foreground">
                    {plannedStops.dropoff?.scheduledAt
                      ? new Date(plannedStops.dropoff.scheduledAt).toLocaleString()
                      : "Unscheduled"}
                  </p>
                </div>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              {canEditSchedule && isDraft ? (
                <Button onClick={handleDispatch} disabled={scheduleSaving || dispatching}>
                  {dispatching ? "Dispatching..." : "Dispatch"}
                </Button>
              ) : null}
              {(isManager || isDispatcher) && currentStatus && !["CLOSED", "CANCELLED", "ON_HOLD"].includes(currentStatus) ? (
                <Button
                  variant="outline"
                  onClick={() =>
                    setModal({
                      type: "HOLD",
                      remarks: "",
                      eventAt: toLocalInput(new Date().toISOString())
                    })
                  }
                >
                  Place On Hold
                </Button>
              ) : null}
              {(isManager || isDispatcher) &&
              failedAttemptEligible.includes(currentStatus ?? "DRAFT") ? (
                <Button
                  variant="outline"
                  onClick={() =>
                    setModal({
                      type: "FAILED",
                      remarks: "",
                      eventAt: toLocalInput(new Date().toISOString())
                    })
                  }
                >
                  Report Failed Attempt
                </Button>
              ) : null}
              {(isManager || isDispatcher) && currentStatus === "ON_HOLD" && trip.holdPreviousStatus ? (
                <Button variant="outline" onClick={() => handleStatusChange(trip.holdPreviousStatus!)}>
                  Resume
                </Button>
              ) : null}
              {(isManager || isDispatcher) && currentStatus === "FAILED_ATTEMPT" && resumeFromFailedAttempt ? (
                <Button
                  variant="outline"
                  onClick={() =>
                    setModal({
                      type: "RESOLVE_FAILED",
                      remarks: "",
                      eventAt: toLocalInput(new Date().toISOString())
                    })
                  }
                >
                  Resolve Failed Attempt
                </Button>
              ) : null}
              {isManager && currentStatus === "DELIVERED" ? (
                <Button onClick={() => setModal({ type: "CLOSE" })}>Close Trip</Button>
              ) : null}
              {isManager && currentStatus && !["CLOSED", "CANCELLED"].includes(currentStatus) ? (
                <Button
                  variant="destructive"
                  onClick={() =>
                    setModal({
                      type: "CANCEL",
                      remarks: "",
                      eventAt: toLocalInput(new Date().toISOString())
                    })
                  }
                >
                  Cancel Trip
                </Button>
              ) : null}
              {canCorrectStatus ? (
                <Button
                  variant="outline"
                  onClick={() => {
                    setCorrectForm({
                      toStatus: "",
                      eventAt: toLocalInput(new Date().toISOString()),
                      remarks: ""
                    });
                    setCorrectOpen(true);
                  }}
                >
                  Correct Status
                </Button>
              ) : null}
            </div>
          </div>
        </div>
      </div>

      {canViewDispatchMap ? (
        <div className="surface-card p-4 md:p-6">
          <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-primary">Trip Map</p>
              <h3 className="mt-2 text-base font-semibold text-foreground">Pickup, Dropoff, and Latest Driver Ping</h3>
            </div>
            <span className="text-xs text-muted-foreground">
              {latestLocation?.recordedAt
                ? `Last ping ${new Date(latestLocation.recordedAt).toLocaleString()}`
                : "No driver ping yet"}
            </span>
          </div>
          <div className="mt-4">
            <TripMap
              pickup={{
                latitude: plannedStops.pickup?.latitude,
                longitude: plannedStops.pickup?.longitude,
                label: plannedStops.pickup?.locationText ?? "Pickup",
                detail: plannedStops.pickup?.scheduledAt
                  ? new Date(plannedStops.pickup.scheduledAt).toLocaleString()
                  : null
              }}
              dropoff={{
                latitude: plannedStops.dropoff?.latitude,
                longitude: plannedStops.dropoff?.longitude,
                label: plannedStops.dropoff?.locationText ?? "Dropoff",
                detail: plannedStops.dropoff?.scheduledAt
                  ? new Date(plannedStops.dropoff.scheduledAt).toLocaleString()
                  : null
              }}
              driver={
                latestLocation
                  ? {
                      latitude: latestLocation.latitude,
                      longitude: latestLocation.longitude,
                      label: trip.driverUsername ?? "Driver location",
                      detail: trip.truckAssetCode ? `Truck ${trip.truckAssetCode}` : null
                    }
                  : null
              }
              driverRecordedAt={latestLocation?.recordedAt}
              driverAccuracyMeters={latestLocation?.accuracyMeters}
            />
          </div>
        </div>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-[1.2fr_1fr]">
        <div className="surface-card p-6" id="trip-timeline">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-semibold">Trip Timeline</h3>
            <span className="text-xs text-muted-foreground">Chronological history</span>
          </div>
          <div className="mt-4 max-h-[520px] space-y-3 overflow-y-auto pr-2 text-sm">
            {timelineSorted.length === 0 ? (
              <EmptyState title="No history" description="Trip history will appear here." />
            ) : (
              timelineSorted.map((entry) => (
                <div key={entry.id} className="rounded-lg border border-border/50 bg-muted/20 px-4 py-3">
                  <div className="flex items-start justify-between text-xs text-muted-foreground">
                    <div className="space-y-1">
                      <span className="block">{new Date(entry.eventAt).toLocaleString()}</span>
                      <span className="block text-[10px]">
                        Recorded {new Date(entry.recordedAt).toLocaleString()}
                      </span>
                    </div>
                    <span>{entry.actorUsername ?? entry.actorUserId}</span>
                  </div>
                  <div className="mt-2 flex items-center gap-2">
                    <p className="text-sm text-foreground">
                      {entry.eventType === "SCHEDULE_UPDATED"
                        ? "Schedule updated"
                        : entry.eventType === "STATUS_CORRECTED"
                        ? `Status corrected: ${statusLabels[entry.fromStatus]} -> ${statusLabels[entry.toStatus]}`
                        : `${statusLabels[entry.fromStatus]} -> ${statusLabels[entry.toStatus]}`}
                    </p>
                    {entry.eventType === "STATUS_CORRECTED" ? (
                      <span className="rounded-full border border-indigo-200 bg-indigo-50 px-2 py-0.5 text-[10px] font-semibold uppercase text-indigo-700">
                        Corrected
                      </span>
                    ) : null}
                  </div>
                  {entry.remarks ? (
                    <p className="mt-1 text-xs text-muted-foreground">{entry.remarks}</p>
                  ) : null}
                </div>
              ))
            )}
          </div>
        </div>

        <div className="space-y-6">
          <div className="surface-card p-6">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold">Stops & Schedule</h3>
              <span className="text-xs text-muted-foreground">{trip.stops.length} stop(s)</span>
            </div>
            {assignmentConflict ? (
              <div className="mt-4 rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm">
                <p className="font-semibold text-rose-700">{assignmentConflict.summary}</p>
                {assignmentConflict.conflicts.length > 0 ? (
                  <ul className="mt-2 list-disc space-y-1 pl-5 text-rose-700">
                    {assignmentConflict.conflicts.map((item, index) => (
                      <li key={`${item.tripId ?? item.tripReference ?? "conflict"}-${index}`}>{buildConflictLine(item)}</li>
                    ))}
                  </ul>
                ) : null}
              </div>
            ) : null}
            <div className="mt-4 grid gap-3 text-sm">
              <div
                className={`rounded-lg border px-4 py-3 ${
                  pickupDelayed ? "border-amber-200 bg-amber-50" : "border-border/50 bg-muted/20"
                }`}
              >
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-foreground">Pickup</span>
                  <span className="text-xs text-muted-foreground">
                    {plannedStops.pickup?.scheduledAt
                      ? new Date(plannedStops.pickup.scheduledAt).toLocaleString()
                      : "Unscheduled"}
                  </span>
                </div>
                <p className="mt-2 text-muted-foreground">{plannedStops.pickup?.locationText ?? "-"}</p>
                <p className="mt-2 text-xs text-muted-foreground">
                  Actual arrival: {actualTimes.pickup ? new Date(actualTimes.pickup).toLocaleString() : "--"}
                </p>
              </div>
              <div
                className={`rounded-lg border px-4 py-3 ${
                  dropoffDelayed ? "border-amber-200 bg-amber-50" : "border-border/50 bg-muted/20"
                }`}
              >
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-foreground">Dropoff</span>
                  <span className="text-xs text-muted-foreground">
                    {plannedStops.dropoff?.scheduledAt
                      ? new Date(plannedStops.dropoff.scheduledAt).toLocaleString()
                      : "Unscheduled"}
                  </span>
                </div>
                <p className="mt-2 text-muted-foreground">{plannedStops.dropoff?.locationText ?? "-"}</p>
                <p className="mt-2 text-xs text-muted-foreground">
                  Actual arrival: {actualTimes.dropoff ? new Date(actualTimes.dropoff).toLocaleString() : "--"}
                </p>
              </div>
            </div>

            {canEditSchedule ? (
              <div className="mt-5 grid gap-4 text-sm md:grid-cols-2">
                <div>
                  <label className="text-xs uppercase text-muted-foreground">Pickup Location</label>
                  <input
                    value={scheduleForm.pickupLocation}
                    onChange={(e) => setScheduleForm((prev) => ({ ...prev, pickupLocation: e.target.value }))}
                    className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
                    disabled={!canEditLocations}
                  />
                </div>
                <div>
                  <label className="text-xs uppercase text-muted-foreground">Pickup Time</label>
                  <input
                    type="datetime-local"
                    value={scheduleForm.pickupScheduledAt}
                    onChange={(e) => setScheduleForm((prev) => ({ ...prev, pickupScheduledAt: e.target.value }))}
                    className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
                  />
                </div>
                <div>
                  <label className="text-xs uppercase text-muted-foreground">Dropoff Location</label>
                  <input
                    value={scheduleForm.dropoffLocation}
                    onChange={(e) => setScheduleForm((prev) => ({ ...prev, dropoffLocation: e.target.value }))}
                    className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
                    disabled={!canEditLocations}
                  />
                </div>
                <div>
                  <label className="text-xs uppercase text-muted-foreground">Dropoff Time</label>
                  <input
                    type="datetime-local"
                    value={scheduleForm.dropoffScheduledAt}
                    onChange={(e) => setScheduleForm((prev) => ({ ...prev, dropoffScheduledAt: e.target.value }))}
                    className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
                  />
                </div>
                {canEditLocations ? (
                  <div className="md:col-span-2">
                    <label className="text-xs uppercase text-muted-foreground">Map Pins</label>
                    <div className="mt-2">
                      <LocationPinPicker
                        value={{
                          pickupLatitude: scheduleForm.pickupLatitude,
                          pickupLongitude: scheduleForm.pickupLongitude,
                          dropoffLatitude: scheduleForm.dropoffLatitude,
                          dropoffLongitude: scheduleForm.dropoffLongitude
                        }}
                        onChange={(pins) => setScheduleForm((prev) => ({ ...prev, ...pins }))}
                      />
                    </div>
                  </div>
                ) : null}
              </div>
            ) : null}
          </div>

          <div className="surface-card p-6">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold">Documents</h3>
              {canViewVersions ? (
                <Button variant="outline" size="sm" onClick={() => openVersionsDrawer()}>
                  View Versions
                </Button>
              ) : null}
            </div>
            <div className="mt-4 overflow-x-auto">
              <DataTable>
                <thead className="bg-muted/30 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
                  <tr>
                    <th className="px-4 py-3 text-left">Type</th>
                    <th className="px-4 py-3 text-left">Version</th>
                    <th className="px-4 py-3 text-left">Uploaded By</th>
                    <th className="px-4 py-3 text-left">Uploaded At</th>
                    <th className="px-4 py-3 text-left">State</th>
                    <th className="px-4 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/50">
                  {docTypes.map((type) => {
                    const doc = documents.find((d) => d.type === type);
                    const state = type === "WAYBILL" ? (generatedWaybill ? "VERIFIED" : "MISSING") : doc?.state ?? "MISSING";
                    return (
                      <tr key={type} className="text-sm">
                        <td className="px-4 py-3 font-medium text-foreground">{type}</td>
                        <td className="px-4 py-3 text-xs text-muted-foreground">
                          {type === "WAYBILL" && generatedWaybill ? `v${generatedWaybill.version}` : doc ? "Active" : "-"}
                        </td>
                        <td className="px-4 py-3 text-xs text-muted-foreground">
                          {type === "WAYBILL" && generatedWaybill
                            ? generatedWaybill.generatedByUserId
                            : doc?.uploadedByUsername ?? doc?.uploadedByUserId ?? "-"}
                        </td>
                        <td className="px-4 py-3 text-xs text-muted-foreground">
                          {type === "WAYBILL" && generatedWaybill
                            ? new Date(generatedWaybill.generatedAt).toLocaleString()
                            : doc?.uploadedAt ? new Date(doc.uploadedAt).toLocaleString() : "-"}
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
                              docStateClasses[state]
                            }`}
                          >
                            {state}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right">
                          <div className="flex items-center justify-end gap-2">
                            {type === "WAYBILL" ? (
                              <>
                                {isDispatcher || isManager || isAdmin ? (
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    disabled={actionLoading}
                                    onClick={handleGenerateWaybill}
                                  >
                                    Generate Waybill
                                  </Button>
                                ) : null}
                                {generatedWaybill ? (
                                  <Button variant="outline" size="sm" onClick={() => window.print()}>
                                    Print
                                  </Button>
                                ) : null}
                              </>
                            ) : canUploadDocument(type) ? (
                              <Button
                                variant="outline"
                                size="sm"
                                disabled={actionLoading}
                                onClick={() =>
                                  setModal({ type: "DOC_UPLOAD", docType: type, storageKey: "" })
                                }
                              >
                                {documentUploadLabel(type)}
                              </Button>
                            ) : null}
                            {canVerifyDocs && doc && doc.state === "UPLOADED" ? (
                              <>
                                <Button
                                  size="sm"
                                  variant="outline"
                                  className="gap-1"
                                  disabled={actionLoading}
                                  onClick={() => handleVerifyDoc(doc.id)}
                                >
                                  <Check className="h-4 w-4" />
                                  Verify
                                </Button>
                                <Button
                                  size="sm"
                                  variant="outline"
                                  className="gap-1"
                                  disabled={actionLoading}
                                  onClick={() =>
                                    setModal({ type: "DOC_REJECT", docId: doc.id, remarks: "" })
                                  }
                                >
                                  <XCircle className="h-4 w-4" />
                                  Reject
                                </Button>
                              </>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </DataTable>
            </div>
            {generatedWaybill ? (
              <div className="mt-5 rounded-lg border border-border bg-background p-5 print:border-0 print:shadow-none">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">NVG Dispatch</p>
                    <h4 className="mt-1 text-lg font-semibold text-foreground">Waybill</h4>
                  </div>
                  <Button variant="outline" size="sm" onClick={() => window.print()}>
                    Print
                  </Button>
                </div>
                <div className="mt-4 grid gap-2 text-sm md:grid-cols-2">
                  <WaybillField label="Waybill No" value={generatedWaybill.waybillNumber} />
                  <WaybillField label="Date" value={new Date(generatedWaybill.generatedAt).toLocaleString()} />
                  <WaybillField label="Container No" value={readWaybillValue(generatedWaybillData, "ContainerNumber")} />
                  <WaybillField label="EIR No" value={readWaybillValue(generatedWaybillData, "EirNumber")} />
                  <WaybillField label="Booking No" value={readWaybillValue(generatedWaybillData, "BookingNumber")} />
                  <WaybillField label="Shipping Line" value={readWaybillValue(generatedWaybillData, "ShippingLine")} />
                  <WaybillField label="From" value={readWaybillValue(generatedWaybillData, "PickupLocation")} />
                  <WaybillField label="To" value={readWaybillValue(generatedWaybillData, "DropoffLocation")} />
                  <WaybillField label="Driver" value={readWaybillValue(generatedWaybillData, "DriverName")} />
                  <WaybillField label="Truck" value={readWaybillValue(generatedWaybillData, "TruckPlate")} />
                  <WaybillField label="Customer" value={readWaybillValue(generatedWaybillData, "CustomerName")} />
                  <WaybillField label="Pickup Time" value={formatWaybillDate(readWaybillValue(generatedWaybillData, "ActualPickupTime"))} />
                  <WaybillField label="Delivery Time" value={formatWaybillDate(readWaybillValue(generatedWaybillData, "ActualDropoffTime"))} />
                </div>
              </div>
            ) : null}
          </div>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1.2fr_1fr]">
        <div className="surface-card p-6">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-sm font-semibold">Assignment</h3>
              <p className="text-xs text-muted-foreground">
                Reassign drivers or trucks before delivery. Conflict checks apply on save.
              </p>
            </div>
            <span className="text-xs text-muted-foreground">{isDraft ? "Draft" : "Active"}</span>
          </div>

          <div className="mt-4 grid gap-4 text-sm md:grid-cols-2">
            <div className="md:col-span-2">
              <label className="text-xs uppercase text-muted-foreground">Customer</label>
              <select
                value={scheduleForm.customerId}
                onChange={(e) => setScheduleForm((prev) => ({ ...prev, customerId: e.target.value }))}
                className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
                disabled={!canEditCustomer}
              >
                <option value="">Select customer</option>
                {customers.map((customer) => (
                  <option key={customer.id} value={customer.id}>
                    {customer.name}
                  </option>
                ))}
              </select>
              {!canEditCustomer ? (
                <p className="mt-1 text-xs text-muted-foreground">Customer is locked after dispatch.</p>
              ) : null}
            </div>
            <div>
              <label className="text-xs uppercase text-muted-foreground">Driver</label>
              <select
                value={scheduleForm.driverUserId}
                onChange={(e) => setScheduleForm((prev) => ({ ...prev, driverUserId: e.target.value }))}
                className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
                disabled={dispatcherReassignLocked}
              >
                <option value="">Unassigned</option>
                {drivers.map((driver) => (
                  <option key={driver.id} value={driver.id}>
                    {driver.username}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-xs uppercase text-muted-foreground">Truck</label>
              <select
                value={scheduleForm.truckAssetId}
                onChange={(e) => setScheduleForm((prev) => ({ ...prev, truckAssetId: e.target.value }))}
                className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
                disabled={dispatcherReassignLocked}
              >
                <option value="">Unassigned</option>
                {trucks.map((truck) => (
                  <option key={truck.id} value={truck.id}>
                    {truck.assetCode}
                  </option>
                ))}
              </select>
            </div>
            <div className="md:col-span-2">
              <label className="text-xs uppercase text-muted-foreground">Change Remarks</label>
              <input
                value={scheduleForm.changeRemarks}
                onChange={(e) => setScheduleForm((prev) => ({ ...prev, changeRemarks: e.target.value }))}
                className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
                placeholder="Required for reassignment after loading."
              />
            </div>
          </div>

          {canEditSchedule ? (
            <div className="mt-5 flex flex-wrap items-center gap-2">
              <Button
                variant="outline"
                onClick={() => handleSaveSchedule()}
                disabled={scheduleSaving || dispatching}
              >
                {scheduleSaving ? "Saving..." : "Save Assignment"}
              </Button>
              {isDraft ? (
                <Button onClick={handleDispatch} disabled={scheduleSaving || dispatching}>
                  {dispatching ? "Dispatching..." : "Dispatch Trip"}
                </Button>
              ) : null}
            </div>
          ) : null}
        </div>

        <div className="surface-card p-6">
          <h3 className="text-sm font-semibold">Trip Metadata</h3>
          <div className="mt-4 grid gap-3 text-sm text-muted-foreground">
            <div className="grid gap-3 md:grid-cols-2">
              <div>
                <label className="text-xs uppercase text-muted-foreground">Container Number</label>
                <input
                  value={scheduleForm.containerNumber}
                  onChange={(e) => setScheduleForm((prev) => ({ ...prev, containerNumber: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm text-foreground"
                  disabled={!canEditSchedule}
                />
              </div>
              <div>
                <label className="text-xs uppercase text-muted-foreground">EIR Number</label>
                <input
                  value={scheduleForm.eirNumber}
                  onChange={(e) => setScheduleForm((prev) => ({ ...prev, eirNumber: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm text-foreground"
                  disabled={!canEditSchedule}
                />
              </div>
              <div>
                <label className="text-xs uppercase text-muted-foreground">Booking Number</label>
                <input
                  value={scheduleForm.bookingNumber}
                  onChange={(e) => setScheduleForm((prev) => ({ ...prev, bookingNumber: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm text-foreground"
                  disabled={!canEditSchedule}
                />
              </div>
              <div>
                <label className="text-xs uppercase text-muted-foreground">Shipping Line</label>
                <input
                  value={scheduleForm.shippingLine}
                  onChange={(e) => setScheduleForm((prev) => ({ ...prev, shippingLine: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm text-foreground"
                  disabled={!canEditSchedule}
                />
              </div>
            </div>
            <div>
              <p className="text-xs uppercase">Created At</p>
              <p className="mt-1 text-foreground">{new Date(trip.createdAt).toLocaleString()}</p>
            </div>
            <div>
              <p className="text-xs uppercase">Created By</p>
              <p className="mt-1 text-foreground">
                {summary?.createdByUsername ?? summary?.createdByUserId ?? "—"}
              </p>
            </div>
            <div>
              <p className="text-xs uppercase">Last Updated</p>
              <p className="mt-1 text-foreground">
                {new Date(summary?.updatedAt ?? trip.updatedAt ?? trip.createdAt).toLocaleString()}
              </p>
            </div>
            <div>
              <p className="text-xs uppercase">POD Pending</p>
              <p className="mt-1 text-foreground">{summary?.podPending ?? trip.podPending ? "Yes" : "No"}</p>
            </div>
            <div className="md:col-span-2">
              <label className="text-xs uppercase text-muted-foreground">Internal Remarks</label>
              <textarea
                value={scheduleForm.notes}
                onChange={(e) => setScheduleForm((prev) => ({ ...prev, notes: e.target.value }))}
                className="mt-2 min-h-[90px] w-full rounded-lg border border-border bg-background px-3 py-2 text-sm"
                placeholder="Operational notes"
              />
            </div>
          </div>
        </div>
      </div>
{versionsOpen ? (
        <div className="fixed inset-0 z-50">
          <div
            className="absolute inset-0 bg-black/40 backdrop-blur-[2px]"
            role="presentation"
          />
          <div
            role="dialog"
            aria-modal="true"
            className="absolute right-0 top-0 h-full w-[min(92vw,560px)] border-l border-slate-200 bg-white p-6 shadow-2xl"
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Document History</p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">Versions</h2>
              </div>
              <button
                onClick={() => setVersionsOpen(false)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>

            <div className="mt-5 grid gap-4 text-sm">
              <div className="grid gap-3 md:grid-cols-2">
                <div>
                  <label className="text-xs uppercase text-slate-500">Type</label>
                  <select
                    value={versionType}
                    onChange={(e) => {
                      setVersionType(e.target.value as TripDocumentType);
                      setVersionPage(1);
                    }}
                    className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                  >
                    {docTypes.map((type) => (
                      <option key={type} value={type}>
                        {type}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="text-xs uppercase text-slate-500">State</label>
                  <select
                    value={versionState}
                    onChange={(e) => {
                      setVersionState(e.target.value as VersionStateFilter);
                      setVersionPage(1);
                    }}
                    className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                  >
                    <option value="ALL">All</option>
                    <option value="UPLOADED">Uploaded</option>
                    <option value="VERIFIED">Verified</option>
                    <option value="REJECTED">Rejected</option>
                  </select>
                </div>
              </div>

              <div className="flex items-center justify-between text-xs text-slate-500">
                <span>
                  {versionTotal} version{versionTotal === 1 ? "" : "s"}
                </span>
                <div className="flex items-center gap-2">
                  <label className="text-[10px] uppercase text-slate-400">Page Size</label>
                  <select
                    value={versionPageSize}
                    onChange={(e) => {
                      setVersionPageSize(Number(e.target.value));
                      setVersionPage(1);
                    }}
                    className="h-8 rounded-md border border-slate-200 bg-white px-2 text-xs"
                  >
                    {[10, 20, 50].map((size) => (
                      <option key={size} value={size}>
                        {size}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              {linkKey ? (
                <div className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs">
                  <p className="text-[10px] uppercase text-slate-400">Storage Key</p>
                  <div className="mt-2 flex items-center gap-2">
                    <input
                      readOnly
                      value={linkKey}
                      className="h-8 flex-1 rounded-md border border-slate-200 bg-white px-2 text-xs"
                    />
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => {
                        navigator.clipboard?.writeText(linkKey);
                        show("Storage key copied.", "success");
                      }}
                    >
                      Copy
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => setLinkKey(null)}>
                      Hide
                    </Button>
                  </div>
                </div>
              ) : null}

              <div className="space-y-3 overflow-y-auto pr-1">
                {versionsLoading ? (
                  <LoadingSkeleton rows={4} />
                ) : versionItems.length === 0 ? (
                  <EmptyState title="No versions" description="No document versions match the filters." />
                ) : (
                  versionItems.map((item) => (
                    <div key={item.id} className="rounded-lg border border-slate-200 bg-white px-4 py-3">
                      <div className="flex items-start justify-between text-xs text-slate-500">
                        <div>
                          <span className="block">{new Date(item.uploadedAt).toLocaleString()}</span>
                          <span className="block text-[10px]">
                            {item.uploadedByUsername ?? item.uploadedByUserId}
                          </span>
                        </div>
                        <div className="flex flex-col items-end gap-1">
                          <span className="rounded-full border border-slate-200 px-2 py-0.5 text-[10px] uppercase">
                            {item.state}
                          </span>
                          {item.isActive ? (
                            <span className="rounded-full border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-[10px] text-emerald-700">
                              Active
                            </span>
                          ) : null}
                        </div>
                      </div>
                      {item.remarks ? (
                        <p className="mt-2 text-xs text-slate-500">Remarks: {item.remarks}</p>
                      ) : null}
                      <div className="mt-3 flex justify-end">
                        <Button
                          size="sm"
                          variant="outline"
                          className="gap-1"
                          onClick={() => handleOpenDocument(item.id)}
                        >
                          <ExternalLink className="h-4 w-4" />
                          Open
                        </Button>
                      </div>
                    </div>
                  ))
                )}
              </div>

              <div className="flex items-center justify-between text-xs text-slate-500">
                <span>
                  Page {versionPage} of {versionTotalPages}
                </span>
                <div className="flex items-center gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={versionPage <= 1 || versionsLoading}
                    onClick={() => setVersionPage((prev) => Math.max(1, prev - 1))}
                  >
                    Previous
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={versionPage >= versionTotalPages || versionsLoading}
                    onClick={() => setVersionPage((prev) => Math.min(versionTotalPages, prev + 1))}
                  >
                    Next
                  </Button>
                </div>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {correctOpen ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,560px)] rounded-2xl border border-slate-200 bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Status Correction</p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">Correct Status</h2>
              </div>
              <button
                onClick={() => setCorrectOpen(false)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>

            <div className="mt-5 space-y-4 text-sm">
              <div>
                <label className="text-xs uppercase text-slate-500">To Status</label>
                <select
                  value={correctForm.toStatus}
                  onChange={(e) =>
                    setCorrectForm((prev) => ({ ...prev, toStatus: e.target.value as TripStatus }))
                  }
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                >
                  <option value="">Select status</option>
                  {correctionOptions.map((status) => (
                    <option key={status} value={status}>
                      {statusLabels[status]}
                    </option>
                  ))}
                </select>
                {correctionOptions.length === 0 ? (
                  <p className="mt-2 text-xs text-slate-500">No forward statuses available.</p>
                ) : null}
              </div>

              <div>
                <label className="text-xs uppercase text-slate-500">Event Time</label>
                <input
                  type="datetime-local"
                  value={correctForm.eventAt}
                  onChange={(e) =>
                    setCorrectForm((prev) => ({ ...prev, eventAt: e.target.value }))
                  }
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
              </div>

              <div>
                <label className="text-xs uppercase text-slate-500">Remarks</label>
                <textarea
                  value={correctForm.remarks}
                  onChange={(e) =>
                    setCorrectForm((prev) => ({ ...prev, remarks: e.target.value }))
                  }
                  className="mt-2 min-h-[100px] w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm"
                  placeholder="Explain the correction"
                />
              </div>
            </div>

            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setCorrectOpen(false)}>
                Cancel
              </Button>
              <Button
                onClick={handleCorrectStatus}
                disabled={actionLoading || correctionOptions.length === 0}
              >
                Submit
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {modal ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,520px)] rounded-2xl border border-slate-200 bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Action Required</p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">
                  {modal.type === "DOC_UPLOAD"
                    ? `Upload ${modal.docType}`
                    : modal.type === "DOC_REJECT"
                    ? "Reject Document"
                    : modal.type === "HOLD"
                    ? "Place Trip On Hold"
                    : modal.type === "FAILED"
                    ? "Report Failed Attempt"
                    : modal.type === "RESOLVE_FAILED"
                    ? "Resolve Failed Attempt"
                    : modal.type === "CANCEL"
                    ? "Cancel Trip"
                    : "Close Trip"}
                </h2>
              </div>
              <button
                onClick={() => setModal(null)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>

            {modal.type === "DOC_UPLOAD" ? (
              <div className="mt-5 space-y-2 text-sm">
                <label className="text-xs uppercase text-slate-500">Storage Key / URL</label>
                <input
                  value={modal.storageKey}
                  onChange={(e) => setModal({ ...modal, storageKey: e.target.value })}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                  placeholder="docs/waybill.pdf"
                />
              </div>
            ) : modal.type === "CLOSE" ? (
              <div className="mt-5 text-sm text-slate-600">
                Closing a trip is irreversible. Make sure POD rules are satisfied before proceeding.
              </div>
            ) : (
              <div className="mt-5 space-y-4 text-sm">
                {"eventAt" in modal ? (
                  <div>
                    <label className="text-xs uppercase text-slate-500">Event Time</label>
                    <input
                      type="datetime-local"
                      value={modal.eventAt}
                      onChange={(e) => setModal({ ...modal, eventAt: e.target.value })}
                      className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                    />
                  </div>
                ) : null}
                <div>
                  <label className="text-xs uppercase text-slate-500">Remarks</label>
                  <textarea
                    value={modal.remarks}
                    onChange={(e) => setModal({ ...modal, remarks: e.target.value })}
                    className="mt-2 min-h-[100px] w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm"
                    placeholder={
                      modal.type === "CANCEL" ? "Optional remarks" : "Add remarks (required)"
                    }
                  />
                </div>
              </div>
            )}

            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setModal(null)}>
                Cancel
              </Button>
              <Button
                onClick={() => {
                  if (!modal || !trip) return;
                  if (modal.type === "DOC_UPLOAD") {
                    if (!modal.storageKey.trim()) {
                      show("Storage key is required.", "error");
                      return;
                    }
                    handleUploadDoc(modal.docType, modal.storageKey.trim());
                    setModal(null);
                  } else if (modal.type === "DOC_REJECT") {
                    if (!modal.remarks.trim()) {
                      show("Remarks are required.", "error");
                      return;
                    }
                    handleRejectDoc(modal.docId, modal.remarks.trim());
                    setModal(null);
                  } else if (modal.type === "HOLD") {
                    if (!modal.remarks.trim()) {
                      show("Remarks are required.", "error");
                      return;
                    }
                    const eventAt = fromLocalInput(modal.eventAt) ?? new Date().toISOString();
                    handleStatusChange("ON_HOLD", modal.remarks.trim(), null, eventAt);
                    setModal(null);
                  } else if (modal.type === "FAILED") {
                    if (!modal.remarks.trim()) {
                      show("Remarks are required.", "error");
                      return;
                    }
                    const eventAt = fromLocalInput(modal.eventAt) ?? new Date().toISOString();
                    handleStatusChange("FAILED_ATTEMPT", modal.remarks.trim(), null, eventAt);
                    setModal(null);
                  } else if (modal.type === "RESOLVE_FAILED") {
                    if (!modal.remarks.trim()) {
                      show("Remarks are required.", "error");
                      return;
                    }
                    if (!resumeFromFailedAttempt) {
                      show("Previous status not available.", "error");
                      return;
                    }
                    const eventAt = fromLocalInput(modal.eventAt) ?? new Date().toISOString();
                    handleStatusChange(resumeFromFailedAttempt, modal.remarks.trim(), null, eventAt);
                    setModal(null);
                  } else if (modal.type === "CANCEL") {
                    const eventAt = fromLocalInput(modal.eventAt) ?? new Date().toISOString();
                    const remarks = modal.remarks.trim() ? modal.remarks.trim() : null;
                    handleStatusChange("CANCELLED", remarks, null, eventAt);
                    setModal(null);
                  } else if (modal.type === "CLOSE") {
                    handleStatusChange("CLOSED");
                    setModal(null);
                  }
                }}
              >
                Confirm
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function readWaybillValue(data: Record<string, unknown> | null, key: string) {
  if (!data) return null;
  const camel = key.charAt(0).toLowerCase() + key.slice(1);
  const value = data[key] ?? data[camel];
  return value == null ? null : String(value);
}

function formatWaybillDate(value: string | null) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function WaybillField({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <p className="text-xs uppercase text-muted-foreground">{label}</p>
      <p className="mt-1 font-medium text-foreground">{value || "-"}</p>
    </div>
  );
}



