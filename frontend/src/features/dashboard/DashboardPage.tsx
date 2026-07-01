import { useEffect, useMemo, useState } from "react";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import EmptyState from "@/components/EmptyState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { cn } from "@/lib/utils";
import { useToast } from "@/lib/useToast";
import { getMe } from "@/features/auth/authStore";
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
  const roles = me?.roles ?? [];
  const { toasts, show } = useToast();
  const [loading, setLoading] = useState(false);
  const [kpis, setKpis] = useState<DashboardKpis>(emptyKpis);
  const [charts, setCharts] = useState<DashboardCharts>(emptyDashboardCharts);

  const loadKpis = async (force = false) => {
    if (!me) return;
    try {
      setLoading(true);
      const [result, chartResult] = await Promise.all([
        fetchDashboardKpis(me, force),
        fetchDashboardCharts(me, force)
      ]);
      setKpis(result);
      setCharts(chartResult);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load dashboard metrics.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadKpis();
  }, [me?.userId]);

  useEffect(() => {
    const handler = () => loadKpis(true);
    window.addEventListener("nvg:kpi-invalidated", handler);
    return () => window.removeEventListener("nvg:kpi-invalidated", handler);
  }, [me?.userId]);

  const sections = useMemo(() => {
    const visibleSections: { key: string; element: JSX.Element }[] = [];

    if (roles.includes("SuperAdmin") || roles.includes("Admin")) {
      visibleSections.push({
        key: "admin",
        element: <AdminDashboardSection kpis={kpis} charts={charts} loading={loading} />
      });
    }

    if (roles.includes("Manager")) {
      visibleSections.push({
        key: "manager",
        element: <ManagerDashboardSection kpis={kpis} charts={charts} loading={loading} />
      });
    }

    if (roles.includes("Dispatcher")) {
      visibleSections.push({
        key: "dispatcher",
        element: <DispatcherDashboardSection kpis={kpis} charts={charts} loading={loading} />
      });
    }

    if (roles.includes("HeadOfFinance")) {
      visibleSections.push({
        key: "finance",
        element: <FinanceDashboardSection kpis={kpis} charts={charts} loading={loading} />
      });
    }

    if (roles.includes("Driver")) {
      visibleSections.push({
        key: "driver",
        element: <DriverDashboardSection kpis={kpis} loading={loading} />
      });
    }

    if (roles.includes("Customer")) {
      visibleSections.push({
        key: "customer",
        element: <CustomerDashboardSection kpis={kpis} charts={charts} loading={loading} />
      });
    }

    if (roles.includes("InventoryOfficer")) {
      visibleSections.push({
        key: "inventory-officer",
        element: <InventoryOfficerDashboardSection kpis={kpis} charts={charts} loading={loading} />
      });
    }

    if (roles.includes("CEO")) {
      visibleSections.push({
        key: "ceo",
        element: <CeoDashboardSection kpis={kpis} charts={charts} loading={loading} />
      });
    }

    return visibleSections;
  }, [roles, kpis, charts, loading]);

  return (
    <div className="space-y-8">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Dashboard"
        description="Role-aware operations snapshot across dispatch, documents, finance, customers, and inventory."
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
          {sections.map((section, index) => (
            <div key={section.key} className="space-y-8">
              {index > 0 ? <Separator /> : null}
              {section.element}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
