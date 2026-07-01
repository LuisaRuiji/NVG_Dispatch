import { api } from "@/lib/api";
import type {
  DocumentAlertChartItem,
  DriverUtilizationChartItem,
  FinancialBreakdownChartItem,
  FinancialRevenueChartItem,
  InventoryStockLevelChartItem,
  OnTimeDelayedChartItem,
  PercentByPeriodChartItem,
  TimeCountChartItem
} from "./components/charts/types";

type Me = {
  userId: string;
  roles: string[];
};

type PagedResult = {
  totalCount: number;
};

export type DispatchDashboardKpis = {
  activeTrips: number;
  driversOnRoad: number;
  driversAvailable: number;
  documentAlerts: number;
  pendingShipmentRequests: number;
  tripsOnHold: number;
  tripsInDraft: number;
  tripsFailedAttempt: number;
  approvedShipmentRequests: number;
  incompleteDocumentAlerts: number;
  statusBreakdown: { status: string; count: number }[];
};

export type FinanceDashboardKpis = {
  totalTripValueThisMonth: number;
  totalPayrollThisMonth: number;
  pendingFinancePoCount: number;
  tripsWithMissingRate: number;
};

export type DriverActiveTripKpi = {
  id: string;
  status: string;
  customerName?: string | null;
  truckAssetCode?: string | null;
};

export type DriverDashboardKpis = {
  myActiveTrip?: DriverActiveTripKpi | null;
  myTripsToday: number;
  myTripsThisWeek: number;
  myPendingDocuments: number;
  dailyTrips: TimeCountChartItem[];
  onTimeRate: number;
  docComplianceRate: number;
};

export type CustomerActiveRequestStatusKpi = {
  status: string;
  count: number;
};

export type CustomerDashboardKpis = {
  myActiveRequests: number;
  myActiveRequestStatuses: CustomerActiveRequestStatusKpi[];
  myDeliveredThisMonth: number;
  myPendingDocuments: number;
  statusBreakdown: { status: string; count: number }[];
};

export type SystemDashboardKpis = {
  totalUsers: number;
  totalTripsThisMonth: number;
  fleetUtilizationPercent: number;
  documentComplianceRate: number;
  monthlyVolume: TimeCountChartItem[];
  usersByRole: { role: string; count: number }[];
};

export type DashboardKpis = {
  pendingIoCount: number | null;
  pendingManagerCount: number | null;
  awaitingIssueCount: number | null;
  openLoansCount: number | null;
  lowStockCount: number | null;
  pendingFinancePoCount: number | null;
  pendingCeoPoCount: number | null;
  myRequestsCount: number | null;
  openInventoryRequestsCount: number | null;
  dispatchKpis: DispatchDashboardKpis | null;
  financeKpis: FinanceDashboardKpis | null;
  driverKpis: DriverDashboardKpis | null;
  customerKpis: CustomerDashboardKpis | null;
  systemKpis: SystemDashboardKpis | null;
};

export type DashboardCharts = {
  dispatcherWeeklyTrips: TimeCountChartItem[];
  documentAlerts: DocumentAlertChartItem[];
  managerWeeklyCompletion: PercentByPeriodChartItem[];
  driverUtilization: DriverUtilizationChartItem[];
  onTimeVsDelayed: OnTimeDelayedChartItem[];
  financeWeeklyRevenue: FinancialRevenueChartItem[];
  financeWeeklyBreakdown: FinancialBreakdownChartItem[];
  customerMonthlyDeliveries: TimeCountChartItem[];
  ceoFleetUtilization: PercentByPeriodChartItem[];
  ceoTripVolume: TimeCountChartItem[];
  ceoRevenueVsPayroll: FinancialBreakdownChartItem[];
  inventoryStockLevels: InventoryStockLevelChartItem[];
  inventoryWeeklyRequests: TimeCountChartItem[];
};

export const emptyDashboardCharts: DashboardCharts = {
  dispatcherWeeklyTrips: [],
  documentAlerts: [],
  managerWeeklyCompletion: [],
  driverUtilization: [],
  onTimeVsDelayed: [],
  financeWeeklyRevenue: [],
  financeWeeklyBreakdown: [],
  customerMonthlyDeliveries: [],
  ceoFleetUtilization: [],
  ceoTripVolume: [],
  ceoRevenueVsPayroll: [],
  inventoryStockLevels: [],
  inventoryWeeklyRequests: []
};

const cache = new Map<string, { fetchedAt: number; data: DashboardKpis }>();
const chartCache = new Map<string, { fetchedAt: number; data: DashboardCharts }>();
const CACHE_TTL_MS = 60_000;

function cacheKey(me: Me) {
  return `${me.userId}:${me.roles.join(",")}`;
}

async function fetchPagedTotal(url: string) {
  const result = await api<PagedResult>(url, { method: "GET" });
  return result.totalCount ?? 0;
}

export async function fetchDashboardKpis(me: Me, force = false): Promise<DashboardKpis> {
  const key = cacheKey(me);
  const cached = cache.get(key);
  const now = Date.now();
  if (!force && cached && now - cached.fetchedAt < CACHE_TTL_MS) {
    return cached.data;
  }

  const roles = me.roles;
  const isDriver = roles.includes("Driver");
  const isIo = roles.includes("InventoryOfficer");
  const isManager = roles.includes("Manager");
  const isDispatcher = roles.includes("Dispatcher");
  const isFinance = roles.includes("HeadOfFinance");
  const isCeo = roles.includes("CEO");
  const isAdmin = roles.includes("Admin");
  const isSuperAdmin = roles.includes("SuperAdmin");
  const isCustomer = roles.includes("Customer");

  const tasks: Promise<[keyof DashboardKpis, DashboardKpis[keyof DashboardKpis]]>[] = [];
  const toKpiTask = <K extends keyof DashboardKpis>(
    keyName: K,
    valueTask: Promise<DashboardKpis[K]>
  ): Promise<[keyof DashboardKpis, DashboardKpis[keyof DashboardKpis]]> =>
    valueTask
      .then((value): [keyof DashboardKpis, DashboardKpis[keyof DashboardKpis]] => [keyName, value])
      .catch((): [keyof DashboardKpis, null] => [keyName, null]);

  if (isDispatcher || isManager || isAdmin || isSuperAdmin || isCeo) {
    tasks.push(
      toKpiTask("dispatchKpis", api<DispatchDashboardKpis>("/api/dashboard/dispatch-kpis", { method: "GET" }))
    );
  }

  if (isFinance || isCeo) {
    tasks.push(
      toKpiTask("financeKpis", api<FinanceDashboardKpis>("/api/dashboard/finance-kpis", { method: "GET" }))
    );
  }

  if (isDriver) {
    tasks.push(
      toKpiTask("driverKpis", api<DriverDashboardKpis>("/api/dashboard/driver-kpis", { method: "GET" }))
    );
  }

  if (isCustomer) {
    tasks.push(
      toKpiTask("customerKpis", api<CustomerDashboardKpis>("/api/dashboard/customer-kpis", { method: "GET" }))
    );
  }

  if (isAdmin || isSuperAdmin || isCeo) {
    tasks.push(
      toKpiTask("systemKpis", api<SystemDashboardKpis>("/api/dashboard/system-kpis", { method: "GET" }))
    );
  }

  if (isDriver) {
    tasks.push(
      toKpiTask(
        "myRequestsCount",
        fetchPagedTotal(`/api/requests?requesterUserId=${encodeURIComponent(me.userId)}&page=1&pageSize=1`)
      )
    );
  }

  if (isIo) {
    tasks.push(
      toKpiTask("pendingIoCount", fetchPagedTotal("/api/requests?status=PENDING_IO&page=1&pageSize=1"))
    );
    tasks.push(
      toKpiTask("awaitingIssueCount", fetchPagedTotal("/api/requests?status=APPROVED&page=1&pageSize=1"))
    );
    tasks.push(
      toKpiTask("openLoansCount", fetchPagedTotal("/api/loans?status=OPEN,PARTIALLY_RETURNED&page=1&pageSize=1"))
    );
    tasks.push(
      toKpiTask(
        "lowStockCount",
        api<unknown[]>("/api/reports/low-stock", { method: "GET" }).then((rows) => rows.length ?? 0)
      )
    );
  }

  if (isAdmin || isSuperAdmin) {
    tasks.push(
      toKpiTask(
        "openInventoryRequestsCount",
        fetchPagedTotal("/api/requests?status=PENDING_IO,PENDING_MANAGER,APPROVED&page=1&pageSize=1")
      )
    );
    tasks.push(
      toKpiTask(
        "lowStockCount",
        api<unknown[]>("/api/reports/low-stock", { method: "GET" }).then((rows) => rows.length ?? 0)
      )
    );
  }

  if (isManager) {
    tasks.push(
      toKpiTask("pendingManagerCount", fetchPagedTotal("/api/requests?status=PENDING_MANAGER&page=1&pageSize=1"))
    );
    tasks.push(
      toKpiTask("openLoansCount", fetchPagedTotal("/api/loans?status=OPEN,PARTIALLY_RETURNED&page=1&pageSize=1"))
    );
    tasks.push(
      toKpiTask(
        "lowStockCount",
        api<unknown[]>("/api/reports/low-stock", { method: "GET" }).then((rows) => rows.length ?? 0)
      )
    );
  }

  if (isFinance) {
    tasks.push(
      toKpiTask(
        "pendingFinancePoCount",
        fetchPagedTotal("/api/purchase-orders?status=PENDING_FINANCE&page=1&pageSize=1")
      )
    );
  }

  if (isCeo) {
    tasks.push(
      toKpiTask("pendingCeoPoCount", fetchPagedTotal("/api/purchase-orders?status=PENDING_CEO&page=1&pageSize=1"))
    );
  }

  const base: DashboardKpis = {
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

  const results = await Promise.all(tasks);
  results.forEach(([k, v]) => {
    (base as Record<keyof DashboardKpis, DashboardKpis[keyof DashboardKpis]>)[k] = v;
  });

  cache.set(key, { fetchedAt: now, data: base });
  return base;
}

export async function fetchDashboardCharts(me: Me, force = false): Promise<DashboardCharts> {
  const key = `charts:${cacheKey(me)}`;
  const cached = chartCache.get(key);
  const now = Date.now();
  if (!force && cached && now - cached.fetchedAt < CACHE_TTL_MS) {
    return cached.data;
  }

  const roles = me.roles;
  const isIo = roles.includes("InventoryOfficer");
  const isManager = roles.includes("Manager");
  const isDispatcher = roles.includes("Dispatcher");
  const isFinance = roles.includes("HeadOfFinance");
  const isCeo = roles.includes("CEO");
  const isAdmin = roles.includes("Admin");
  const isSuperAdmin = roles.includes("SuperAdmin");
  const isCustomer = roles.includes("Customer");

  const tasks: Promise<[keyof DashboardCharts, DashboardCharts[keyof DashboardCharts]]>[] = [];
  const toChartTask = <K extends keyof DashboardCharts>(
    keyName: K,
    valueTask: Promise<DashboardCharts[K]>
  ): Promise<[keyof DashboardCharts, DashboardCharts[keyof DashboardCharts]]> =>
    valueTask
      .then((value): [keyof DashboardCharts, DashboardCharts[keyof DashboardCharts]] => [keyName, value])
      .catch((): [keyof DashboardCharts, DashboardCharts[keyof DashboardCharts]] => [keyName, []]);

  if (isDispatcher || isManager || isAdmin || isSuperAdmin) {
    tasks.push(toChartTask("dispatcherWeeklyTrips", api<TimeCountChartItem[]>("/api/dashboard/dispatcher-weekly-trips", { method: "GET" })));
    tasks.push(toChartTask("documentAlerts", api<DocumentAlertChartItem[]>("/api/dashboard/document-alerts", { method: "GET" })));
  }

  if (isManager || isAdmin || isSuperAdmin || isCeo) {
    tasks.push(toChartTask("managerWeeklyCompletion", api<PercentByPeriodChartItem[]>("/api/dashboard/manager-weekly-completion", { method: "GET" })));
    tasks.push(toChartTask("driverUtilization", api<DriverUtilizationChartItem[]>("/api/dashboard/driver-utilization", { method: "GET" })));
    tasks.push(toChartTask("onTimeVsDelayed", api<OnTimeDelayedChartItem[]>("/api/dashboard/ontime-vs-delayed", { method: "GET" })));
  }

  if (isFinance || isManager || isAdmin || isSuperAdmin || isCeo) {
    tasks.push(toChartTask("financeWeeklyRevenue", api<FinancialRevenueChartItem[]>("/api/dashboard/finance-weekly-revenue", { method: "GET" })));
    tasks.push(toChartTask("financeWeeklyBreakdown", api<FinancialBreakdownChartItem[]>("/api/dashboard/finance-weekly-breakdown", { method: "GET" })));
  }

  if (isCustomer) {
    tasks.push(toChartTask("customerMonthlyDeliveries", api<TimeCountChartItem[]>("/api/dashboard/customer-monthly-deliveries", { method: "GET" })));
  }

  if (isCeo || isAdmin || isSuperAdmin) {
    tasks.push(toChartTask("ceoFleetUtilization", api<PercentByPeriodChartItem[]>("/api/dashboard/ceo-fleet-utilization", { method: "GET" })));
    tasks.push(toChartTask("ceoTripVolume", api<TimeCountChartItem[]>("/api/dashboard/ceo-trip-volume", { method: "GET" })));
    tasks.push(toChartTask("ceoRevenueVsPayroll", api<FinancialBreakdownChartItem[]>("/api/dashboard/finance-weekly-breakdown?groupBy=month", { method: "GET" })));
  }

  if (isIo || isManager || isAdmin || isSuperAdmin) {
    tasks.push(toChartTask("inventoryStockLevels", api<InventoryStockLevelChartItem[]>("/api/dashboard/inventory-stock-levels", { method: "GET" })));
    tasks.push(toChartTask("inventoryWeeklyRequests", api<TimeCountChartItem[]>("/api/dashboard/inventory-weekly-requests", { method: "GET" })));
  }

  const nextCharts: DashboardCharts = { ...emptyDashboardCharts };
  const results = await Promise.all(tasks);
  results.forEach(([k, v]) => {
    (nextCharts as Record<keyof DashboardCharts, DashboardCharts[keyof DashboardCharts]>)[k] = v;
  });

  chartCache.set(key, { fetchedAt: now, data: nextCharts });
  return nextCharts;
}

export function clearDashboardKpiCache() {
  cache.clear();
  chartCache.clear();
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event("nvg:kpi-invalidated"));
  }
}
