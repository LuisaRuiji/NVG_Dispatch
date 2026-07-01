import { FileText, Truck, Users } from "lucide-react";
import KpiCard from "@/components/KpiCard";
import type { DashboardCharts, DashboardKpis } from "../kpis";
import { DashboardSection, formatNumber, KpiGrid } from "./sectionPrimitives";
import { ChartGrid } from "./charts/chartPrimitives";
import AdminTripVolumeBar from "./charts/AdminTripVolumeBar";
import AdminUsersByRoleDonut from "./charts/AdminUsersByRoleDonut";

type Props = {
  kpis: DashboardKpis;
  charts: DashboardCharts;
  loading: boolean;
};

export default function AdminDashboardSection({ kpis, charts, loading }: Props) {
  const dispatch = kpis.dispatchKpis;
  const system = kpis.systemKpis;
  const hasData =
    dispatch !== null ||
    system !== null ||
    kpis.openInventoryRequestsCount !== null ||
    kpis.lowStockCount !== null;

  return (
    <DashboardSection
      title="Admin Dashboard"
      description="System-wide dispatch, document, inventory, and user health."
      loading={loading}
      empty={!hasData}
      actions={[
        { label: "Manage Users", to: "/admin/users", icon: Users },
        { label: "View All Trips", to: "/dispatch/trips", icon: Truck },
        { label: "View Reports", to: "/reports", icon: FileText }
      ]}
    >
      <KpiGrid>
        <KpiCard title="Active Trips" value={formatNumber(dispatch?.activeTrips)} subtitle="Trips currently moving" />
        <KpiCard
          title="Drivers On Road"
          value={dispatch ? `${dispatch.driversOnRoad} / ${dispatch.driversAvailable}` : null}
          subtitle="On road vs available"
        />
        <KpiCard title="Document Alerts" value={formatNumber(dispatch?.documentAlerts)} subtitle="Uploads pending verification" />
        <KpiCard title="Pending Shipments" value={formatNumber(dispatch?.pendingShipmentRequests)} subtitle="Requests awaiting approval" />
        <KpiCard title="Open Inventory" value={formatNumber(kpis.openInventoryRequestsCount)} subtitle="Requests still in process" />
        <KpiCard title="Low Stock" value={formatNumber(kpis.lowStockCount)} subtitle="Items below reorder level" />
        <KpiCard title="Total Users" value={formatNumber(system?.totalUsers)} subtitle="System user accounts" />
        <KpiCard title="Trips This Month" value={formatNumber(system?.totalTripsThisMonth)} subtitle="Created this month" />
      </KpiGrid>
      <ChartGrid>
        <AdminTripVolumeBar data={system?.monthlyVolume ?? charts.ceoTripVolume} loading={loading} />
        <AdminUsersByRoleDonut data={system?.usersByRole ?? []} loading={loading} />
      </ChartGrid>
    </DashboardSection>
  );
}
