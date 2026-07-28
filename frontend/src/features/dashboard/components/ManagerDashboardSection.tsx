import { ClipboardCheck, FileText, Route } from "lucide-react";
import KpiCard from "@/components/KpiCard";
import type { DashboardCharts, DashboardKpis } from "../kpis";
import { DashboardSection, formatNumber, KpiGrid } from "./sectionPrimitives";
import { ChartGrid } from "./charts/chartPrimitives";
import ManagerCompletionRateLine from "./charts/ManagerCompletionRateLine";
import ManagerDriverUtilizationBar from "./charts/ManagerDriverUtilizationBar";
import ManagerOnTimeStackedBar from "./charts/ManagerOnTimeStackedBar";

type Props = {
  kpis: DashboardKpis;
  charts: DashboardCharts;
  loading: boolean;
};

export default function ManagerDashboardSection({ kpis, charts, loading }: Props) {
  const dispatch = kpis.dispatchKpis;
  const hasData =
    dispatch !== null ||
    kpis.pendingManagerCount !== null ||
    kpis.lowStockCount !== null;

  return (
    <DashboardSection
      title="Manager Dashboard"
      description="Approval queues, dispatch exceptions, and stock risk."
      loading={loading}
      empty={!hasData}
      actions={[
        { label: "Approve Requests", to: "/dispatch/requests", icon: ClipboardCheck },
        { label: "Open Planning", to: "/dispatch/planning", icon: Route },
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
        <KpiCard title="Document Alerts" value={formatNumber(dispatch?.documentAlerts)} subtitle="Uploads needing verification" />
        <KpiCard title="Shipment Approvals" value={formatNumber(dispatch?.pendingShipmentRequests)} subtitle="Submitted requests" />
        <KpiCard
          title="Hold / Failed"
          value={dispatch ? dispatch.tripsOnHold + dispatch.tripsFailedAttempt : null}
          subtitle="Trips needing manager review"
        />
        <KpiCard title="Inventory Approvals" value={formatNumber(kpis.pendingManagerCount)} subtitle="Pending manager requests" />
        <KpiCard title="Low Stock" value={formatNumber(kpis.lowStockCount)} subtitle="Items below reorder level" />
      </KpiGrid>
      <ChartGrid>
        <ManagerCompletionRateLine data={charts.managerWeeklyCompletion} loading={loading} />
        <ManagerDriverUtilizationBar data={charts.driverUtilization} loading={loading} />
        <ManagerOnTimeStackedBar data={charts.onTimeVsDelayed} loading={loading} />
      </ChartGrid>
    </DashboardSection>
  );
}
