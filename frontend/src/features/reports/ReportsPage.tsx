import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api, downloadFile } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";

type PagedResult<T> = {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
};

type SupplierSpendRow = {
  basis: string;
  supplierId: string;
  supplierName: string;
  totalSpend: number;
  totalPurchaseOrders: number;
  totalLines: number;
  totalQty: number;
  averagePurchaseOrderValue: number;
};

type InventoryValuationRow = {
  inventoryId: string;
  inventoryName: string;
  quantity: number;
  averageCost: number;
  totalValue: number;
};

type AssetSummaryRow = {
  assetId: string;
  assetCode: string;
  totalItemsConsumed: number;
  totalMaintenanceCost: number;
  requestCount: number;
};

type AssetBreakdownRow = {
  inventoryId: string;
  inventoryName: string;
  totalQuantityUsed: number;
  totalCost: number;
};

type TabKey = "supplier" | "valuation" | "asset";

export default function ReportsPage() {
  const nav = useNavigate();
  const me = getMe();
  const { toasts, show } = useToast();

  const roles = me?.roles ?? [];
  const canSeeSupplierSpend =
    roles.includes("Manager") || roles.includes("HeadOfFinance") || roles.includes("CEO");
  const canSeeInventoryValuation = roles.includes("Manager") || roles.includes("InventoryOfficer");
  const canSeeAssetMaintenance = roles.includes("Manager") || roles.includes("InventoryOfficer");

  const availableTabs = useMemo(() => {
    const tabs: { key: TabKey; label: string }[] = [];
    if (canSeeSupplierSpend) tabs.push({ key: "supplier", label: "Supplier Spend" });
    if (canSeeInventoryValuation) tabs.push({ key: "valuation", label: "Inventory Valuation" });
    if (canSeeAssetMaintenance) tabs.push({ key: "asset", label: "Asset Maintenance" });
    return tabs;
  }, [canSeeAssetMaintenance, canSeeInventoryValuation, canSeeSupplierSpend]);

  const [activeTab, setActiveTab] = useState<TabKey>("supplier");

  useEffect(() => {
    if (availableTabs.length === 0) {
      return;
    }
    if (!availableTabs.some((tab) => tab.key === activeTab)) {
      setActiveTab(availableTabs[0].key);
    }
  }, [availableTabs, activeTab]);

  const [supplierBasis, setSupplierBasis] = useState<"ORDERED" | "RECEIVED">("ORDERED");
  const [supplierFrom, setSupplierFrom] = useState("");
  const [supplierTo, setSupplierTo] = useState("");
  const [supplierPage, setSupplierPage] = useState(1);
  const [supplierPageSize] = useState(20);
  const [supplierData, setSupplierData] = useState<PagedResult<SupplierSpendRow> | null>(null);
  const [supplierLoading, setSupplierLoading] = useState(false);

  const supplierTotalPages = useMemo(() => {
    if (!supplierData) return 1;
    return Math.max(1, Math.ceil(supplierData.totalCount / supplierData.pageSize));
  }, [supplierData]);

  const loadSupplierSpend = async (targetPage = 1) => {
    try {
      setSupplierLoading(true);
      const params = new URLSearchParams();
      params.set("basis", supplierBasis);
      params.set("page", targetPage.toString());
      params.set("pageSize", supplierPageSize.toString());
      if (supplierFrom) params.set("from", supplierFrom);
      if (supplierTo) params.set("to", supplierTo);

      const result = await api<PagedResult<SupplierSpendRow>>(
        `/api/reports/supplier-spend?${params.toString()}`,
        { method: "GET" }
      );
      setSupplierData(result);
      setSupplierPage(result.page);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load supplier spend report.", "error");
    } finally {
      setSupplierLoading(false);
    }
  };

  const exportSupplierSpendCsv = async () => {
    try {
      const params = new URLSearchParams();
      params.set("basis", supplierBasis);
      if (supplierFrom) params.set("from", supplierFrom);
      if (supplierTo) params.set("to", supplierTo);
      const suffix = supplierBasis.toLowerCase();
      await downloadFile(
        `/api/reports/supplier-spend.csv?${params.toString()}`,
        `supplier-spend-${suffix}.csv`
      );
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to export supplier spend CSV.", "error");
    }
  };

  const [valuationRows, setValuationRows] = useState<InventoryValuationRow[]>([]);
  const [valuationLoading, setValuationLoading] = useState(false);

  const loadInventoryValuation = async () => {
    try {
      setValuationLoading(true);
      const result = await api<InventoryValuationRow[]>("/api/reports/inventory-valuation", {
        method: "GET"
      });
      setValuationRows(result);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load inventory valuation.", "error");
    } finally {
      setValuationLoading(false);
    }
  };

  const exportInventoryValuationCsv = async () => {
    try {
      await downloadFile("/api/reports/inventory-valuation.csv", "inventory-valuation.csv");
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to export inventory valuation CSV.", "error");
    }
  };

  const [assetPage, setAssetPage] = useState(1);
  const [assetPageSize] = useState(20);
  const [assetSummary, setAssetSummary] = useState<PagedResult<AssetSummaryRow> | null>(null);
  const [assetLoading, setAssetLoading] = useState(false);
  const [selectedAsset, setSelectedAsset] = useState<{ id: string; code: string } | null>(null);
  const [assetBreakdown, setAssetBreakdown] = useState<AssetBreakdownRow[]>([]);
  const [assetBreakdownLoading, setAssetBreakdownLoading] = useState(false);

  const assetTotalPages = useMemo(() => {
    if (!assetSummary) return 1;
    return Math.max(1, Math.ceil(assetSummary.totalCount / assetSummary.pageSize));
  }, [assetSummary]);

  const loadAssetSummary = async (targetPage = 1) => {
    try {
      setAssetLoading(true);
      const result = await api<PagedResult<AssetSummaryRow>>(
        `/api/reports/asset-maintenance-cost?page=${targetPage}&pageSize=${assetPageSize}`,
        { method: "GET" }
      );
      setAssetSummary(result);
      setAssetPage(result.page);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load asset maintenance summary.", "error");
    } finally {
      setAssetLoading(false);
    }
  };

  const exportAssetMaintenanceCsv = async () => {
    try {
      await downloadFile("/api/reports/asset-maintenance-cost.csv", "asset-maintenance.csv");
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to export asset maintenance CSV.", "error");
    }
  };

  const loadAssetBreakdown = async (assetId: string, assetCode: string) => {
    try {
      setAssetBreakdownLoading(true);
      setSelectedAsset({ id: assetId, code: assetCode });
      const result = await api<AssetBreakdownRow[]>(
        `/api/reports/asset-consumption?assetId=${encodeURIComponent(assetId)}`,
        { method: "GET" }
      );
      setAssetBreakdown(result);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load asset breakdown.", "error");
    } finally {
      setAssetBreakdownLoading(false);
    }
  };

  useEffect(() => {
    if (!me) {
      nav("/login");
      return;
    }

    if (availableTabs.length === 0) {
      show("Access denied.", "error");
      return;
    }
  }, []);

  useEffect(() => {
    if (activeTab === "supplier" && canSeeSupplierSpend && !supplierData && !supplierLoading) {
      loadSupplierSpend(1);
    }
    if (activeTab === "valuation" && canSeeInventoryValuation && valuationRows.length === 0) {
      loadInventoryValuation();
    }
    if (activeTab === "asset" && canSeeAssetMaintenance && !assetSummary && !assetLoading) {
      loadAssetSummary(1);
    }
  }, [activeTab, canSeeAssetMaintenance, canSeeInventoryValuation, canSeeSupplierSpend]);

  return (
    <div>
      <ToastHost toasts={toasts} />
      <PageHeader title="Reports" description="Operational and financial reporting." />

      <div className="flex flex-wrap gap-2">
        {availableTabs.map((tab) => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            className={`rounded-lg border px-3 py-2 text-sm ${
              activeTab === tab.key
                ? "border-primary bg-primary/10 text-primary"
                : "border-border text-muted-foreground"
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === "supplier" && canSeeSupplierSpend ? (
        <section className="mt-6 space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 className="text-lg font-semibold">Supplier Spend</h2>
            <button onClick={exportSupplierSpendCsv} disabled={supplierLoading} className="rounded-lg border border-border px-3 py-2 text-sm">
              Export CSV
            </button>
          </div>
          <div className="flex flex-wrap items-center gap-4">
            <div className="flex items-center gap-2 text-sm">
              <label className="inline-flex items-center gap-2">
                <input
                  type="radio"
                  name="basis"
                  value="ORDERED"
                  checked={supplierBasis === "ORDERED"}
                  onChange={() => setSupplierBasis("ORDERED")}
                />
                ORDERED
              </label>
              <label className="inline-flex items-center gap-2">
                <input
                  type="radio"
                  name="basis"
                  value="RECEIVED"
                  checked={supplierBasis === "RECEIVED"}
                  onChange={() => setSupplierBasis("RECEIVED")}
                />
                RECEIVED
              </label>
            </div>
            <label className="text-sm text-muted-foreground">
              From:
              <input
                type="date"
                value={supplierFrom}
                onChange={(e) => setSupplierFrom(e.target.value)}
                className="ml-2 h-9 rounded-lg border border-border bg-white px-2 text-sm"
              />
            </label>
            <label className="text-sm text-muted-foreground">
              To:
              <input
                type="date"
                value={supplierTo}
                onChange={(e) => setSupplierTo(e.target.value)}
                className="ml-2 h-9 rounded-lg border border-border bg-white px-2 text-sm"
              />
            </label>
            <button onClick={() => loadSupplierSpend(1)} disabled={supplierLoading} className="rounded-lg bg-primary px-3 py-2 text-sm text-white">
              Apply
            </button>
          </div>

          {supplierLoading && !supplierData ? (
            <LoadingSkeleton rows={5} />
          ) : (
            <DataTable>
              <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left">Supplier</th>
                  <th className="px-4 py-3 text-right">Total Spend</th>
                  <th className="px-4 py-3 text-right">POs</th>
                  <th className="px-4 py-3 text-right">Lines</th>
                  <th className="px-4 py-3 text-right">Total Qty</th>
                  <th className="px-4 py-3 text-right">Avg PO Value</th>
                </tr>
              </thead>
              <tbody>
                {supplierData?.items.map((row) => (
                  <tr key={row.supplierId} className="border-t border-border">
                    <td className="px-4 py-3 text-sm">{row.supplierName}</td>
                    <td className="px-4 py-3 text-right text-sm">{row.totalSpend.toFixed(2)}</td>
                    <td className="px-4 py-3 text-right text-sm">{row.totalPurchaseOrders}</td>
                    <td className="px-4 py-3 text-right text-sm">{row.totalLines}</td>
                    <td className="px-4 py-3 text-right text-sm">{row.totalQty}</td>
                    <td className="px-4 py-3 text-right text-sm">{row.averagePurchaseOrderValue.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </DataTable>
          )}

          {supplierData?.items.length === 0 && !supplierLoading ? (
            <EmptyState title="No supplier spend data." description="Adjust filters or basis." />
          ) : null}

          <div className="flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
            <button
              onClick={() => loadSupplierSpend(Math.max(1, supplierPage - 1))}
              disabled={supplierPage <= 1 || supplierLoading}
              className="rounded-lg border border-border px-3 py-2 text-sm text-foreground"
            >
              Prev
            </button>
            <span>
              Page {supplierPage} of {supplierTotalPages}
            </span>
            <button
              onClick={() => loadSupplierSpend(Math.min(supplierTotalPages, supplierPage + 1))}
              disabled={supplierPage >= supplierTotalPages || supplierLoading}
              className="rounded-lg border border-border px-3 py-2 text-sm text-foreground"
            >
              Next
            </button>
          </div>
        </section>
      ) : null}

      {activeTab === "valuation" && canSeeInventoryValuation ? (
        <section className="mt-6 space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 className="text-lg font-semibold">Inventory Valuation</h2>
            <button onClick={exportInventoryValuationCsv} disabled={valuationLoading} className="rounded-lg border border-border px-3 py-2 text-sm">
              Export CSV
            </button>
          </div>
          {valuationLoading && valuationRows.length === 0 ? (
            <LoadingSkeleton rows={5} />
          ) : (
            <DataTable>
              <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left">Item</th>
                  <th className="px-4 py-3 text-right">Qty</th>
                  <th className="px-4 py-3 text-right">Avg Cost</th>
                  <th className="px-4 py-3 text-right">Total Value</th>
                </tr>
              </thead>
              <tbody>
                {valuationRows.map((row) => (
                  <tr key={row.inventoryId} className="border-t border-border">
                    <td className="px-4 py-3 text-sm">{row.inventoryName}</td>
                    <td className="px-4 py-3 text-right text-sm">{row.quantity}</td>
                    <td className="px-4 py-3 text-right text-sm">{row.averageCost.toFixed(2)}</td>
                    <td className="px-4 py-3 text-right text-sm">{row.totalValue.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </DataTable>
          )}
          {valuationRows.length === 0 && !valuationLoading ? (
            <EmptyState title="No valuation data." description="Inventory valuation will appear here." />
          ) : null}
        </section>
      ) : null}

      {activeTab === "asset" && canSeeAssetMaintenance ? (
        <section className="mt-6 space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 className="text-lg font-semibold">Asset Maintenance</h2>
            <button onClick={exportAssetMaintenanceCsv} disabled={assetLoading} className="rounded-lg border border-border px-3 py-2 text-sm">
              Export CSV
            </button>
          </div>

          {assetLoading && !assetSummary ? (
            <LoadingSkeleton rows={5} />
          ) : (
            <DataTable>
              <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left">Asset</th>
                  <th className="px-4 py-3 text-right">Total Cost</th>
                  <th className="px-4 py-3 text-right">Items Used</th>
                  <th className="px-4 py-3 text-right">Request Count</th>
                </tr>
              </thead>
              <tbody>
                {assetSummary?.items.map((row) => (
                  <tr
                    key={row.assetId}
                    className="cursor-pointer border-t border-border hover:bg-muted/40"
                    onClick={() => loadAssetBreakdown(row.assetId, row.assetCode)}
                  >
                    <td className="px-4 py-3 text-sm">{row.assetCode}</td>
                    <td className="px-4 py-3 text-right text-sm">{row.totalMaintenanceCost.toFixed(2)}</td>
                    <td className="px-4 py-3 text-right text-sm">{row.totalItemsConsumed}</td>
                    <td className="px-4 py-3 text-right text-sm">{row.requestCount}</td>
                  </tr>
                ))}
              </tbody>
            </DataTable>
          )}

          {assetSummary?.items.length === 0 && !assetLoading ? (
            <EmptyState title="No asset maintenance data." description="Maintenance costs will appear here." />
          ) : null}

          <div className="flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
            <button
              onClick={() => loadAssetSummary(Math.max(1, assetPage - 1))}
              disabled={assetPage <= 1}
              className="rounded-lg border border-border px-3 py-2 text-sm text-foreground"
            >
              Prev
            </button>
            <span>
              Page {assetPage} of {assetTotalPages}
            </span>
            <button
              onClick={() => loadAssetSummary(Math.min(assetTotalPages, assetPage + 1))}
              disabled={assetPage >= assetTotalPages}
              className="rounded-lg border border-border px-3 py-2 text-sm text-foreground"
            >
              Next
            </button>
          </div>

          <div>
            <h3 className="text-lg font-semibold">Consumption Breakdown</h3>
            {selectedAsset ? (
              <p className="mt-1 text-sm text-muted-foreground">Asset: {selectedAsset.code}</p>
            ) : (
              <p className="mt-1 text-sm text-muted-foreground">Select an asset to view consumption.</p>
            )}
            {assetBreakdownLoading && selectedAsset ? (
              <LoadingSkeleton rows={4} />
            ) : (
              <DataTable className="mt-3">
                <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="px-4 py-3 text-left">Inventory</th>
                    <th className="px-4 py-3 text-right">Qty Used</th>
                    <th className="px-4 py-3 text-right">Total Cost</th>
                  </tr>
                </thead>
                <tbody>
                  {assetBreakdown.map((row) => (
                    <tr key={row.inventoryId} className="border-t border-border">
                      <td className="px-4 py-3 text-sm">{row.inventoryName}</td>
                      <td className="px-4 py-3 text-right text-sm">{row.totalQuantityUsed}</td>
                      <td className="px-4 py-3 text-right text-sm">{row.totalCost.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </DataTable>
            )}

            {selectedAsset && assetBreakdown.length === 0 && !assetBreakdownLoading ? (
              <EmptyState title="No breakdown data." description="No consumption recorded." />
            ) : null}
          </div>
        </section>
      ) : null}
    </div>
  );
}
