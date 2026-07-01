import { FileText, Receipt } from "lucide-react";
import KpiCard from "@/components/KpiCard";
import type { DashboardCharts, DashboardKpis } from "../kpis";
import { DashboardSection, formatCurrency, formatNumber, KpiGrid } from "./sectionPrimitives";
import { ChartGrid } from "./charts/chartPrimitives";
import FinanceCostGroupedBar from "./charts/FinanceCostGroupedBar";
import FinanceRevenueArea from "./charts/FinanceRevenueArea";

type Props = {
  kpis: DashboardKpis;
  charts: DashboardCharts;
  loading: boolean;
};

export default function FinanceDashboardSection({ kpis, charts, loading }: Props) {
  const finance = kpis.financeKpis;

  return (
    <DashboardSection
      title="Finance Dashboard"
      description="Trip revenue, payroll exposure, and financial approval gaps."
      loading={loading}
      empty={finance === null}
      actions={[
        { label: "View Financial Reports", to: "/reports", icon: FileText },
        { label: "View Purchase Orders", to: "/purchase-orders", icon: Receipt }
      ]}
    >
      <KpiGrid>
        <KpiCard title="Trip Value" value={formatCurrency(finance?.totalTripValueThisMonth)} subtitle="This month" />
        <KpiCard title="Payroll" value={formatCurrency(finance?.totalPayrollThisMonth)} subtitle="Delivered trips this month" />
        <KpiCard title="Pending Finance" value={formatNumber(finance?.pendingFinancePoCount)} subtitle="Purchase orders" />
        <KpiCard title="Missing Rate" value={formatNumber(finance?.tripsWithMissingRate)} subtitle="Trips missing Rate" />
      </KpiGrid>
      <ChartGrid>
        <FinanceRevenueArea data={charts.financeWeeklyRevenue} loading={loading} />
        <FinanceCostGroupedBar data={charts.financeWeeklyBreakdown} loading={loading} />
      </ChartGrid>
    </DashboardSection>
  );
}
