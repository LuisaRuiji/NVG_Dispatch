import { useEffect, useMemo, useState } from "react";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import EmptyState from "@/components/EmptyState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { useToast } from "@/lib/useToast";
import { getMe } from "@/features/auth/authStore";
import { resolveDashboardRole } from "@/features/auth/roles";
import { RefreshCw } from "lucide-react";
import {
  emptyDashboardCharts,
  fetchDashboardCharts,
  fetchDashboardKpis,
  type DashboardCharts,
  type DashboardKpis
} from "./kpis";
import AdminDashboardSection from "./components/AdminDashboardSection";
import ManagerDashboardSection from "./components/ManagerDashboardSection";
import DispatcherDashboardSection from "./components/DispatcherDashboardSection";
import FinanceDashboardSection from "./components/FinanceDashboardSection";
import DriverDashboardSection from "./components/DriverDashboardSection";
import CustomerDashboardSection from "./components/CustomerDashboardSection";
import InventoryOfficerDashboardSection from "./components/InventoryOfficerDashboardSection";
import CeoDashboardSection from "./components/CeoDashboardSection";

const emptyKpis: DashboardKpis = {
  pendingIoCount: null,
  pendingManagerCount: null,
  awaitingIssueCount: null,
  openLoansCount: null,
  lowStockCount: null,
  pendingFinancePoCount: null,
  pendingCeoPoCount: null,
  myRequestsCount: null,
  openInventoryRequestsCount: null,
  dispatchKpis: null,
  financeKpis: null,
  driverKpis: null,
  customerKpis: null,
  systemKpis: null
};

export default function DashboardPage() {
  const me = getMe();
  const dashboardRole = resolveDashboardRole(me?.roles ?? []);
  const { toasts, show } = useToast();
  const [loading, setLoading] = useState(false);
  const [kpis, setKpis] = useState<DashboardKpis>(emptyKpis);
  const [charts, setCharts] = useState<DashboardCharts>(emptyDashboardCharts);

  const loadKpis = async (force = false) => {
    if (!me) return;
    try {
      setLoading(true);
      const [result, chartResult] = await Promise.all([
        fetchDashboardKpis(me, dashboardRole, force),
        fetchDashboardCharts(me, dashboardRole, force)
      ]);
      setKpis(result);
      setCharts(chartResult);
      if (force && dashboardRole === "Dispatcher") {
        window.dispatchEvent(new Event("nvg:dispatcher-dashboard-refresh"));
      }
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load dashboard metrics.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadKpis();
  }, [me?.userId, dashboardRole]);

  useEffect(() => {
    const handler = () => loadKpis(true);
    window.addEventListener("nvg:kpi-invalidated", handler);
    return () => window.removeEventListener("nvg:kpi-invalidated", handler);
  }, [me?.userId, dashboardRole]);

  const sections = useMemo(() => {
    switch (dashboardRole) {
      case "SuperAdmin":
      case "Admin":
        return [{ key: "admin", element: <AdminDashboardSection kpis={kpis} charts={charts} loading={loading} /> }];
      case "Manager":
        return [{ key: "manager", element: <ManagerDashboardSection kpis={kpis} charts={charts} loading={loading} /> }];
      case "Dispatcher":
        return [{ key: "dispatcher", element: <DispatcherDashboardSection kpis={kpis} loading={loading} /> }];
      case "HeadOfFinance":
        return [{ key: "finance", element: <FinanceDashboardSection kpis={kpis} charts={charts} loading={loading} /> }];
      case "Driver":
        return [{ key: "driver", element: <DriverDashboardSection kpis={kpis} loading={loading} /> }];
      case "Customer":
        return [{ key: "customer", element: <CustomerDashboardSection kpis={kpis} charts={charts} loading={loading} /> }];
      case "InventoryOfficer":
        return [{ key: "inventory-officer", element: <InventoryOfficerDashboardSection kpis={kpis} charts={charts} loading={loading} /> }];
      case "CEO":
        return [{ key: "ceo", element: <CeoDashboardSection kpis={kpis} charts={charts} loading={loading} /> }];
      default:
        return [];
    }
  }, [dashboardRole, kpis, charts, loading]);

  return (
    <div className="space-y-8">
      <ToastHost toasts={toasts} />
      <PageHeader
        title={dashboardRole === "Dispatcher" ? "Dispatcher Dashboard" : "Dashboard"}
        description={dashboardRole === "Dispatcher" ? "Review active trips, assignment blockers, and Trip Chaining suggestions." : "Role-aware operations snapshot across your current responsibilities."}
        actions={
          <>
            <Badge variant="outline" className="text-[10px] animate-pulse">LIVE</Badge>
            <Button
              variant="outline"
              size="sm"
              onClick={() => loadKpis(true)}
              disabled={loading}
              className="gap-2"
            >
              <RefreshCw className={cn("h-4 w-4", loading && "animate-spin")} />
              Refresh
            </Button>
          </>
        }
      />

      {sections.length === 0 ? (
        <EmptyState title="No dashboard sections" description="Your current role has no dashboard widgets." />
      ) : (
        <div className="space-y-8">
          {sections.map((section) => (
            <div key={section.key} className="space-y-8">
              {section.element}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
