import { BarChart3, Route } from "lucide-react";
import KpiCard from "@/components/KpiCard";
import { useMediaQuery } from "@/hooks/useMediaQuery";
import type { DashboardCharts, DashboardKpis } from "../kpis";
import { DashboardSection, formatCurrency, formatNumber, formatPercent } from "./sectionPrimitives";
import CeoFleetUtilizationLine from "./charts/CeoFleetUtilizationLine";
import CeoRevenueVsPayrollBar from "./charts/CeoRevenueVsPayrollBar";
import CeoTripVolumeBar from "./charts/CeoTripVolumeBar";

type Props = {
  kpis: DashboardKpis;
  charts: DashboardCharts;
  loading: boolean;
};

export default function CeoDashboardSection({ kpis, charts, loading }: Props) {
  const finance = kpis.financeKpis;
  const system = kpis.systemKpis;
  const hasData = finance !== null || system !== null || kpis.pendingCeoPoCount !== null;
  const showDesktopCharts = useMediaQuery("(min-width: 768px)");

  return (
    <DashboardSection
      title="CEO Dashboard"
      description="Fleet output, utilization, financial totals, and document compliance."
      loading={loading}
      empty={!hasData}
      actions={[
        { label: "View Full Reports", to: "/reports", icon: BarChart3 },
        { label: "View Dispatch Overview", to: "/dispatch/board", icon: Route }
      ]}
    >
      <div className="grid grid-cols-1 gap-3 min-[420px]:grid-cols-2 md:grid-cols-3 xl:grid-cols-6">
        <KpiCard title="Trips This Month" value={formatNumber(system?.totalTripsThisMonth)} subtitle="Total created trips" />
        <KpiCard title="Fleet Utilization" value={formatPercent(system?.fleetUtilizationPercent)} subtitle="Active trucks in use" />
        <KpiCard title="Trip Value" value={formatCurrency(finance?.totalTripValueThisMonth)} subtitle="This month" />
        <KpiCard title="Payroll" value={formatCurrency(finance?.totalPayrollThisMonth)} subtitle="Delivered trips this month" />
        <KpiCard title="Pending CEO" value={formatNumber(kpis.pendingCeoPoCount)} subtitle="Purchase orders" />
        <KpiCard title="Document Compliance" value={formatPercent(system?.documentComplianceRate)} subtitle="Delivered trips verified" />
      </div>
      <div className="grid gap-4 lg:grid-cols-3">
        <CeoFleetUtilizationLine data={charts.ceoFleetUtilization} loading={loading} />
        {showDesktopCharts ? <CeoTripVolumeBar data={charts.ceoTripVolume} loading={loading} /> : null}
        {showDesktopCharts ? <CeoRevenueVsPayrollBar data={charts.ceoRevenueVsPayroll} loading={loading} /> : null}
      </div>
    </DashboardSection>
  );
}
