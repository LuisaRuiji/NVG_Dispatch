import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { getMe } from "@/features/auth/authStore";
import PageHeader from "@/components/PageHeader";
import KpiCard from "@/components/KpiCard";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import { useToast } from "@/lib/useToast";
import { fetchDashboardKpis } from "./kpis";

export default function DashboardPage() {
  const me = getMe();
  const { toasts, show } = useToast();
  const roles = me?.roles ?? [];

  const [kpis, setKpis] = useState({
    pendingIoCount: null,
    pendingManagerCount: null,
    awaitingIssueCount: null,
    openLoansCount: null,
    lowStockCount: null,
    pendingFinancePoCount: null,
    pendingCeoPoCount: null,
    myRequestsCount: null
  });

  const hasAnyKpi =
    roles.includes("InventoryOfficer") ||
    roles.includes("Manager") ||
    roles.includes("HeadOfFinance") ||
    roles.includes("CEO") ||
    roles.includes("Driver");

  const loadKpis = async (force = false) => {
    if (!me) return;
    try {
      const result = await fetchDashboardKpis(me, force);
      setKpis(result);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load dashboard metrics.", "error");
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

  const quickActions = useMemo(() => {
    const actions: { label: string; to: string }[] = [];
    if (roles.includes("Driver")) {
      actions.push({ label: "New Maintenance Issue", to: "/requests/new/maintenance-issue" });
      actions.push({ label: "New Borrow Request", to: "/requests/new/borrow" });
    }
    if (roles.includes("InventoryOfficer")) {
      actions.push({ label: "Create Purchase Order", to: "/purchase-orders" });
    }
    return actions;
  }, [roles]);

  return (
    <div className="space-y-8">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Dashboard"
        description="Operational snapshot across requests, stock, and loans."
        actions={
          <button
            className="rounded-lg border border-border px-3 py-2 text-sm"
            onClick={() => loadKpis(true)}
          >
            Refresh
          </button>
        }
      />

      {hasAnyKpi ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          {roles.includes("InventoryOfficer") ? (
            <>
              <KpiCard title="Pending IO" value={kpis.pendingIoCount} subtitle="Requests awaiting IO review" />
              <KpiCard title="Awaiting Issue" value={kpis.awaitingIssueCount} subtitle="Approved requests" />
            </>
          ) : null}
          {roles.includes("Manager") ? (
            <KpiCard title="Pending Manager" value={kpis.pendingManagerCount} subtitle="Requests awaiting decision" />
          ) : null}
          {(roles.includes("InventoryOfficer") || roles.includes("Manager")) ? (
            <>
              <KpiCard title="Open Loans" value={kpis.openLoansCount} subtitle="Borrowed items open" />
              <KpiCard title="Low Stock" value={kpis.lowStockCount} subtitle="Items below reorder level" />
            </>
          ) : null}
          {roles.includes("HeadOfFinance") ? (
            <KpiCard title="Pending Finance" value={kpis.pendingFinancePoCount} subtitle="POs awaiting finance" />
          ) : null}
          {roles.includes("CEO") ? (
            <KpiCard title="Pending CEO" value={kpis.pendingCeoPoCount} subtitle="POs awaiting CEO" />
          ) : null}
          {roles.includes("Driver") ? (
            <KpiCard title="My Requests" value={kpis.myRequestsCount} subtitle="Requests submitted" />
          ) : null}
        </div>
      ) : (
        <EmptyState title="No operational KPIs available." description="Dashboard widgets are role-based." />
      )}

      <div className="grid gap-4 lg:grid-cols-[1.4fr_1fr]">
        <div className="surface-card p-6">
          <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">Quick Actions</p>
          <div className="mt-4 grid gap-3">
            {quickActions.length === 0 ? (
              <EmptyState title="No quick actions" description="Your role has no quick actions." />
            ) : (
              quickActions.map((action) => (
                <Link
                  key={action.to}
                  to={action.to}
                  className="flex items-center justify-between rounded-lg border border-border px-4 py-3 text-sm font-medium text-foreground hover:bg-muted"
                >
                  {action.label}
                  <span className="text-muted-foreground">→</span>
                </Link>
              ))
            )}
          </div>
        </div>

        <div className="surface-card p-6">
          <div className="mb-3 flex items-center justify-between">
            <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
              Status Summary
            </p>
            <span className="text-xs text-muted-foreground">Live</span>
          </div>
          <div className="space-y-3 text-sm text-muted-foreground">
            <div className="flex items-center justify-between">
              <span>IO Queue</span>
              <span className="font-semibold text-foreground">{kpis.pendingIoCount ?? "—"}</span>
            </div>
            <div className="flex items-center justify-between">
              <span>Manager Queue</span>
              <span className="font-semibold text-foreground">{kpis.pendingManagerCount ?? "—"}</span>
            </div>
            <div className="flex items-center justify-between">
              <span>Open Loans</span>
              <span className="font-semibold text-foreground">{kpis.openLoansCount ?? "—"}</span>
            </div>
            <div className="flex items-center justify-between">
              <span>Low Stock</span>
              <span className="font-semibold text-foreground">{kpis.lowStockCount ?? "—"}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
