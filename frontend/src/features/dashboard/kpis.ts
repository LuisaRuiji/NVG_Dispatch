import { api } from "@/lib/api";

type Me = {
  userId: string;
  roles: string[];
};

type PagedResult = {
  totalCount: number;
};

type DashboardKpis = {
  pendingIoCount: number | null;
  pendingManagerCount: number | null;
  awaitingIssueCount: number | null;
  openLoansCount: number | null;
  lowStockCount: number | null;
  pendingFinancePoCount: number | null;
  pendingCeoPoCount: number | null;
  myRequestsCount: number | null;
};

const cache = new Map<string, { fetchedAt: number; data: DashboardKpis }>();
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
  const isFinance = roles.includes("HeadOfFinance");
  const isCeo = roles.includes("CEO");

  const tasks: Promise<[keyof DashboardKpis, number | null]>[] = [];

  if (isDriver) {
    tasks.push(
      fetchPagedTotal(
        `/api/requests?requesterUserId=${encodeURIComponent(me.userId)}&page=1&pageSize=1`
      )
        .then((count) => ["myRequestsCount", count])
        .catch(() => ["myRequestsCount", null])
    );
  }

  if (isIo) {
    tasks.push(
      fetchPagedTotal("/api/requests?status=PENDING_IO&page=1&pageSize=1")
        .then((count) => ["pendingIoCount", count])
        .catch(() => ["pendingIoCount", null])
    );
    tasks.push(
      fetchPagedTotal("/api/requests?status=APPROVED&page=1&pageSize=1")
        .then((count) => ["awaitingIssueCount", count])
        .catch(() => ["awaitingIssueCount", null])
    );
    tasks.push(
      fetchPagedTotal("/api/loans?status=OPEN,PARTIALLY_RETURNED&page=1&pageSize=1")
        .then((count) => ["openLoansCount", count])
        .catch(() => ["openLoansCount", null])
    );
    tasks.push(
      api<unknown[]>("/api/reports/low-stock", { method: "GET" })
        .then((rows) => ["lowStockCount", rows.length ?? 0])
        .catch(() => ["lowStockCount", null])
    );
  }

  if (isManager) {
    tasks.push(
      fetchPagedTotal("/api/requests?status=PENDING_MANAGER&page=1&pageSize=1")
        .then((count) => ["pendingManagerCount", count])
        .catch(() => ["pendingManagerCount", null])
    );
    tasks.push(
      fetchPagedTotal("/api/loans?status=OPEN,PARTIALLY_RETURNED&page=1&pageSize=1")
        .then((count) => ["openLoansCount", count])
        .catch(() => ["openLoansCount", null])
    );
    tasks.push(
      api<unknown[]>("/api/reports/low-stock", { method: "GET" })
        .then((rows) => ["lowStockCount", rows.length ?? 0])
        .catch(() => ["lowStockCount", null])
    );
  }

  if (isFinance) {
    tasks.push(
      fetchPagedTotal("/api/purchase-orders?status=PENDING_FINANCE&page=1&pageSize=1")
        .then((count) => ["pendingFinancePoCount", count])
        .catch(() => ["pendingFinancePoCount", null])
    );
  }

  if (isCeo) {
    tasks.push(
      fetchPagedTotal("/api/purchase-orders?status=PENDING_CEO&page=1&pageSize=1")
        .then((count) => ["pendingCeoPoCount", count])
        .catch(() => ["pendingCeoPoCount", null])
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
    myRequestsCount: null
  };

  const results = await Promise.all(tasks);
  results.forEach(([k, v]) => {
    base[k] = v;
  });

  cache.set(key, { fetchedAt: now, data: base });
  return base;
}

export function clearDashboardKpiCache() {
  cache.clear();
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event("nvg:kpi-invalidated"));
  }
}
