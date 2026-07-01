import { Link } from "react-router-dom";
import { ArrowRight, ClipboardList, Receipt, Settings2, Zap } from "lucide-react";
import KpiCard from "@/components/KpiCard";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import { Badge } from "@/components/ui/badge";
import type { DashboardCharts, DashboardKpis } from "../kpis";
import { ChartGrid } from "./charts/chartPrimitives";
import InventoryRequestVolumeLine from "./charts/InventoryRequestVolumeLine";
import InventoryStockLevelBar from "./charts/InventoryStockLevelBar";

type Props = {
  kpis: DashboardKpis;
  charts: DashboardCharts;
  loading: boolean;
};

const quickActions = [
  { label: "Create Purchase Order", to: "/purchase-orders", icon: Receipt },
  { label: "View Requests", to: "/queue/io", icon: ClipboardList }
];

export default function InventoryOfficerDashboardSection({ kpis, charts, loading }: Props) {
  const hasData =
    kpis.pendingIoCount !== null ||
    kpis.awaitingIssueCount !== null ||
    kpis.openLoansCount !== null ||
    kpis.lowStockCount !== null;

  return (
    <section className="space-y-4">
      <div>
        <h2 className="text-base font-semibold text-foreground">Inventory Officer Dashboard</h2>
        <p className="mt-1 text-sm text-muted-foreground">Operational snapshot across requests, stock, loans, and approvals.</p>
      </div>

      {loading ? (
        <div className="surface-card p-6">
          <LoadingSkeleton rows={6} />
        </div>
      ) : !hasData ? (
        <EmptyState title="No operational KPIs available." description="Dashboard widgets are role-based." />
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <KpiCard title="Pending IO" value={kpis.pendingIoCount} subtitle="Requests awaiting IO review" />
            <KpiCard title="Awaiting Issue" value={kpis.awaitingIssueCount} subtitle="Approved requests" />
            <KpiCard title="Open Loans" value={kpis.openLoansCount} subtitle="Borrowed items open" />
            <KpiCard title="Low Stock" value={kpis.lowStockCount} subtitle="Items below reorder level" />
          </div>

          <div className="grid gap-4 lg:grid-cols-[1.4fr_1fr]">
            <div className="surface-card p-6">
              <div className="mb-4 flex items-center gap-2">
                <Zap className="h-4 w-4 text-primary" />
                <h3 className="text-sm font-semibold text-foreground">Quick Actions</h3>
              </div>
              <div className="grid gap-3">
                {quickActions.map((action) => (
                  <Link
                    key={action.to}
                    to={action.to}
                    className="group/action flex items-center justify-between rounded-xl border border-border/50 bg-card p-4 text-sm font-medium text-foreground transition-all duration-200 hover:border-primary/30 hover:bg-muted/50 hover:shadow-md hover:shadow-primary/5 active:scale-[0.99]"
                  >
                    <span className="flex items-center gap-3">
                      <action.icon className="h-4 w-4 text-primary/70 transition-colors group-hover/action:text-primary" />
                      {action.label}
                    </span>
                    <ArrowRight className="h-4 w-4 text-muted-foreground transition-transform duration-200 group-hover/action:translate-x-1 group-hover/action:text-primary" />
                  </Link>
                ))}
              </div>
            </div>

            <div className="surface-card p-6">
              <div className="mb-6 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Settings2 className="h-4 w-4 text-primary" />
                  <h3 className="text-sm font-semibold text-foreground">Status Summary</h3>
                </div>
                <Badge variant="outline" className="text-[10px] animate-pulse">LIVE</Badge>
              </div>
              <div className="space-y-3 text-sm text-muted-foreground">
                <div className="flex items-center justify-between">
                  <span>IO Queue</span>
                  <span className="font-semibold text-foreground">{kpis.pendingIoCount ?? "-"}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span>Awaiting Issue</span>
                  <span className="font-semibold text-foreground">{kpis.awaitingIssueCount ?? "-"}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span>Open Loans</span>
                  <span className="font-semibold text-foreground">{kpis.openLoansCount ?? "-"}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span>Low Stock</span>
                  <span className="font-semibold text-foreground">{kpis.lowStockCount ?? "-"}</span>
                </div>
              </div>
            </div>
          </div>

          <ChartGrid>
            <InventoryStockLevelBar data={charts.inventoryStockLevels} loading={loading} />
            <InventoryRequestVolumeLine data={charts.inventoryWeeklyRequests} loading={loading} />
          </ChartGrid>
        </>
      )}
    </section>
  );
}
