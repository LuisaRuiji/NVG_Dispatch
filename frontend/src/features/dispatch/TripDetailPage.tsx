import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import DataTable from "@/components/DataTable";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import type { DispatchTripDetail, TripDocumentType, TripStatus } from "./types";
import { nextOperationalStatus, operationalFlow, statusLabels } from "./types";
import { Check, XCircle } from "lucide-react";

type CustomerOption = { id: string; name: string };
type DriverOption = { id: string; username: string };
type TruckOption = { id: string; assetCode: string };

type ScheduleForm = {
  customerId: string;
  pickupLocation: string;
  pickupScheduledAt: string;
  dropoffLocation: string;
  dropoffScheduledAt: string;
  driverUserId: string;
  truckAssetId: string;
  notes: string;
  changeRemarks: string;
};

type ActionModal =
  | { type: "HOLD"; remarks: string }
  | { type: "FAILED"; remarks: string }
  | { type: "DOC_UPLOAD"; docType: TripDocumentType; storageKey: string }
  | { type: "DOC_REJECT"; docId: string; remarks: string }
  | null;

const docTypes: TripDocumentType[] = ["WAYBILL", "POD", "ATW"];
const failedAttemptEligible: TripStatus[] = [
  "ENROUTE_PICKUP",
  "AT_PICKUP",
  "ENROUTE_DROPOFF",
  "AT_DROPOFF"
];

const toLocalInput = (iso?: string | null) => {
  if (!iso) return "";
  const date = new Date(iso);
  const offset = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
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
  const isCeo = roles.includes("CEO");

  const [loading, setLoading] = useState(true);
  const [trip, setTrip] = useState<DispatchTripDetail | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [modal, setModal] = useState<ActionModal>(null);
  const [scheduleSaving, setScheduleSaving] = useState(false);
  const [dispatching, setDispatching] = useState(false);
  const [customers, setCustomers] = useState<CustomerOption[]>([]);
  const [drivers, setDrivers] = useState<DriverOption[]>([]);
  const [trucks, setTrucks] = useState<TruckOption[]>([]);
  const [scheduleForm, setScheduleForm] = useState<ScheduleForm>({
    customerId: "",
    pickupLocation: "",
    pickupScheduledAt: "",
    dropoffLocation: "",
    dropoffScheduledAt: "",
    driverUserId: "",
    truckAssetId: "",
    notes: "",
    changeRemarks: ""
  });

  const canVerifyDocs = isManager || isFinance;
  const canViewAny = isManager || isDispatcher || isFinance || isCeo;
  const canEditSchedule = isManager || isDispatcher;

  const fetchTrip = async () => {
    if (!id) return;
    try {
      setLoading(true);
      const detail = await api<DispatchTripDetail>(`/api/dispatch/trips/${id}`, { method: "GET" });
      setTrip(detail);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load trip detail.", "error");
    } finally {
      setLoading(false);
    }
  };

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

  const nextStatus = useMemo(() => {
    if (!trip) return null;
    return nextOperationalStatus(trip.status);
  }, [trip]);

  const plannedStops = useMemo(() => {
    if (!trip) return { pickup: null, dropoff: null };
    const pickup = trip.stops.find((stop) => stop.stopType === "PICKUP") ?? null;
    const dropoff = trip.stops.find((stop) => stop.stopType === "DROPOFF") ?? null;
    return { pickup, dropoff };
  }, [trip]);

  const actualTimes = useMemo(() => {
    if (!trip) return { pickup: null, dropoff: null, delivered: null };
    const pickup = trip.history.find((entry) => entry.toStatus === "AT_PICKUP")?.createdAt ?? null;
    const dropoff = trip.history.find((entry) => entry.toStatus === "AT_DROPOFF")?.createdAt ?? null;
    const delivered = trip.history.find((entry) => entry.toStatus === "DELIVERED")?.createdAt ?? null;
    return { pickup, dropoff, delivered };
  }, [trip]);

  useEffect(() => {
    if (!trip) return;
    setScheduleForm({
      customerId: trip.customer.id,
      pickupLocation: plannedStops.pickup?.locationText ?? "",
      pickupScheduledAt: toLocalInput(plannedStops.pickup?.scheduledAt),
      dropoffLocation: plannedStops.dropoff?.locationText ?? "",
      dropoffScheduledAt: toLocalInput(plannedStops.dropoff?.scheduledAt),
      driverUserId: trip.driverUserId ?? "",
      truckAssetId: trip.truckAssetId ?? "",
      notes: trip.notes ?? "",
      changeRemarks: ""
    });
  }, [trip]);

  const resumeFromFailedAttempt = useMemo(() => {
    if (!trip) return null;
    const last = [...trip.history]
      .reverse()
      .find((entry) => entry.toStatus === "FAILED_ATTEMPT");
    return last?.fromStatus ?? null;
  }, [trip]);

  const buildStopsPayload = () => {
    return [
      {
        stopType: "PICKUP",
        locationText: scheduleForm.pickupLocation,
        scheduledAt: new Date(scheduleForm.pickupScheduledAt).toISOString()
      },
      {
        stopType: "DROPOFF",
        locationText: scheduleForm.dropoffLocation,
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
    if (!trip) return null;
    const assignmentChanged =
      scheduleForm.driverUserId !== (trip.driverUserId ?? "") ||
      scheduleForm.truckAssetId !== (trip.truckAssetId ?? "");
    const loadedOrLater = ["LOADED", "ENROUTE_DROPOFF", "AT_DROPOFF", "DELIVERED"].includes(trip.status);
    if (assignmentChanged && loadedOrLater && isManager && !scheduleForm.changeRemarks.trim()) {
      return "Remarks are required to reassign after loading.";
    }
    return null;
  };

  const handleSaveSchedule = async (silent?: boolean) => {
    if (!trip) return false;
    const error = validateSchedule();
    if (error) {
      show(error, "error");
      return false;
    }
    try {
      setScheduleSaving(true);
      await api(`/api/dispatch/trips/${trip.id}`, {
        method: "PUT",
        body: JSON.stringify({
          customerId: scheduleForm.customerId,
          driverUserId: scheduleForm.driverUserId || null,
          truckAssetId: scheduleForm.truckAssetId || null,
          notes: scheduleForm.notes || null,
          remarks: scheduleForm.changeRemarks || null,
          stops: buildStopsPayload()
        })
      });
      if (!silent) {
        show("Schedule updated.", "success");
      }
      await fetchTrip();
      return true;
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to update schedule.", "error");
      return false;
    } finally {
      setScheduleSaving(false);
    }
  };

  const handleDispatch = async () => {
    if (!trip) return;
    if (!scheduleForm.driverUserId) {
      show("Assign a driver before dispatching.", "error");
      return;
    }
    const saved = await handleSaveSchedule(true);
    if (!saved) return;
    try {
      setDispatching(true);
      await api(`/api/dispatch/trips/${trip.id}/dispatch`, {
        method: "POST",
        body: JSON.stringify({
          driverUserId: scheduleForm.driverUserId,
          truckAssetId: scheduleForm.truckAssetId || null,
          remarks: null
        })
      });
      show("Trip dispatched.", "success");
      await fetchTrip();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to dispatch trip.", "error");
    } finally {
      setDispatching(false);
    }
  };

  const handleStatusChange = async (toStatus: TripStatus, remarks?: string | null) => {
    if (!trip) return;
    try {
      setActionLoading(true);
      await api(`/api/dispatch/trips/${trip.id}/status`, {
        method: "POST",
        body: JSON.stringify({ toStatus, remarks: remarks ?? null })
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

  const handleUploadDoc = async (docType: TripDocumentType, storageKey: string) => {
    if (!trip) return;
    try {
      setActionLoading(true);
      await api(`/api/dispatch/trips/${trip.id}/documents`, {
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

  const handleVerifyDoc = async (docId: string) => {
    if (!trip) return;
    try {
      setActionLoading(true);
      await api(`/api/dispatch/trips/${trip.id}/documents/${docId}/verify`, { method: "POST" });
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
    if (!trip) return;
    try {
      setActionLoading(true);
      await api(`/api/dispatch/trips/${trip.id}/documents/${docId}/reject`, {
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

  const isDraft = trip.status === "DRAFT";
  const canEditCustomer = isManager || isDraft;
  const canEditLocations = isManager || isDraft;
  const dispatcherReassignLocked =
    isDispatcher &&
    !isManager &&
    !isDraft &&
    !["DISPATCHED", "ENROUTE_PICKUP"].includes(trip.status);

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title={`Trip ${trip.id.slice(0, 8)}`}
        description={`Customer: ${trip.customer?.name ?? "-"}`}
        actions={
          <Button variant="outline" onClick={() => nav(-1)}>
            Back
          </Button>
        }
      />

      <div className="surface-card p-6">
        <div className="flex flex-wrap items-center gap-4">
          <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
          <span className="text-sm text-muted-foreground">
            Compliance: {trip.docVerificationEnabled ? "Strict" : "Relaxed"}
          </span>
          {trip.podPending ? (
            <span className="text-xs rounded-full border border-amber-200 bg-amber-50 px-2 py-1 text-amber-700">
              POD Pending
            </span>
          ) : null}
        </div>

        <div className="mt-4 grid gap-3 md:grid-cols-3 text-sm text-muted-foreground">
          <div>
            <p className="text-xs uppercase">Driver</p>
            <p className="mt-1 text-foreground">{trip.driverUsername ?? "-"}</p>
          </div>
          <div>
            <p className="text-xs uppercase">Truck</p>
            <p className="mt-1 text-foreground">{trip.truckAssetCode ?? "-"}</p>
          </div>
          <div>
            <p className="text-xs uppercase">Updated</p>
            <p className="mt-1 text-foreground">
              {new Date(trip.updatedAt ?? trip.createdAt).toLocaleString()}
            </p>
          </div>
        </div>
        <div className="mt-6 grid gap-3 md:grid-cols-3 text-sm text-muted-foreground">
          <div className="rounded-xl border border-border/50 bg-muted/20 px-4 py-3">
            <p className="text-xs uppercase">Planned Pickup</p>
            <p className="mt-1 text-foreground">
              {plannedStops.pickup?.scheduledAt
                ? new Date(plannedStops.pickup.scheduledAt).toLocaleString()
                : "Unscheduled"}
            </p>
          </div>
          <div className="rounded-xl border border-border/50 bg-muted/20 px-4 py-3">
            <p className="text-xs uppercase">Planned Dropoff</p>
            <p className="mt-1 text-foreground">
              {plannedStops.dropoff?.scheduledAt
                ? new Date(plannedStops.dropoff.scheduledAt).toLocaleString()
                : "Unscheduled"}
            </p>
          </div>
          <div className="rounded-xl border border-border/50 bg-muted/20 px-4 py-3">
            <p className="text-xs uppercase">Actual Milestones</p>
            <p className="mt-1 text-foreground text-xs">
              Pickup: {actualTimes.pickup ? new Date(actualTimes.pickup).toLocaleString() : "—"}
            </p>
            <p className="text-foreground text-xs">
              Dropoff: {actualTimes.dropoff ? new Date(actualTimes.dropoff).toLocaleString() : "—"}
            </p>
            <p className="text-foreground text-xs">
              Delivered: {actualTimes.delivered ? new Date(actualTimes.delivered).toLocaleString() : "—"}
            </p>
          </div>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1.2fr_1fr]">
        <div className="space-y-6">
          {canEditSchedule ? (
            <div className="surface-card p-6">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-sm font-semibold">Schedule & Assignment</h3>
                  <p className="text-xs text-muted-foreground">
                    {isDraft
                      ? "Draft trips require planned pickup/dropoff and a customer."
                      : "Dispatchers can adjust planned times and assignments after dispatch."}
                  </p>
                </div>
                <span className="text-xs text-muted-foreground">
                  {isDraft ? "Draft" : "Active"}
                </span>
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
                <label className="text-xs uppercase text-muted-foreground">Notes</label>
                <input
                  value={scheduleForm.notes}
                  onChange={(e) => setScheduleForm((prev) => ({ ...prev, notes: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
                />
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

              <div className="mt-5 flex flex-wrap items-center gap-2">
                <Button
                  variant="outline"
                  onClick={() => handleSaveSchedule()}
                  disabled={scheduleSaving || dispatching}
                >
                  {scheduleSaving ? "Saving..." : "Save Schedule"}
                </Button>
                {isDraft ? (
                  <Button
                    onClick={handleDispatch}
                    disabled={scheduleSaving || dispatching}
                  >
                    {dispatching ? "Dispatching..." : "Dispatch Trip"}
                  </Button>
                ) : null}
              </div>
            </div>
          ) : null}

          <div className="surface-card p-6">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold">Stops</h3>
              <span className="text-xs text-muted-foreground">{trip.stops.length} stop(s)</span>
            </div>
            <div className="mt-4 space-y-3 text-sm">
              {trip.stops.map((stop) => (
                <div key={stop.id} className="rounded-lg border border-border/50 bg-muted/20 px-4 py-3">
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-foreground">{stop.stopType}</span>
                    <span className="text-xs text-muted-foreground">
                      {stop.scheduledAt ? new Date(stop.scheduledAt).toLocaleString() : "Unscheduled"}
                    </span>
                  </div>
                  <p className="mt-2 text-muted-foreground">{stop.locationText}</p>
                </div>
              ))}
            </div>
          </div>

          <div className="surface-card p-6">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold">Documents</h3>
              <span className="text-xs text-muted-foreground">Waybill + POD required</span>
            </div>
            <div className="mt-4 grid gap-3">
              {docTypes.map((type) => {
                const doc = trip.documents.find((d) => d.type === type);
                const state = doc?.state ?? "MISSING";
                return (
                  <div key={type} className="rounded-lg border border-border/50 bg-muted/20 px-4 py-3">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="text-sm font-semibold text-foreground">{type}</p>
                        <p className="text-xs text-muted-foreground">
                          Status: {state}
                          {doc?.remarks ? ` • ${doc.remarks}` : ""}
                        </p>
                      </div>
                      <div className="flex items-center gap-2">
                        {isDriver ? (
                          <Button
                            variant="outline"
                            size="sm"
                            disabled={actionLoading}
                            onClick={() => setModal({ type: "DOC_UPLOAD", docType: type, storageKey: "" })}
                          >
                            Upload
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
                              onClick={() => setModal({ type: "DOC_REJECT", docId: doc.id, remarks: "" })}
                            >
                              <XCircle className="h-4 w-4" />
                              Reject
                            </Button>
                          </>
                        ) : null}
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        <div className="space-y-6">
          <div className="surface-card p-6">
            <h3 className="text-sm font-semibold">Next Actions</h3>
            <p className="mt-1 text-xs text-muted-foreground">
              Delivered requires POD upload unless manager marks POD pending. Closing requires verified POD if strict.
            </p>

            <div className="mt-4 space-y-3">
              {isDriver && nextStatus && operationalFlow.includes(trip.status) ? (
                <Button
                  className="w-full"
                  onClick={() => handleStatusChange(nextStatus)}
                  disabled={actionLoading}
                >
                  Advance to {statusLabels[nextStatus]}
                </Button>
              ) : null}

              {isDriver ? (
                <>
                  <Button
                    variant="outline"
                    className="w-full"
                    onClick={() => setModal({ type: "HOLD", remarks: "" })}
                    disabled={actionLoading || trip.status === "CLOSED" || trip.status === "CANCELLED"}
                  >
                    Place On Hold
                  </Button>
                  <Button
                    variant="outline"
                    className="w-full"
                    onClick={() => setModal({ type: "FAILED", remarks: "" })}
                    disabled={actionLoading || !failedAttemptEligible.includes(trip.status)}
                  >
                    Report Failed Attempt
                  </Button>
                </>
              ) : null}

              {isManager ? (
                <>
                  {trip.status === "DELIVERED" ? (
                    <Button
                      className="w-full"
                      onClick={() => handleStatusChange("CLOSED")}
                      disabled={actionLoading}
                    >
                      Close Trip
                    </Button>
                  ) : null}
                  {trip.status === "ON_HOLD" && trip.holdPreviousStatus ? (
                    <Button
                      variant="outline"
                      className="w-full"
                      onClick={() => handleStatusChange(trip.holdPreviousStatus!)}
                      disabled={actionLoading}
                    >
                      Resume to {statusLabels[trip.holdPreviousStatus!]}
                    </Button>
                  ) : null}
                  {trip.status === "FAILED_ATTEMPT" && resumeFromFailedAttempt ? (
                    <Button
                      variant="outline"
                      className="w-full"
                      onClick={() => handleStatusChange(resumeFromFailedAttempt)}
                      disabled={actionLoading}
                    >
                      Resume to {statusLabels[resumeFromFailedAttempt]}
                    </Button>
                  ) : null}
                  {trip.status !== "CLOSED" && trip.status !== "CANCELLED" ? (
                    <Button
                      variant="destructive"
                      className="w-full"
                      onClick={() => handleStatusChange("CANCELLED")}
                      disabled={actionLoading}
                    >
                      Cancel Trip
                    </Button>
                  ) : null}
                </>
              ) : null}

              {!isDriver && !isManager ? (
                <p className="text-xs text-muted-foreground">
                  {canViewAny ? "No actions available for your role." : "Access limited."}
                </p>
              ) : null}
            </div>
          </div>

          <div className="surface-card p-6">
            <h3 className="text-sm font-semibold">Status Timeline</h3>
            <div className="mt-4 space-y-3 text-sm">
              {trip.history.length === 0 ? (
                <EmptyState title="No history" description="Trip history will appear here." />
              ) : (
                trip.history.map((entry) => (
                  <div key={entry.id} className="rounded-lg border border-border/50 bg-muted/20 px-4 py-3">
                    <div className="flex items-center justify-between text-xs text-muted-foreground">
                      <span>{new Date(entry.createdAt).toLocaleString()}</span>
                      <span>{entry.actorUsername ?? entry.actorUserId}</span>
                    </div>
                    <p className="mt-2 text-sm text-foreground">
                      {entry.eventType === "SCHEDULE_UPDATED"
                        ? "Schedule updated"
                        : `${statusLabels[entry.fromStatus]} → ${statusLabels[entry.toStatus]}`}
                    </p>
                    {entry.remarks ? (
                      <p className="mt-1 text-xs text-muted-foreground">{entry.remarks}</p>
                    ) : null}
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      </div>

      {modal ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          onClick={() => setModal(null)}
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
                    ? "Place on Hold"
                    : "Report Failed Attempt"}
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
            ) : (
              <div className="mt-5 space-y-2 text-sm">
                <label className="text-xs uppercase text-slate-500">Remarks</label>
                <textarea
                  value={modal.remarks}
                  onChange={(e) => setModal({ ...modal, remarks: e.target.value })}
                  className="mt-2 min-h-[100px] w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm"
                  placeholder="Add remarks"
                />
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
                    handleStatusChange("ON_HOLD", modal.remarks.trim());
                    setModal(null);
                  } else {
                    if (!modal.remarks.trim()) {
                      show("Remarks are required.", "error");
                      return;
                    }
                    handleStatusChange("FAILED_ATTEMPT", modal.remarks.trim());
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

