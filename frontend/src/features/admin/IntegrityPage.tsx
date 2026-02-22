import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";

type IntegritySummary = {
  maintenanceWithoutAsset: number;
  loansOverReturned: number;
  poOverReceived: number;
  negativeInventoryCount: number;
  requestsIssuedWithoutStockLogs: number;
  duplicateIssueLogs: number;
  orphanStockLogs: number;
  orphanApprovalActions: number;
  poWithoutWorkflow: number;
  adjustmentWithoutLogs: number;
  supplierActionsWithoutAudit: number;
  poReceiptsWithoutAudit: number;
  loanReturnsWithoutAudit: number;
  adjustmentsWithoutAudit: number;
  inactiveSuppliersReferenced: number;
  inactiveInventoryReferenced: number;
};

export default function IntegrityPage() {
  const nav = useNavigate();
  const me = getMe();
  const { toasts, show } = useToast();
  const [summary, setSummary] = useState<IntegritySummary | null>(null);
  const [loading, setLoading] = useState(false);

  const canAccess =
    me?.roles?.includes("Manager") ||
    me?.roles?.includes("HeadOfFinance") ||
    me?.roles?.includes("CEO");

  const loadIntegrity = async () => {
    try {
      setLoading(true);
      const result = await api<IntegritySummary>("/api/reports/integrity", { method: "GET" });
      setSummary(result);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load integrity report.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!me) {
      nav("/login");
      return;
    }
    if (!canAccess) {
      show("Access denied.", "error");
      return;
    }

    loadIntegrity();
  }, []);

  const renderRow = (label: string, value: number) => {
    const isBad = value > 0;
    return (
      <tr key={label} className="border-t border-border">
        <td className={`px-4 py-3 text-sm ${isBad ? "text-red-600" : ""}`}>{label}</td>
        <td className={`px-4 py-3 text-right text-sm ${isBad ? "text-red-600" : ""}`}>{value}</td>
      </tr>
    );
  };

  return (
    <div>
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Integrity Check"
        description="System-wide safety alarms. All values should be zero."
        actions={
          <button onClick={loadIntegrity} disabled={loading} className="rounded-lg border border-border px-3 py-2 text-sm">
            Refresh
          </button>
        }
      />

      {loading && !summary ? <LoadingSkeleton rows={6} /> : null}

      {summary ? (
        <DataTable>
          <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left">Check</th>
              <th className="px-4 py-3 text-right">Count</th>
            </tr>
          </thead>
          <tbody>
            {renderRow("Maintenance without asset", summary.maintenanceWithoutAsset)}
            {renderRow("Loans over returned", summary.loansOverReturned)}
            {renderRow("POs over received", summary.poOverReceived)}
            {renderRow("Negative inventory count", summary.negativeInventoryCount)}
            {renderRow("Requests issued without stock logs", summary.requestsIssuedWithoutStockLogs)}
            {renderRow("Duplicate issue logs", summary.duplicateIssueLogs)}
            {renderRow("Orphan stock logs", summary.orphanStockLogs)}
            {renderRow("Orphan approval actions", summary.orphanApprovalActions)}
            {renderRow("POs without workflow", summary.poWithoutWorkflow)}
            {renderRow("Adjustments without logs", summary.adjustmentWithoutLogs)}
            {renderRow("Supplier actions without audit", summary.supplierActionsWithoutAudit)}
            {renderRow("PO receipts without audit", summary.poReceiptsWithoutAudit)}
            {renderRow("Loan returns without audit", summary.loanReturnsWithoutAudit)}
            {renderRow("Adjustments without audit", summary.adjustmentsWithoutAudit)}
            {renderRow("Inactive suppliers referenced", summary.inactiveSuppliersReferenced)}
            {renderRow("Inactive inventory referenced", summary.inactiveInventoryReferenced)}
          </tbody>
        </DataTable>
      ) : (
        <EmptyState title="No integrity data yet." description="Run a refresh to pull metrics." />
      )}
    </div>
  );
}
