import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import type { DispatchTripListItem, TripStatus } from "./types";
import { statusLabels } from "./types";
import { Plus, RefreshCw } from "lucide-react";

type FilterState = {
  status: "ALL" | TripStatus;
  from: string;
  to: string;
};

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
};

export default function DispatchBoardPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const [filters, setFilters] = useState<FilterState>({
    status: "ALL",
    from: "",
    to: ""
  });
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);
  const [trips, setTrips] = useState<DispatchTripListItem[]>([]);
  const [scheduledTrips, setScheduledTrips] = useState<DispatchTripListItem[]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [creating, setCreating] = useState(false);
  const [customers, setCustomers] = useState<CustomerOption[]>([]);
  const [drivers, setDrivers] = useState<DriverOption[]>([]);
  const [trucks, setTrucks] = useState<TruckOption[]>([]);
  const [form, setForm] = useState<ScheduleForm>({
    customerId: "",
    pickupLocation: "",
    pickupScheduledAt: "",
    dropoffLocation: "",
    dropoffScheduledAt: "",
    driverUserId: "",
    truckAssetId: "",
    notes: ""
  });

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const scheduledRows = useMemo(
    () => scheduledTrips.filter((trip) => trip.status === "DRAFT"),
    [scheduledTrips]
  );

  const activeTrips = useMemo(
    () => trips.filter((trip) => trip.status !== "DRAFT"),
    [trips]
  );

  const loadTrips = async (forcePage?: number) => {
    const targetPage = forcePage ?? page;
    try {
      if (filters.status === "DRAFT") {
        setTrips([]);
        setTotalCount(0);
        setLoading(false);
        return;
      }
      setLoading(true);
      const params = new URLSearchParams();
      params.set("page", targetPage.toString());
      params.set("pageSize", pageSize.toString());
      if (filters.status !== "ALL") {
        params.set("status", filters.status);
      }
      if (filters.from) params.set("from", filters.from);
      if (filters.to) params.set("to", filters.to);

      const result = await api<PagedResult<DispatchTripListItem>>(`/api/dispatch/trips?${params.toString()}`, {
        method: "GET"
      });
      setTrips(result.items ?? []);
      setTotalCount(result.totalCount ?? 0);
      setPage(result.page ?? targetPage);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load dispatch board.", "error");
    } finally {
      setLoading(false);
    }
  };

  const loadScheduled = async () => {
    try {
      const params = new URLSearchParams();
      params.set("status", "DRAFT");
      params.set("page", "1");
      params.set("pageSize", "50");
      if (filters.from) params.set("from", filters.from);
      if (filters.to) params.set("to", filters.to);
      const result = await api<PagedResult<DispatchTripListItem>>(`/api/dispatch/trips?${params.toString()}`, {
        method: "GET"
      });
      setScheduledTrips(result.items ?? []);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load scheduled trips.", "error");
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
    loadTrips(1);
    loadScheduled();
  }, [filters.status, filters.from, filters.to]);

  useEffect(() => {
    loadTrips();
  }, [page]);

  useEffect(() => {
    loadReferenceData();
  }, []);

  const canSubmit =
    form.customerId &&
    form.pickupLocation &&
    form.pickupScheduledAt &&
    form.dropoffLocation &&
    form.dropoffScheduledAt;

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Dispatch Board"
        description="Monitor active trips, assignments, and delivery progress."
        actions={
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" className="gap-2" onClick={() => loadTrips()} disabled={loading}>
              <RefreshCw className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"} />
              Refresh
            </Button>
            <Button size="sm" className="gap-2" onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" />
              Create Scheduled Trip
            </Button>
          </div>
        }
      />

      <div className="surface-card p-6">
        <div className="grid gap-4 md:grid-cols-[220px_200px_200px_auto] md:items-end">
          <div>
            <label className="text-xs uppercase text-muted-foreground">Status</label>
            <select
              value={filters.status}
              onChange={(e) =>
                setFilters((prev) => ({ ...prev, status: e.target.value as FilterState["status"] }))
              }
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            >
              <option value="ALL">All</option>
              {Object.entries(statusLabels).map(([key, label]) => (
                <option key={key} value={key}>
                  {label}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">From</label>
            <input
              type="date"
              value={filters.from}
              onChange={(e) => setFilters((prev) => ({ ...prev, from: e.target.value }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">To</label>
            <input
              type="date"
              value={filters.to}
              onChange={(e) => setFilters((prev) => ({ ...prev, to: e.target.value }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            />
          </div>
          <div className="flex items-center justify-end text-sm text-muted-foreground">
            {loading ? "Loading..." : `${totalCount} trip(s)`}
          </div>
        </div>
      </div>

      <div className="space-y-6">
        <div className="surface-card p-6">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">Scheduled</p>
              <h3 className="text-sm font-semibold text-foreground">Draft Trips</h3>
            </div>
            <span className="text-xs text-muted-foreground">{scheduledRows.length} scheduled</span>
          </div>
          {loading && scheduledRows.length === 0 ? (
            <LoadingSkeleton rows={4} />
          ) : scheduledRows.length === 0 ? (
            <EmptyState title="No scheduled trips" description="Create a draft trip to start scheduling." />
          ) : (
            <DataTable>
              <thead className="bg-muted/30 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
                <tr>
                  <th className="px-6 py-4 text-left">Trip</th>
                  <th className="px-6 py-4 text-left">Customer</th>
                  <th className="px-6 py-4 text-left">Pickup (Planned)</th>
                  <th className="px-6 py-4 text-left">Dropoff (Planned)</th>
                  <th className="px-6 py-4 text-left">Driver</th>
                  <th className="px-6 py-4 text-left">Truck</th>
                  <th className="px-6 py-4 text-right">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/50">
                {scheduledRows.map((trip) => (
                  <tr key={trip.id} className="text-sm">
                    <td className="px-6 py-4 font-medium text-foreground">{trip.id.slice(0, 8)}</td>
                    <td className="px-6 py-4">{trip.customer?.name ?? "-"}</td>
                    <td className="px-6 py-4">
                      {trip.pickupScheduledAt ? new Date(trip.pickupScheduledAt).toLocaleString() : "-"}
                    </td>
                    <td className="px-6 py-4">
                      {trip.dropoffScheduledAt ? new Date(trip.dropoffScheduledAt).toLocaleString() : "-"}
                    </td>
                    <td className="px-6 py-4">{trip.driverUsername ?? "-"}</td>
                    <td className="px-6 py-4">{trip.truckAssetCode ?? "-"}</td>
                    <td className="px-6 py-4 text-right">
                      <Button variant="outline" size="sm" onClick={() => nav(`/dispatch/trips/${trip.id}`)}>
                        View
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </DataTable>
          )}
        </div>

        <div className="surface-card p-6">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">Active</p>
              <h3 className="text-sm font-semibold text-foreground">Trips In Motion</h3>
            </div>
            <span className="text-xs text-muted-foreground">{activeTrips.length} trip(s)</span>
          </div>
          {loading && trips.length === 0 ? (
            <LoadingSkeleton rows={6} />
          ) : activeTrips.length === 0 ? (
            <EmptyState title="No active trips" description="Try adjusting your filters." />
          ) : (
            <>
              <DataTable>
                <thead className="bg-muted/30 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
                  <tr>
                    <th className="px-6 py-4 text-left">Trip</th>
                    <th className="px-6 py-4 text-left">Customer</th>
                    <th className="px-6 py-4 text-left">Driver</th>
                    <th className="px-6 py-4 text-left">Truck</th>
                    <th className="px-6 py-4 text-left">Status</th>
                    <th className="px-6 py-4 text-left">Docs</th>
                    <th className="px-6 py-4 text-right">Updated</th>
                    <th className="px-6 py-4 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/50">
                  {activeTrips.map((trip) => {
                    const updated = trip.updatedAt ?? trip.createdAt;
                    return (
                      <tr key={trip.id} className="text-sm">
                        <td className="px-6 py-4 font-medium text-foreground">{trip.id.slice(0, 8)}</td>
                        <td className="px-6 py-4">{trip.customer?.name ?? "-"}</td>
                        <td className="px-6 py-4">{trip.driverUsername ?? "-"}</td>
                        <td className="px-6 py-4">{trip.truckAssetCode ?? "-"}</td>
                        <td className="px-6 py-4">
                          <StatusBadge status={statusLabels[trip.status] ?? trip.status} />
                        </td>
                        <td className="px-6 py-4 text-muted-foreground">
                          {trip.uploadedDocumentCount}/{trip.requiredDocumentCount}
                          {trip.podPending ? " • POD pending" : ""}
                        </td>
                        <td className="px-6 py-4 text-right text-xs text-muted-foreground">
                          {new Date(updated).toLocaleString()}
                        </td>
                        <td className="px-6 py-4 text-right">
                          <Button variant="outline" size="sm" onClick={() => nav(`/dispatch/trips/${trip.id}`)}>
                            View
                          </Button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </DataTable>

              <div className="mt-4 flex items-center justify-between text-sm text-muted-foreground">
                <span>
                  Page {page} of {totalPages}
                </span>
                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page <= 1 || loading}
                  >
                    Prev
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    disabled={page >= totalPages || loading}
                  >
                    Next
                  </Button>
                </div>
              </div>
            </>
          )}
        </div>
      </div>

      {createOpen ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          onClick={() => setCreateOpen(false)}
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,600px)] rounded-2xl border border-slate-200 bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Schedule Trip</p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">Create Scheduled Trip</h2>
              </div>
              <button
                onClick={() => setCreateOpen(false)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>

            <div className="mt-5 grid gap-4 text-sm md:grid-cols-2">
              <div className="md:col-span-2">
                <label className="text-xs uppercase text-slate-500">Customer</label>
                <select
                  value={form.customerId}
                  onChange={(e) => setForm((prev) => ({ ...prev, customerId: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                >
                  <option value="">Select customer</option>
                  {customers.map((customer) => (
                    <option key={customer.id} value={customer.id}>
                      {customer.name}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className="text-xs uppercase text-slate-500">Pickup Location</label>
                <input
                  value={form.pickupLocation}
                  onChange={(e) => setForm((prev) => ({ ...prev, pickupLocation: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-slate-500">Pickup Time</label>
                <input
                  type="datetime-local"
                  value={form.pickupScheduledAt}
                  onChange={(e) => setForm((prev) => ({ ...prev, pickupScheduledAt: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-slate-500">Dropoff Location</label>
                <input
                  value={form.dropoffLocation}
                  onChange={(e) => setForm((prev) => ({ ...prev, dropoffLocation: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-slate-500">Dropoff Time</label>
                <input
                  type="datetime-local"
                  value={form.dropoffScheduledAt}
                  onChange={(e) => setForm((prev) => ({ ...prev, dropoffScheduledAt: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-slate-500">Driver (Optional)</label>
                <select
                  value={form.driverUserId}
                  onChange={(e) => setForm((prev) => ({ ...prev, driverUserId: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
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
                <label className="text-xs uppercase text-slate-500">Truck (Optional)</label>
                <select
                  value={form.truckAssetId}
                  onChange={(e) => setForm((prev) => ({ ...prev, truckAssetId: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
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
                <label className="text-xs uppercase text-slate-500">Notes</label>
                <input
                  value={form.notes}
                  onChange={(e) => setForm((prev) => ({ ...prev, notes: e.target.value }))}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
              </div>
            </div>

            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setCreateOpen(false)} disabled={creating}>
                Cancel
              </Button>
              <Button
                onClick={async () => {
                  if (!canSubmit) {
                    show("Customer, pickup, and dropoff schedules are required.", "error");
                    return;
                  }
                  try {
                    setCreating(true);
                    await api(`/api/dispatch/trips`, {
                      method: "POST",
                      body: JSON.stringify({
                        customerId: form.customerId,
                        driverUserId: form.driverUserId || null,
                        truckAssetId: form.truckAssetId || null,
                        notes: form.notes || null,
                        stops: [
                          {
                            stopType: "PICKUP",
                            locationText: form.pickupLocation,
                            scheduledAt: new Date(form.pickupScheduledAt).toISOString()
                          },
                          {
                            stopType: "DROPOFF",
                            locationText: form.dropoffLocation,
                            scheduledAt: new Date(form.dropoffScheduledAt).toISOString()
                          }
                        ]
                      })
                    });
                    show("Scheduled trip created.", "success");
                    setCreateOpen(false);
                    setForm({
                      customerId: "",
                      pickupLocation: "",
                      pickupScheduledAt: "",
                      dropoffLocation: "",
                      dropoffScheduledAt: "",
                      driverUserId: "",
                      truckAssetId: "",
                      notes: ""
                    });
                    loadScheduled();
                    loadTrips();
                  } catch (e: any) {
                    console.error(e);
                    show(e?.message ?? "Failed to create trip.", "error");
                  } finally {
                    setCreating(false);
                  }
                }}
                disabled={!canSubmit || creating}
              >
                {creating ? "Creating..." : "Create Trip"}
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
