import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Download, RefreshCw } from "lucide-react";
import { api, downloadFile } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import KpiCard from "@/components/KpiCard";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import RecommendationAcceptanceBar from "./RecommendationAcceptanceBar";

type PagedResult<T> = {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
};

type TripStatusCountRow = {
  status: string;
  count: number;
};

type TripWeeklyCountRow = {
  weekStart: string;
  count: number;
};

type TripSummaryReport = {
  statusCounts: TripStatusCountRow[];
  weeklyCounts: TripWeeklyCountRow[];
  deliveredTrips: number;
  totalNonCancelledTrips: number;
  completionRatePercent: number;
};

type DriverPerformanceRow = {
  driverUserId: string;
  driverName: string;
  tripsCompleted: number;
  averageDeliveryMinutes?: number | null;
  onTimeRatePercent: number;
  documentComplianceRatePercent: number;
};

type DeliveryTimeRouteRow = {
  routeKey: string;
  fromLocation: string;
  toLocation: string;
  tripCount: number;
  averageDeliveryMinutes: number;
  longestDeliveryMinutes: number;
  generatedAt: string;
};

type DeliveryTimeReport = {
  routes: PagedResult<DeliveryTimeRouteRow>;
  longestRoutes: DeliveryTimeRouteRow[];
  generatedAt: string;
};

type DocumentProcessingRow = {
  documentType: string;
  pendingVerification: number;
  averageVerificationHours?: number | null;
  rejectionRatePercent: number;
};

type FinancialPeriodRow = {
  periodStart: string;
  periodLabel: string;
  totalTripRevenue: number;
  totalDriverPayroll: number;
  totalFuelCost: number;
};

type DriverPayrollRow = {
  driverUserId?: string | null;
  driverName: string;
  deliveredTrips: number;
  totalPayroll: number;
};

type FinancialSummaryReport = {
  periods: FinancialPeriodRow[];
  driverPayroll: DriverPayrollRow[];
};

type RecommendationKpis = {
  totalGenerated: number;
  totalAccepted: number;
  totalIgnored: number;
  acceptanceRatePercent: number;
  averageAcceptedScore?: number | null;
};

type RecommendationWeeklyRow = {
  weekStart: string;
  accepted: number;
  ignored: number;
};

type RecommendationHistoryRow = {
  date: string;
  driver: string;
  completedTripId: string;
  recommendedTripId: string;
  score: number;
  rank: number;
  action: string;
  reviewedBy?: string | null;
};

type RecommendationReport = {
  kpis: RecommendationKpis;
  weekly: RecommendationWeeklyRow[];
  recentHistory: PagedResult<RecommendationHistoryRow>;
};

type TabKey =
  | "trip-summary"
  | "driver-performance"
  | "delivery-time"
  | "document-processing"
  | "financial-summary"
  | "recommendations";

const pageSize = 20;

export default function DispatchReportsPage() {
  const nav = useNavigate();
  const me = getMe();
  const roles = me?.roles ?? [];
  const { toasts, show } = useToast();

  const isAdmin = roles.includes("Admin") || roles.includes("SuperAdmin");
  const canSeeTripSummary =
    isAdmin || roles.includes("Dispatcher") || roles.includes("Manager") || roles.includes("CEO");
  const canSeeManagerReports = isAdmin || roles.includes("Manager") || roles.includes("CEO");
  const canSeeDocumentProcessing = canSeeManagerReports || roles.includes("Dispatcher");
  const canSeeFinancialSummary =
    isAdmin || roles.includes("HeadOfFinance") || roles.includes("Manager") || roles.includes("CEO");
  const canSeeRecommendations = canSeeManagerReports;

  const availableTabs = useMemo(() => {
    const tabs: { key: TabKey; label: string }[] = [];
    if (canSeeTripSummary) tabs.push({ key: "trip-summary", label: "Trip Summary" });
    if (canSeeManagerReports) tabs.push({ key: "driver-performance", label: "Driver Performance" });
    if (canSeeManagerReports) tabs.push({ key: "delivery-time", label: "Delivery Time Analysis" });
    if (canSeeDocumentProcessing) tabs.push({ key: "document-processing", label: "Document Processing" });
    if (canSeeFinancialSummary) tabs.push({ key: "financial-summary", label: "Financial Summary" });
    if (canSeeRecommendations) tabs.push({ key: "recommendations", label: "Trip Chaining" });
    return tabs;
  }, [canSeeDocumentProcessing, canSeeFinancialSummary, canSeeManagerReports, canSeeRecommendations, canSeeTripSummary]);

  const [activeTab, setActiveTab] = useState<TabKey>(() => availableTabs[0]?.key ?? "trip-summary");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [financialGroupBy, setFinancialGroupBy] = useState<"week" | "month">("week");

  const [tripSummary, setTripSummary] = useState<TripSummaryReport | null>(null);
  const [tripSummaryLoading, setTripSummaryLoading] = useState(false);

  const [driverPage, setDriverPage] = useState(1);
  const [driverPerformance, setDriverPerformance] = useState<PagedResult<DriverPerformanceRow> | null>(null);
  const [driverLoading, setDriverLoading] = useState(false);

  const [deliveryPage, setDeliveryPage] = useState(1);
  const [deliveryTime, setDeliveryTime] = useState<DeliveryTimeReport | null>(null);
  const [deliveryLoading, setDeliveryLoading] = useState(false);

  const [documentProcessing, setDocumentProcessing] = useState<DocumentProcessingRow[]>([]);
  const [documentLoading, setDocumentLoading] = useState(false);

  const [financialSummary, setFinancialSummary] = useState<FinancialSummaryReport | null>(null);
  const [financialLoading, setFinancialLoading] = useState(false);

  const [recommendationPage, setRecommendationPage] = useState(1);
  const [recommendationReport, setRecommendationReport] = useState<RecommendationReport | null>(null);
  const [recommendationLoading, setRecommendationLoading] = useState(false);

  const driverTotalPages = useMemo(() => {
    if (!driverPerformance) return 1;
    return Math.max(1, Math.ceil(driverPerformance.totalCount / driverPerformance.pageSize));
  }, [driverPerformance]);

  const deliveryTotalPages = useMemo(() => {
    if (!deliveryTime) return 1;
    return Math.max(1, Math.ceil(deliveryTime.routes.totalCount / deliveryTime.routes.pageSize));
  }, [deliveryTime]);

  const recommendationTotalPages = useMemo(() => {
    if (!recommendationReport) return 1;
    return Math.max(1, Math.ceil(recommendationReport.recentHistory.totalCount / recommendationReport.recentHistory.pageSize));
  }, [recommendationReport]);

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
    if (availableTabs.length === 0) return;
    if (!availableTabs.some((tab) => tab.key === activeTab)) {
      setActiveTab(availableTabs[0].key);
    }
  }, [availableTabs, activeTab]);

  useEffect(() => {
    if (!availableTabs.some((tab) => tab.key === activeTab)) return;
    loadActiveTab(1);
  }, [activeTab, availableTabs]);

  function buildQuery(extra?: Record<string, string | number>) {
    const params = new URLSearchParams();
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    Object.entries(extra ?? {}).forEach(([key, value]) => params.set(key, value.toString()));
    const query = params.toString();
    return query ? `?${query}` : "";
  }

  async function loadTripSummary() {
    try {
      setTripSummaryLoading(true);
      const result = await api<TripSummaryReport>(
        `/api/reports/dispatch/trip-summary${buildQuery()}`,
        { method: "GET" }
      );
      setTripSummary(result);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load trip summary.", "error");
    } finally {
      setTripSummaryLoading(false);
    }
  }

  async function loadDriverPerformance(targetPage = 1) {
    try {
      setDriverLoading(true);
      const result = await api<PagedResult<DriverPerformanceRow>>(
        `/api/reports/dispatch/driver-performance${buildQuery({ page: targetPage, pageSize })}`,
        { method: "GET" }
      );
      setDriverPerformance(result);
      setDriverPage(result.page);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load driver performance.", "error");
    } finally {
      setDriverLoading(false);
    }
  }

  async function loadDeliveryTime(targetPage = 1) {
    try {
      setDeliveryLoading(true);
      const result = await api<DeliveryTimeReport>(
        `/api/reports/dispatch/delivery-time${buildQuery({ page: targetPage, pageSize })}`,
        { method: "GET" }
      );
      setDeliveryTime(result);
      setDeliveryPage(result.routes.page);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load delivery time analysis.", "error");
    } finally {
      setDeliveryLoading(false);
    }
  }

  async function loadDocumentProcessing() {
    try {
      setDocumentLoading(true);
      const result = await api<DocumentProcessingRow[]>(
        `/api/reports/dispatch/document-processing${buildQuery()}`,
        { method: "GET" }
      );
      setDocumentProcessing(result);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load document processing.", "error");
    } finally {
      setDocumentLoading(false);
    }
  }

  async function loadFinancialSummary() {
    try {
      setFinancialLoading(true);
      const result = await api<FinancialSummaryReport>(
        `/api/reports/dispatch/financial-summary${buildQuery({ groupBy: financialGroupBy })}`,
        { method: "GET" }
      );
      setFinancialSummary(result);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load financial summary.", "error");
    } finally {
      setFinancialLoading(false);
    }
  }

  async function loadRecommendations(targetPage = 1) {
    try {
      setRecommendationLoading(true);
      const result = await api<RecommendationReport>(
        `/api/reports/dispatch/recommendations${buildQuery({ page: targetPage, pageSize })}`,
        { method: "GET" }
      );
      setRecommendationReport(result);
      setRecommendationPage(result.recentHistory.page);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load Trip Chaining history.", "error");
    } finally {
      setRecommendationLoading(false);
    }
  }

  function loadActiveTab(targetPage = 1) {
    if (activeTab === "trip-summary") void loadTripSummary();
    if (activeTab === "driver-performance") void loadDriverPerformance(targetPage);
    if (activeTab === "delivery-time") void loadDeliveryTime(targetPage);
    if (activeTab === "document-processing") void loadDocumentProcessing();
    if (activeTab === "financial-summary") void loadFinancialSummary();
    if (activeTab === "recommendations") void loadRecommendations(targetPage);
  }

  async function exportCsv(path: string, filename: string) {
    try {
      await downloadFile(`${path}${buildQuery(activeTab === "financial-summary" ? { groupBy: financialGroupBy } : undefined)}`, filename);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to export CSV.", "error");
    }
  }

  const activeLoading =
    (activeTab === "trip-summary" && tripSummaryLoading) ||
    (activeTab === "driver-performance" && driverLoading) ||
    (activeTab === "delivery-time" && deliveryLoading) ||
    (activeTab === "document-processing" && documentLoading) ||
    (activeTab === "financial-summary" && financialLoading) ||
    (activeTab === "recommendations" && recommendationLoading);

  return (
    <div>
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Dispatch Reports"
        description="Trip throughput, driver performance, document processing, and financial summaries."
        actions={
          <Button variant="outline" size="sm" onClick={() => loadActiveTab(1)} disabled={activeLoading} className="gap-2">
            <RefreshCw className={cn("h-4 w-4", activeLoading && "animate-spin")} />
            Refresh
          </Button>
        }
      />

      {availableTabs.length === 0 ? (
        <EmptyState title="No dispatch reports available." description="Your role has no dispatch report access." />
      ) : (
        <>
          <div className="flex flex-wrap gap-2">
            {availableTabs.map((tab) => (
              <button
                key={tab.key}
                onClick={() => setActiveTab(tab.key)}
                className={cn(
                  "rounded-lg border px-3 py-2 text-sm transition-colors",
                  activeTab === tab.key
                    ? "border-primary bg-primary/10 text-primary"
                    : "border-border text-muted-foreground hover:bg-muted/50 hover:text-foreground"
                )}
              >
                {tab.label}
              </button>
            ))}
          </div>

          <div className="mt-6 surface-card p-4">
            <div className="flex flex-wrap items-end gap-3">
              <label className="text-sm text-muted-foreground">
                From
                <input
                  type="date"
                  value={from}
                  onChange={(e) => setFrom(e.target.value)}
                  className="mt-1 block h-9 rounded-lg border border-border bg-white px-2 text-sm text-foreground"
                />
              </label>
              <label className="text-sm text-muted-foreground">
                To
                <input
                  type="date"
                  value={to}
                  onChange={(e) => setTo(e.target.value)}
                  className="mt-1 block h-9 rounded-lg border border-border bg-white px-2 text-sm text-foreground"
                />
              </label>
              {activeTab === "financial-summary" ? (
                <label className="text-sm text-muted-foreground">
                  Group
                  <select
                    value={financialGroupBy}
                    onChange={(e) => setFinancialGroupBy(e.target.value as "week" | "month")}
                    className="mt-1 block h-9 rounded-lg border border-border bg-white px-2 text-sm text-foreground"
                  >
                    <option value="week">Week</option>
                    <option value="month">Month</option>
                  </select>
                </label>
              ) : null}
              <Button size="sm" onClick={() => loadActiveTab(1)} disabled={activeLoading}>
                Apply
              </Button>
            </div>
          </div>

          {activeTab === "trip-summary" && canSeeTripSummary ? (
            <TripSummaryTab
              report={tripSummary}
              loading={tripSummaryLoading}
              onExport={() => exportCsv("/api/reports/dispatch/trip-summary.csv", "dispatch-trip-summary.csv")}
            />
          ) : null}

          {activeTab === "driver-performance" && canSeeManagerReports ? (
            <DriverPerformanceTab
              rows={driverPerformance}
              loading={driverLoading}
              page={driverPage}
              totalPages={driverTotalPages}
              onPage={loadDriverPerformance}
              onExport={() => exportCsv("/api/reports/dispatch/driver-performance.csv", "dispatch-driver-performance.csv")}
            />
          ) : null}

          {activeTab === "delivery-time" && canSeeManagerReports ? (
            <DeliveryTimeTab
              report={deliveryTime}
              loading={deliveryLoading}
              page={deliveryPage}
              totalPages={deliveryTotalPages}
              onPage={loadDeliveryTime}
              onExport={() => exportCsv("/api/reports/dispatch/delivery-time.csv", "dispatch-delivery-time.csv")}
            />
          ) : null}

          {activeTab === "document-processing" && canSeeDocumentProcessing ? (
            <DocumentProcessingTab
              rows={documentProcessing}
              loading={documentLoading}
              onExport={() => exportCsv("/api/reports/dispatch/document-processing.csv", "dispatch-document-processing.csv")}
            />
          ) : null}

          {activeTab === "financial-summary" && canSeeFinancialSummary ? (
            <FinancialSummaryTab
              report={financialSummary}
              loading={financialLoading}
              onExport={() => exportCsv("/api/reports/dispatch/financial-summary.csv", "dispatch-financial-summary.csv")}
            />
          ) : null}

          {activeTab === "recommendations" && canSeeRecommendations ? (
            <RecommendationsTab
              report={recommendationReport}
              loading={recommendationLoading}
              page={recommendationPage}
              totalPages={recommendationTotalPages}
              onPage={loadRecommendations}
              onExport={() => exportCsv("/api/reports/dispatch/recommendations.csv", "trip-chaining-history.csv")}
            />
          ) : null}
        </>
      )}
    </div>
  );
}

function TripSummaryTab({
  report,
  loading,
  onExport
}: {
  report: TripSummaryReport | null;
  loading: boolean;
  onExport: () => void;
}) {
  return (
    <section className="mt-6 space-y-6">
      <SectionHeader title="Trip Summary" onExport={onExport} loading={loading} />
      {loading && !report ? (
        <LoadingSkeleton rows={5} />
      ) : !report ? (
        <EmptyState title="No trip summary data." />
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-3">
            <SummaryTile label="Delivered" value={report.deliveredTrips} />
            <SummaryTile label="Non-cancelled" value={report.totalNonCancelledTrips} />
            <SummaryTile label="Completion Rate" value={`${formatNumber(report.completionRatePercent)}%`} />
          </div>

          <div className="grid gap-6 xl:grid-cols-2">
            <div className="space-y-3">
              <h2 className="text-lg font-semibold">Trips by Status</h2>
              <DataTable>
                <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="px-4 py-3 text-left">Status</th>
                    <th className="px-4 py-3 text-right">Count</th>
                  </tr>
                </thead>
                <tbody>
                  {report.statusCounts.map((row) => (
                    <tr key={row.status} className="border-t border-border">
                      <td className="px-4 py-3 text-sm">{row.status}</td>
                      <td className="px-4 py-3 text-right text-sm">{row.count}</td>
                    </tr>
                  ))}
                </tbody>
              </DataTable>
            </div>

            <div className="space-y-3">
              <h2 className="text-lg font-semibold">Trips by Week</h2>
              <DataTable>
                <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="px-4 py-3 text-left">Week</th>
                    <th className="px-4 py-3 text-right">Count</th>
                  </tr>
                </thead>
                <tbody>
                  {report.weeklyCounts.map((row) => (
                    <tr key={row.weekStart} className="border-t border-border">
                      <td className="px-4 py-3 text-sm">{formatDate(row.weekStart)}</td>
                      <td className="px-4 py-3 text-right text-sm">{row.count}</td>
                    </tr>
                  ))}
                </tbody>
              </DataTable>
            </div>
          </div>
        </>
      )}
    </section>
  );
}

function DriverPerformanceTab({
  rows,
  loading,
  page,
  totalPages,
  onPage,
  onExport
}: {
  rows: PagedResult<DriverPerformanceRow> | null;
  loading: boolean;
  page: number;
  totalPages: number;
  onPage: (page: number) => void;
  onExport: () => void;
}) {
  return (
    <section className="mt-6 space-y-4">
      <SectionHeader title="Driver Performance" onExport={onExport} loading={loading} />
      {loading && !rows ? (
        <LoadingSkeleton rows={5} />
      ) : (
        <DataTable>
          <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left">Driver</th>
              <th className="px-4 py-3 text-right">Trips Completed</th>
              <th className="px-4 py-3 text-right">Avg Delivery Min</th>
              <th className="px-4 py-3 text-right">On-time Rate</th>
              <th className="px-4 py-3 text-right">Doc Compliance</th>
            </tr>
          </thead>
          <tbody>
            {rows?.items.map((row) => (
              <tr key={row.driverUserId} className="border-t border-border">
                <td className="px-4 py-3 text-sm">{row.driverName}</td>
                <td className="px-4 py-3 text-right text-sm">{row.tripsCompleted}</td>
                <td className="px-4 py-3 text-right text-sm">{formatNumber(row.averageDeliveryMinutes)}</td>
                <td className="px-4 py-3 text-right text-sm">{formatPercent(row.onTimeRatePercent)}</td>
                <td className="px-4 py-3 text-right text-sm">{formatPercent(row.documentComplianceRatePercent)}</td>
              </tr>
            ))}
          </tbody>
        </DataTable>
      )}
      {rows?.items.length === 0 && !loading ? <EmptyState title="No driver performance data." /> : null}
      <Pager page={page} totalPages={totalPages} loading={loading} onPage={onPage} />
    </section>
  );
}

function DeliveryTimeTab({
  report,
  loading,
  page,
  totalPages,
  onPage,
  onExport
}: {
  report: DeliveryTimeReport | null;
  loading: boolean;
  page: number;
  totalPages: number;
  onPage: (page: number) => void;
  onExport: () => void;
}) {
  return (
    <section className="mt-6 space-y-6">
      <SectionHeader title="Delivery Time Analysis" onExport={onExport} loading={loading} />
      {loading && !report ? (
        <LoadingSkeleton rows={5} />
      ) : !report ? (
        <EmptyState title="No delivery time data." />
      ) : (
        <>
          <div className="space-y-3">
            <h2 className="text-lg font-semibold">Average Delivery Time per Route</h2>
            <DeliveryRouteTable rows={report.routes.items} />
            {report.routes.items.length === 0 ? <EmptyState title="No route data." /> : null}
            <Pager page={page} totalPages={totalPages} loading={loading} onPage={onPage} />
          </div>
          <div className="space-y-3">
            <h2 className="text-lg font-semibold">Longest Routes</h2>
            <DeliveryRouteTable rows={report.longestRoutes} />
          </div>
        </>
      )}
    </section>
  );
}

function DeliveryRouteTable({ rows }: { rows: DeliveryTimeRouteRow[] }) {
  return (
    <DataTable>
      <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
        <tr>
          <th className="px-4 py-3 text-left">Route</th>
          <th className="px-4 py-3 text-right">Trips</th>
          <th className="px-4 py-3 text-right">Avg Min</th>
          <th className="px-4 py-3 text-right">Longest Min</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.routeKey} className="border-t border-border">
            <td className="px-4 py-3 text-sm">{row.routeKey}</td>
            <td className="px-4 py-3 text-right text-sm">{row.tripCount}</td>
            <td className="px-4 py-3 text-right text-sm">{formatNumber(row.averageDeliveryMinutes)}</td>
            <td className="px-4 py-3 text-right text-sm">{formatNumber(row.longestDeliveryMinutes)}</td>
          </tr>
        ))}
      </tbody>
    </DataTable>
  );
}

function DocumentProcessingTab({
  rows,
  loading,
  onExport
}: {
  rows: DocumentProcessingRow[];
  loading: boolean;
  onExport: () => void;
}) {
  return (
    <section className="mt-6 space-y-4">
      <SectionHeader title="Document Processing" onExport={onExport} loading={loading} />
      {loading && rows.length === 0 ? (
        <LoadingSkeleton rows={5} />
      ) : (
        <DataTable>
          <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left">Type</th>
              <th className="px-4 py-3 text-right">Pending Verification</th>
              <th className="px-4 py-3 text-right">Avg Verification Hours</th>
              <th className="px-4 py-3 text-right">Rejection Rate</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.documentType} className="border-t border-border">
                <td className="px-4 py-3 text-sm">{row.documentType}</td>
                <td className="px-4 py-3 text-right text-sm">{row.pendingVerification}</td>
                <td className="px-4 py-3 text-right text-sm">{formatNumber(row.averageVerificationHours)}</td>
                <td className="px-4 py-3 text-right text-sm">{formatPercent(row.rejectionRatePercent)}</td>
              </tr>
            ))}
          </tbody>
        </DataTable>
      )}
      {rows.length === 0 && !loading ? <EmptyState title="No document processing data." /> : null}
    </section>
  );
}

function FinancialSummaryTab({
  report,
  loading,
  onExport
}: {
  report: FinancialSummaryReport | null;
  loading: boolean;
  onExport: () => void;
}) {
  return (
    <section className="mt-6 space-y-6">
      <SectionHeader title="Financial Summary" onExport={onExport} loading={loading} />
      {loading && !report ? (
        <LoadingSkeleton rows={5} />
      ) : !report ? (
        <EmptyState title="No financial summary data." />
      ) : (
        <>
          <div className="space-y-3">
            <h2 className="text-lg font-semibold">Revenue, Payroll, and Fuel</h2>
            <DataTable>
              <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left">Period</th>
                  <th className="px-4 py-3 text-right">Trip Revenue</th>
                  <th className="px-4 py-3 text-right">Driver Payroll</th>
                  <th className="px-4 py-3 text-right">Fuel Cost</th>
                </tr>
              </thead>
              <tbody>
                {report.periods.map((row) => (
                  <tr key={row.periodStart} className="border-t border-border">
                    <td className="px-4 py-3 text-sm">{row.periodLabel}</td>
                    <td className="px-4 py-3 text-right text-sm">{formatMoney(row.totalTripRevenue)}</td>
                    <td className="px-4 py-3 text-right text-sm">{formatMoney(row.totalDriverPayroll)}</td>
                    <td className="px-4 py-3 text-right text-sm">{formatMoney(row.totalFuelCost)}</td>
                  </tr>
                ))}
              </tbody>
            </DataTable>
          </div>
          <div className="space-y-3">
            <h2 className="text-lg font-semibold">Per-driver Payroll</h2>
            <DataTable>
              <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left">Driver</th>
                  <th className="px-4 py-3 text-right">Delivered Trips</th>
                  <th className="px-4 py-3 text-right">Payroll</th>
                </tr>
              </thead>
              <tbody>
                {report.driverPayroll.map((row) => (
                  <tr key={row.driverUserId ?? row.driverName} className="border-t border-border">
                    <td className="px-4 py-3 text-sm">{row.driverName}</td>
                    <td className="px-4 py-3 text-right text-sm">{row.deliveredTrips}</td>
                    <td className="px-4 py-3 text-right text-sm">{formatMoney(row.totalPayroll)}</td>
                  </tr>
                ))}
              </tbody>
            </DataTable>
          </div>
        </>
      )}
    </section>
  );
}

function RecommendationsTab({
  report,
  loading,
  page,
  totalPages,
  onPage,
  onExport
}: {
  report: RecommendationReport | null;
  loading: boolean;
  page: number;
  totalPages: number;
  onPage: (page: number) => void;
  onExport: () => void;
}) {
  const rows = report?.recentHistory.items ?? [];

  return (
    <section className="mt-6 space-y-6">
      <SectionHeader title="Trip Chaining" onExport={onExport} loading={loading} />
      {loading && !report ? (
        <LoadingSkeleton rows={5} />
      ) : !report ? (
        <EmptyState title="No Trip Chaining history." />
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <KpiCard title="Suggested" value={report.kpis.totalGenerated.toLocaleString()} subtitle="Next movements identified" />
            <KpiCard title="Confirmed" value={report.kpis.totalAccepted.toLocaleString()} subtitle="Dispatcher confirmed" />
            <KpiCard title="Dismissed" value={report.kpis.totalIgnored.toLocaleString()} subtitle="Dismissed or superseded" />
            <KpiCard title="Confirmation Rate" value={formatPercent(report.kpis.acceptanceRatePercent)} subtitle="Confirmed out of suggested" />
          </div>

          <RecommendationAcceptanceBar data={report.weekly} loading={loading} />

          <div className="space-y-3">
            <h2 className="text-lg font-semibold">Recent Trip Chaining history</h2>
            <DataTable>
              <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left">Date</th>
                  <th className="px-4 py-3 text-left">Driver</th>
                  <th className="px-4 py-3 text-left">Current Movement</th>
                  <th className="px-4 py-3 text-left">Suggested Next Movement</th>
                  <th className="px-4 py-3 text-left">Action</th>
                  <th className="px-4 py-3 text-left">Reviewed By</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={`${row.completedTripId}-${row.recommendedTripId}-${row.rank}`} className="border-t border-border">
                    <td className="px-4 py-3 text-sm">{formatDate(row.date)}</td>
                    <td className="px-4 py-3 text-sm">{row.driver}</td>
                    <td className="px-4 py-3 text-sm font-mono">{row.completedTripId.slice(0, 8).toUpperCase()}</td>
                    <td className="px-4 py-3 text-sm font-mono">{row.recommendedTripId.slice(0, 8).toUpperCase()}</td>
                    <td className="px-4 py-3 text-sm">{row.action === "Accepted" ? "Confirmed" : row.action === "Ignored" ? "Dismissed" : row.action}</td>
                    <td className="px-4 py-3 text-sm">{row.reviewedBy ?? "-"}</td>
                  </tr>
                ))}
              </tbody>
            </DataTable>
            {rows.length === 0 ? <EmptyState title="No Trip Chaining history." /> : null}
            <Pager page={page} totalPages={totalPages} loading={loading} onPage={onPage} />
          </div>
        </>
      )}
    </section>
  );
}

function SectionHeader({
  title,
  loading,
  onExport
}: {
  title: string;
  loading: boolean;
  onExport: () => void;
}) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h2 className="text-lg font-semibold">{title}</h2>
      <Button variant="outline" size="sm" onClick={onExport} disabled={loading} className="gap-2">
        <Download className="h-4 w-4" />
        Export CSV
      </Button>
    </div>
  );
}

function SummaryTile({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="surface-card p-5">
      <p className="text-xs font-bold uppercase tracking-widest text-muted-foreground/80">{label}</p>
      <p className="mt-2 text-2xl font-semibold text-foreground">{value}</p>
    </div>
  );
}

function Pager({
  page,
  totalPages,
  loading,
  onPage
}: {
  page: number;
  totalPages: number;
  loading: boolean;
  onPage: (page: number) => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
      <Button
        variant="outline"
        size="sm"
        onClick={() => onPage(Math.max(1, page - 1))}
        disabled={page <= 1 || loading}
      >
        Prev
      </Button>
      <span>
        Page {page} of {totalPages}
      </span>
      <Button
        variant="outline"
        size="sm"
        onClick={() => onPage(Math.min(totalPages, page + 1))}
        disabled={page >= totalPages || loading}
      >
        Next
      </Button>
    </div>
  );
}

function formatDate(value: string) {
  return value ? value.slice(0, 10) : "-";
}

function formatNumber(value?: number | null) {
  return value == null
    ? "-"
    : value.toLocaleString(undefined, { maximumFractionDigits: 1 });
}

function formatPercent(value?: number | null) {
  return value == null ? "-" : `${formatNumber(value)}%`;
}

function formatMoney(value?: number | null) {
  return value == null
    ? "-"
    : `PHP ${value.toLocaleString(undefined, { maximumFractionDigits: 2 })}`;
}
