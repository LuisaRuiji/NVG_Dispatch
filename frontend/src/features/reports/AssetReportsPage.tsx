import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";
import AppNav from "@/components/AppNav";

type PagedResult<T> = {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
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

export default function AssetReportsPage() {
  const nav = useNavigate();
  const me = getMe();
  const { toasts, show } = useToast();

  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [summary, setSummary] = useState<PagedResult<AssetSummaryRow> | null>(null);
  const [selectedAsset, setSelectedAsset] = useState<{ id: string; code: string } | null>(null);
  const [breakdown, setBreakdown] = useState<AssetBreakdownRow[]>([]);
  const [loadingSummary, setLoadingSummary] = useState(false);
  const [loadingBreakdown, setLoadingBreakdown] = useState(false);

  const totalPages = useMemo(() => {
    if (!summary) return 1;
    return Math.max(1, Math.ceil(summary.totalCount / summary.pageSize));
  }, [summary]);

  const loadSummary = async (targetPage = page) => {
    try {
      setLoadingSummary(true);
      const result = await api<PagedResult<AssetSummaryRow>>(
        `/api/reports/asset-maintenance-cost?page=${targetPage}&pageSize=${pageSize}`,
        { method: "GET" }
      );
      setSummary(result);
      setPage(result.page);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load asset maintenance summary.", "error");
    } finally {
      setLoadingSummary(false);
    }
  };

  const loadBreakdown = async (assetId: string, assetCode: string) => {
    try {
      setLoadingBreakdown(true);
      setSelectedAsset({ id: assetId, code: assetCode });
      const result = await api<AssetBreakdownRow[]>(
        `/api/reports/asset-consumption?assetId=${encodeURIComponent(assetId)}`,
        { method: "GET" }
      );
      setBreakdown(result);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load asset breakdown.", "error");
    } finally {
      setLoadingBreakdown(false);
    }
  };

  useEffect(() => {
    if (!me) {
      nav("/login");
      return;
    }
    if (!me.roles?.includes("InventoryOfficer") && !me.roles?.includes("Manager")) {
      show("Access denied.", "error");
      return;
    }

    loadSummary(1);
  }, []);

  return (
    <div style={{ maxWidth: 1100, margin: "32px auto" }}>
      <ToastHost toasts={toasts} />
      <AppNav />
      <h1>Asset Reports</h1>

      <section style={{ marginTop: 24 }}>
        <h2>Asset Maintenance Summary</h2>
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead>
            <tr>
              <th style={{ textAlign: "left", borderBottom: "1px solid #ccc" }}>Asset</th>
              <th style={{ textAlign: "right", borderBottom: "1px solid #ccc" }}>Total Cost</th>
              <th style={{ textAlign: "right", borderBottom: "1px solid #ccc" }}>Items Used</th>
              <th style={{ textAlign: "right", borderBottom: "1px solid #ccc" }}>Request Count</th>
            </tr>
          </thead>
          <tbody>
            {summary?.items.map((row) => (
              <tr
                key={row.assetId}
                style={{ cursor: "pointer" }}
                onClick={() => loadBreakdown(row.assetId, row.assetCode)}
              >
                <td>{row.assetCode}</td>
                <td style={{ textAlign: "right" }}>{row.totalMaintenanceCost.toFixed(2)}</td>
                <td style={{ textAlign: "right" }}>{row.totalItemsConsumed}</td>
                <td style={{ textAlign: "right" }}>{row.requestCount}</td>
              </tr>
            ))}
            {summary?.items.length === 0 && !loadingSummary ? (
              <tr>
                <td colSpan={4} style={{ padding: "8px 0" }}>
                  No maintenance data found.
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>

        <div style={{ display: "flex", gap: 8, alignItems: "center", marginTop: 12 }}>
          <button onClick={() => loadSummary(Math.max(1, page - 1))} disabled={page <= 1 || loadingSummary}>
            Prev
          </button>
          <span>
            Page {page} of {totalPages}
          </span>
          <button
            onClick={() => loadSummary(Math.min(totalPages, page + 1))}
            disabled={page >= totalPages || loadingSummary}
          >
            Next
          </button>
        </div>
      </section>

      <section style={{ marginTop: 32 }}>
        <h2>Asset Consumption Breakdown</h2>
        {selectedAsset ? (
          <p style={{ marginTop: 4 }}>Asset: {selectedAsset.code}</p>
        ) : (
          <p style={{ marginTop: 4 }}>Select an asset to see breakdown.</p>
        )}
        <table style={{ width: "100%", borderCollapse: "collapse", marginTop: 8 }}>
          <thead>
            <tr>
              <th style={{ textAlign: "left", borderBottom: "1px solid #ccc" }}>Inventory</th>
              <th style={{ textAlign: "right", borderBottom: "1px solid #ccc" }}>Qty Used</th>
              <th style={{ textAlign: "right", borderBottom: "1px solid #ccc" }}>Total Cost</th>
            </tr>
          </thead>
          <tbody>
            {breakdown.map((row) => (
              <tr key={row.inventoryId}>
                <td>{row.inventoryName}</td>
                <td style={{ textAlign: "right" }}>{row.totalQuantityUsed}</td>
                <td style={{ textAlign: "right" }}>{row.totalCost.toFixed(2)}</td>
              </tr>
            ))}
            {selectedAsset && breakdown.length === 0 && !loadingBreakdown ? (
              <tr>
                <td colSpan={3} style={{ padding: "8px 0" }}>
                  No breakdown data found.
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </section>
    </div>
  );
}
