import { useCallback, useEffect, useState } from "react";
import { PackagePlus, Search } from "lucide-react";
import KpiCard from "@/components/KpiCard";
import StatusBadge from "@/components/StatusBadge";
import type { DashboardCharts, DashboardKpis } from "../kpis";
import { DashboardSection, formatNumber, KpiGrid } from "./sectionPrimitives";
import { ChartGrid } from "./charts/chartPrimitives";
import CustomerDeliveriesPerMonthBar from "./charts/CustomerDeliveriesPerMonthBar";
import CustomerRequestStatusDonut from "./charts/CustomerRequestStatusDonut";
import { useDispatchHub } from "@/hooks/useDispatchHub";
import { clearDashboardKpiCache } from "../kpis";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import type { CustomerShipmentListItem } from "@/features/portal/types";
import { statusLabels } from "@/features/dispatch/types";

type Props = {
  kpis: DashboardKpis;
  charts: DashboardCharts;
  loading: boolean;
};

function formatRequestStatuses(statuses?: { status: string; count: number }[]) {
  if (!statuses || statuses.length === 0) {
    return "No active request statuses";
  }

  return statuses.map((item) => `${item.status}: ${item.count}`).join(" | ");
}

export default function CustomerDashboardSection({ kpis, charts, loading }: Props) {
  const customer = kpis.customerKpis;
  const [latestShipment, setLatestShipment] = useState<CustomerShipmentListItem | null>(null);

  const loadLatestShipment = useCallback(async () => {
    if (!customer) {
      setLatestShipment(null);
      return;
    }

    try {
      const result = await api<PagedResult<CustomerShipmentListItem>>("/api/portal/shipments?page=1&pageSize=1", {
        method: "GET"
      });
      setLatestShipment(result.items?.[0] ?? null);
    } catch {
      setLatestShipment(null);
    }
  }, [customer]);

  useDispatchHub({
    onTripStatusChanged: () => {
      clearDashboardKpiCache();
      void loadLatestShipment();
    }
  });

  useEffect(() => {
    void loadLatestShipment();
  }, [loadLatestShipment]);

  return (
    <DashboardSection
      title="Customer Dashboard"
      description="Shipment request activity, deliveries, and pending document work."
      loading={loading}
      empty={customer === null}
      actions={[
        { label: "New Shipment Request", to: "/portal/requests/new", icon: PackagePlus },
        { label: "Track My Shipments", to: "/portal/shipments", icon: Search }
      ]}
    >
      <div className="grid gap-3 md:grid-cols-[0.85fr_1.15fr]">
        <div className="surface-card border-primary/20 bg-primary/5 p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-primary">Active Requests</p>
          <div className="mt-2 text-3xl font-semibold text-foreground">
            {formatNumber(customer?.myActiveRequests) ?? "0"}
          </div>
          <p className="text-sm text-muted-foreground">
            {formatRequestStatuses(customer?.myActiveRequestStatuses)}
          </p>
        </div>
        <div className="surface-card p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            Latest Shipment
          </p>
          {latestShipment ? (
            <div className="mt-2 space-y-2">
              <div className="flex flex-wrap items-center gap-2">
                <p className="font-mono text-xl font-semibold text-foreground">
                  {latestShipment.containerNumber ?? latestShipment.tripId.slice(0, 8)}
                </p>
                <StatusBadge status={statusLabels[latestShipment.status] ?? latestShipment.status} />
              </div>
              <p className="text-sm text-muted-foreground">
                {latestShipment.pickupLocation} to {latestShipment.dropoffLocation}
              </p>
            </div>
          ) : (
            <div className="mt-2">
              <p className="text-lg font-semibold text-foreground">No shipments yet</p>
              <p className="text-sm text-muted-foreground">Approved trips will appear here for tracking.</p>
            </div>
          )}
        </div>
      </div>
      <KpiGrid>
        <KpiCard title="Delivered This Month" value={formatNumber(customer?.myDeliveredThisMonth)} subtitle="Completed shipments" />
        <KpiCard title="Pending Documents" value={formatNumber(customer?.myPendingDocuments)} subtitle="Documents to provide" />
      </KpiGrid>
      <ChartGrid>
        <CustomerRequestStatusDonut data={customer?.statusBreakdown ?? []} loading={loading} />
        <CustomerDeliveriesPerMonthBar data={charts.customerMonthlyDeliveries} loading={loading} />
      </ChartGrid>
    </DashboardSection>
  );
}
