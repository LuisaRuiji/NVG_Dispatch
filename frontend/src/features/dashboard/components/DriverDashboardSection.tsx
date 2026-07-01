import { useEffect, useState } from "react";
import { FileUp, Truck } from "lucide-react";
import { Link } from "react-router-dom";
import KpiCard from "@/components/KpiCard";
import StatusBadge from "@/components/StatusBadge";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import type { DispatchTripListItem } from "@/features/dispatch/types";
import { getDriverDocumentBlockers, summarizeDocumentBlockers } from "@/features/dispatch/driverTripUi";
import type { DashboardKpis } from "../kpis";
import { DashboardSection, formatNumber, KpiGrid } from "./sectionPrimitives";
import { ChartGrid } from "./charts/chartPrimitives";
import DriverDocumentComplianceDonut from "./charts/DriverDocumentComplianceDonut";
import DriverOnTimeDonut from "./charts/DriverOnTimeDonut";
import DriverTripsPerDayBar from "./charts/DriverTripsPerDayBar";
import { useDispatchHub } from "@/hooks/useDispatchHub";
import { emitToast } from "@/lib/toastBus";
import { clearDashboardKpiCache } from "../kpis";

type Props = {
  kpis: DashboardKpis;
  loading: boolean;
};

function formatDocumentType(type: string) {
  return type.split("_").join(" ");
}

export default function DriverDashboardSection({ kpis, loading }: Props) {
  const driver = kpis.driverKpis;
  const [activeTripBlockers, setActiveTripBlockers] = useState<string[]>([]);
  const activeTripHref = driver?.myActiveTrip?.id
    ? `/dispatch/my-trips/${driver.myActiveTrip.id}`
    : "/dispatch/my-trips";
  const activeTripId = driver?.myActiveTrip?.id;
  const pendingDocumentCount = driver?.myPendingDocuments ?? 0;

  useEffect(() => {
    if (!activeTripId) {
      setActiveTripBlockers([]);
      return;
    }

    let cancelled = false;

    (async () => {
      try {
        const params = new URLSearchParams({
          scope: "ACTIVE",
          page: "1",
          pageSize: "5"
        });
        const result = await api<PagedResult<DispatchTripListItem>>(
          `/api/dispatch/my-trips?${params.toString()}`,
          { method: "GET" }
        );
        const activeTrip =
          result.items?.find((trip) => trip.id === activeTripId) ?? result.items?.[0] ?? null;
        const blockers = summarizeDocumentBlockers(
          getDriverDocumentBlockers(activeTrip?.documents, pendingDocumentCount)
        );
        if (!cancelled) {
          setActiveTripBlockers(blockers);
        }
      } catch {
        if (!cancelled) {
          setActiveTripBlockers(summarizeDocumentBlockers(getDriverDocumentBlockers(undefined, pendingDocumentCount)));
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [activeTripId, pendingDocumentCount]);

  useDispatchHub({
    onTripStatusChanged: (event) => {
      if (!activeTripId || event.tripId === activeTripId) {
        clearDashboardKpiCache();
      }
    },
    onDocumentVerified: (event) => {
      if (!activeTripId || event.tripId === activeTripId) {
        clearDashboardKpiCache();
      }
      emitToast(
        `Your ${formatDocumentType(event.documentType)} was ${event.isVerified ? "verified" : "rejected"}.`,
        event.isVerified ? "success" : "error"
      );
    }
  });

  return (
    <DashboardSection
      title="Driver Dashboard"
      description="Current assignment, trip counts, and document follow-up."
      loading={loading}
      empty={driver === null}
      actions={[
        { label: "View My Trips", to: "/dispatch/my-trips", icon: Truck },
        { label: "Upload Document", to: activeTripHref, icon: FileUp }
      ]}
    >
      <div className="surface-card border-primary/20 bg-primary/5 p-4 md:p-5">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-primary">Active Trip</p>
            {driver?.myActiveTrip ? (
              <>
                <div className="mt-2 flex flex-wrap items-center gap-2">
                  <h3 className="text-lg font-semibold text-foreground">
                    {driver.myActiveTrip.customerName ?? driver.myActiveTrip.truckAssetCode ?? "Current assignment"}
                  </h3>
                  <StatusBadge status={driver.myActiveTrip.status} />
                </div>
                <p className="mt-1 text-sm text-muted-foreground">
                  {driver.myActiveTrip.truckAssetCode
                    ? `Truck ${driver.myActiveTrip.truckAssetCode}`
                    : "Open the trip to continue status updates."}
                </p>
              </>
            ) : (
              <>
                <h3 className="mt-2 text-lg font-semibold text-foreground">No active trip</h3>
                <p className="mt-1 text-sm text-muted-foreground">Assigned dispatch work will appear here first.</p>
              </>
            )}
          </div>
          {driver?.myActiveTrip ? (
            <Link
              to={activeTripHref}
              className="inline-flex h-12 w-full items-center justify-center rounded-full bg-primary px-5 text-sm font-semibold text-primary-foreground shadow transition-all duration-200 active:scale-[0.98] sm:w-auto"
            >
              Update Status
            </Link>
          ) : null}
        </div>
        <div className="mt-3 flex flex-wrap gap-2">
          {activeTripBlockers.length > 0 ? (
            activeTripBlockers.map((blocker) => (
              <span
                key={blocker}
                className="rounded-full border border-amber-200 bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-800"
              >
                {blocker}
              </span>
            ))
          ) : pendingDocumentCount > 0 ? (
            <span className="rounded-full border border-amber-200 bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-800">
              {formatNumber(pendingDocumentCount)}{" "}
              {pendingDocumentCount === 1 ? "document needs" : "documents need"} attention
            </span>
          ) : (
            <span className="rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700">
              Documents current
            </span>
          )}
        </div>
      </div>
      <KpiGrid>
        <KpiCard title="Trips Today" value={formatNumber(driver?.myTripsToday)} subtitle="Assigned today" />
        <KpiCard title="Trips This Week" value={formatNumber(driver?.myTripsThisWeek)} subtitle="Assigned this week" />
        <KpiCard title="Pending Documents" value={formatNumber(driver?.myPendingDocuments)} subtitle="Docs to upload" />
      </KpiGrid>
      <ChartGrid>
        <DriverTripsPerDayBar data={driver?.dailyTrips ?? []} loading={loading} />
        <DriverOnTimeDonut percent={driver?.onTimeRate} loading={loading} />
        <DriverDocumentComplianceDonut percent={driver?.docComplianceRate} loading={loading} />
      </ChartGrid>
    </DashboardSection>
  );
}
