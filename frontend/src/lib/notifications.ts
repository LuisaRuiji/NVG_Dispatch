import { api } from "@/lib/api";
import { fetchDashboardKpis } from "@/features/dashboard/kpis";
import type { PagedResult } from "@/lib/paging";

type Me = {
  userId: string;
  roles: string[];
};

export type NotificationItem = {
  key: string;
  title: string;
  body?: string;
  href?: string;
  tone?: "info" | "warning";
};

type IntegritySummary = Record<string, number>;

const CACHE_TTL_MS = 45_000;
const cache = new Map<string, { fetchedAt: number; items: NotificationItem[] }>();

function cacheKey(me: Me) {
  return `${me.userId}:${me.roles.join(",")}`;
}

async function fetchPagedTotal(url: string) {
  const result = await api<PagedResult<unknown>>(url, { method: "GET" });
  return result.totalCount ?? 0;
}

function addCountNotice(
  items: NotificationItem[],
  keyBase: string,
  count: number | null,
  title: string,
  href?: string,
  tone: NotificationItem["tone"] = "info",
  body?: string
) {
  if (!count || count <= 0) return;
  items.push({
    key: `${keyBase}:${count}`,
    title: `${count} ${title}`,
    body,
    href,
    tone
  });
}

export async function fetchRoleNotifications(me: Me, force = false): Promise<NotificationItem[]> {
  const key = cacheKey(me);
  const now = Date.now();
  const cached = cache.get(key);
  if (!force && cached && now - cached.fetchedAt < CACHE_TTL_MS) {
    return cached.items;
  }

  const roles = me.roles ?? [];
  const isDriver = roles.includes("Driver");
  const isIo = roles.includes("InventoryOfficer");
  const isManager = roles.includes("Manager");
  const isDispatcher = roles.includes("Dispatcher");
  const isFinance = roles.includes("HeadOfFinance");
  const isCeo = roles.includes("CEO");
  const isAdmin = roles.includes("Admin") || roles.includes("SuperAdmin");

  const items: NotificationItem[] = [];

  const kpisPromise = fetchDashboardKpis(me).catch(() => null);
  const tasks: Promise<void>[] = [];

  if (isDriver) {
    tasks.push(
      fetchPagedTotal(
        `/api/requests?requesterUserId=${encodeURIComponent(
          me.userId
        )}&status=PENDING_IO,PENDING_MANAGER&page=1&pageSize=1`
      )
        .then((count) =>
          addCountNotice(items, "driver-requests-pending", count, "request(s) pending approval", "/my/requests")
        )
        .catch(() => {})
    );
    tasks.push(
      fetchPagedTotal(
        `/api/dispatch/trips?status=DISPATCHED&driverId=${encodeURIComponent(me.userId)}&page=1&pageSize=1`
      )
        .then((count) =>
          addCountNotice(items, "driver-trips-dispatched", count, "trip(s) dispatched", "/dispatch/my-trips", "info")
        )
        .catch(() => {})
    );
    tasks.push(
      fetchPagedTotal(
        `/api/dispatch/trips?status=ON_HOLD&driverId=${encodeURIComponent(me.userId)}&page=1&pageSize=1`
      )
        .then((count) =>
          addCountNotice(items, "driver-trips-hold", count, "trip(s) on hold", "/dispatch/my-trips", "warning")
        )
        .catch(() => {})
    );
    tasks.push(
      fetchPagedTotal(
        `/api/dispatch/trips?status=FAILED_ATTEMPT&driverId=${encodeURIComponent(me.userId)}&page=1&pageSize=1`
      )
        .then((count) =>
          addCountNotice(items, "driver-trips-failed", count, "trip(s) marked failed attempt", "/dispatch/my-trips", "warning")
        )
        .catch(() => {})
    );
  }

  if (isIo) {
    tasks.push(
      fetchPagedTotal("/api/purchase-orders?status=APPROVED,PARTIALLY_RECEIVED&page=1&pageSize=1")
        .then((count) =>
          addCountNotice(items, "io-po-receive", count, "PO(s) ready to receive", "/purchase-orders")
        )
        .catch(() => {})
    );
  }

  if (isManager) {
    tasks.push(
      fetchPagedTotal("/api/purchase-orders?status=PENDING_MANAGER&page=1&pageSize=1")
        .then((count) =>
          addCountNotice(items, "manager-po-approve", count, "PO(s) awaiting approval", "/purchase-orders")
        )
        .catch(() => {})
    );
    tasks.push(
      fetchPagedTotal("/api/dispatch/trips?status=ON_HOLD&page=1&pageSize=1")
        .then((count) =>
          addCountNotice(items, "manager-trips-hold", count, "trip(s) on hold", "/dispatch/board", "warning")
        )
        .catch(() => {})
    );
    tasks.push(
      fetchPagedTotal("/api/dispatch/trips?status=FAILED_ATTEMPT&page=1&pageSize=1")
        .then((count) =>
          addCountNotice(items, "manager-trips-failed", count, "trip(s) need failed attempt review", "/dispatch/board", "warning")
        )
        .catch(() => {})
    );
    tasks.push(
      fetchPagedTotal("/api/dispatch/trips?status=DELIVERED&page=1&pageSize=1")
        .then((count) =>
          addCountNotice(items, "manager-trips-delivered", count, "delivered trip(s) awaiting close", "/dispatch/board", "info")
        )
        .catch(() => {})
    );
  }

  if (isDispatcher) {
    tasks.push(
      fetchPagedTotal("/api/dispatch/trips?status=DRAFT&page=1&pageSize=1")
        .then((count) =>
          addCountNotice(items, "dispatcher-trips-draft", count, "draft trip(s) need dispatch", "/dispatch/board", "info")
        )
        .catch(() => {})
    );
    tasks.push(
      fetchPagedTotal("/api/dispatch/trips?status=DISPATCHED&page=1&pageSize=1")
        .then((count) =>
          addCountNotice(items, "dispatcher-trips-dispatched", count, "dispatched trip(s) active", "/dispatch/board", "info")
        )
        .catch(() => {})
    );
    tasks.push(
      fetchPagedTotal("/api/dispatch/trips?status=ON_HOLD&page=1&pageSize=1")
        .then((count) =>
          addCountNotice(items, "dispatcher-trips-hold", count, "trip(s) on hold", "/dispatch/board", "warning")
        )
        .catch(() => {})
    );
    tasks.push(
      fetchPagedTotal("/api/dispatch/trips?status=FAILED_ATTEMPT&page=1&pageSize=1")
        .then((count) =>
          addCountNotice(items, "dispatcher-trips-failed", count, "trip(s) marked failed attempt", "/dispatch/board", "warning")
        )
        .catch(() => {})
    );
  }

  if (isAdmin || isCeo) {
    tasks.push(
      api<IntegritySummary>("/api/reports/integrity", { method: "GET" })
        .then((summary) => {
          const values = Object.values(summary ?? {});
          const categories = values.filter((value) => typeof value === "number" && value > 0).length;
          if (categories > 0) {
            const total = values.reduce((sum, value) => sum + (typeof value === "number" ? value : 0), 0);
            items.push({
              key: `integrity:${categories}:${total}`,
              title: `${categories} integrity checks flagged`,
              body: `${total} issue(s) need review.`,
              href: "/admin/integrity",
              tone: "warning"
            });
          }
        })
        .catch(() => {})
    );
  }

  const kpis = await kpisPromise;
  if (kpis) {
    if (isIo) {
      addCountNotice(items, "io-pending", kpis.pendingIoCount, "request(s) awaiting IO review", "/queue/io");
      addCountNotice(items, "io-awaiting-issue", kpis.awaitingIssueCount, "request(s) awaiting issue", "/queue/issue");
      addCountNotice(items, "io-loans", kpis.openLoansCount, "open loan(s) need attention", "/loans");
      addCountNotice(items, "io-low-stock", kpis.lowStockCount, "low stock item(s)", "/reports");
    }

    if (isManager) {
      addCountNotice(items, "manager-pending", kpis.pendingManagerCount, "request(s) awaiting decision", "/queue/manager");
      addCountNotice(items, "manager-loans", kpis.openLoansCount, "open loan(s) need attention", "/loans");
      addCountNotice(items, "manager-low-stock", kpis.lowStockCount, "low stock item(s)", "/reports");
    }

    if (isFinance) {
      addCountNotice(items, "finance-pending-po", kpis.pendingFinancePoCount, "PO(s) awaiting finance approval", "/purchase-orders");
    }

    if (isCeo) {
      addCountNotice(items, "ceo-pending-po", kpis.pendingCeoPoCount, "PO(s) awaiting CEO approval", "/purchase-orders");
    }
  }

  await Promise.all(tasks);

  const sorted = items.sort((a, b) => {
    if (a.tone === b.tone) return a.title.localeCompare(b.title);
    if (a.tone === "warning") return -1;
    if (b.tone === "warning") return 1;
    return 0;
  });

  cache.set(key, { fetchedAt: now, items: sorted });
  return sorted;
}
