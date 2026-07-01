import { ClipboardList, Route, UserPlus } from "lucide-react";
import KpiCard from "@/components/KpiCard";
import type { DashboardCharts, DashboardKpis } from "../kpis";
import { DashboardSection, formatNumber, KpiGrid } from "./sectionPrimitives";
import { ChartGrid } from "./charts/chartPrimitives";
import DispatcherDocumentAlertBar from "./charts/DispatcherDocumentAlertBar";
import DispatcherTripStatusDonut from "./charts/DispatcherTripStatusDonut";
import DispatcherTripsPerDayBar from "./charts/DispatcherTripsPerDayBar";
import RecommendationPanel from "@/features/dispatch/components/RecommendationPanel";
import { useDispatchHub } from "@/hooks/useDispatchHub";
import { emitToast } from "@/lib/toastBus";
import { clearDashboardKpiCache } from "../kpis";

type Props = {
  kpis: DashboardKpis;
  charts: DashboardCharts;
  loading: boolean;
};

export default function DispatcherDashboardSection({ kpis, charts, loading }: Props) {
  const dispatch = kpis.dispatchKpis;

  useDispatchHub({
    onRecommendationGenerated: (event) => {
      emitToast(
        `${event.driverName} just delivered - ${event.recommendationCount} nearby jobs available`,
        "success"
      );
      window.dispatchEvent(new Event("nvg:recommendations-refresh"));
    },
    onTripStatusChanged: () => {
      clearDashboardKpiCache();
    }
  });

  return (
    <DashboardSection
      title="Dispatcher Dashboard"
      description="Live trip movement, assignment capacity, and conversion queues."
      loading={loading}
      empty={dispatch === null}
      actions={[
        { label: "New Trip", to: "/dispatch/trips", icon: ClipboardList },
        { label: "View Dispatch Queue", to: "/dispatch/requests", icon: Route },
        { label: "Assign Drivers", to: "/dispatch/board", icon: UserPlus }
      ]}
    >
      <RecommendationPanel />
      <KpiGrid>
        <KpiCard title="Active Trips" value={formatNumber(dispatch?.activeTrips)} subtitle="Trips currently moving" />
        <KpiCard title="Drivers Available" value={formatNumber(dispatch?.driversAvailable)} subtitle="Ready for assignment" />
        <KpiCard title="To Convert" value={formatNumber(dispatch?.approvedShipmentRequests)} subtitle="Approved shipment requests" />
        <KpiCard title="Document Alerts" value={formatNumber(dispatch?.incompleteDocumentAlerts)} subtitle="Incomplete docs on active trips" />
        <KpiCard title="Draft Trips" value={formatNumber(dispatch?.tripsInDraft)} subtitle="Not yet dispatched" />
      </KpiGrid>
      <ChartGrid>
        <DispatcherTripStatusDonut data={dispatch?.statusBreakdown ?? []} loading={loading} />
        <DispatcherTripsPerDayBar data={charts.dispatcherWeeklyTrips} loading={loading} />
        <DispatcherDocumentAlertBar data={charts.documentAlerts} loading={loading} />
      </ChartGrid>
    </DashboardSection>
  );
}
