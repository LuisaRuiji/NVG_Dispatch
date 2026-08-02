import { useEffect, useMemo, useRef, useState, type ComponentType } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { getMe, logout } from "@/features/auth/authStore";
import { resolvePrimaryRole, type UserRole } from "@/features/auth/roles";
import { cn } from "@/lib/utils";
import vaiaLogo from "@/assets/755802620_1034371329178362_4911076349972347238_n.png";
import ToastHost from "@/components/ToastHost";
import { useToast } from "@/lib/useToast";
import { onToast } from "@/lib/toastBus";
import { onMaintenance } from "@/lib/maintenanceBus";
import { api } from "@/lib/api";
import { fetchRoleNotifications, type NotificationItem } from "@/lib/notifications";
import {
  LayoutDashboard,
  Package,
  FileText,
  Truck,
  ShoppingCart,
  ShieldCheck,
  ClipboardList,
  ClipboardCheck,
  User,
  Settings,
  Bell,
  Sliders,
  Map as MapIcon,
  CalendarClock,
  LogOut
} from "lucide-react";
import { TrackingProvider } from "@/features/dispatch/TrackingContext";

type NavItem = {
  label: string;
  to: string;
  roles?: UserRole[];
  moduleKey?: string;
  icon: ComponentType<{ className?: string }>;
};

const navSections: { title: string; items: NavItem[] }[] = [
  {
    title: "Core",
    items: [
      { label: "Dashboard", to: "/dashboard", roles: ["SuperAdmin", "Admin", "Dispatcher", "Manager", "HeadOfFinance", "Driver", "Customer", "InventoryOfficer", "CEO"], icon: LayoutDashboard },
      { label: "Inventory", to: "/inventory", roles: ["InventoryOfficer", "Manager"], icon: Package, moduleKey: "inventory" }
    ]
  },
  {
    title: "Requests",
    items: [
      { label: "Requests", to: "/my/requests", roles: ["Driver"], icon: User, moduleKey: "requests" },
      { label: "IO Queue", to: "/queue/io", roles: ["InventoryOfficer"], icon: ClipboardList, moduleKey: "requests" },
      { label: "Manager Queue", to: "/queue/manager", roles: ["Manager"], icon: ClipboardCheck, moduleKey: "requests" },
      { label: "Awaiting Issue", to: "/queue/issue", roles: ["InventoryOfficer"], icon: FileText, moduleKey: "requests" }
    ]
  },
  {
    title: "Operations",
    items: [
      { label: "Loans", to: "/loans", roles: ["InventoryOfficer", "Manager"], icon: Truck, moduleKey: "loans" },
      { label: "Purchase Orders", to: "/purchase-orders", roles: ["InventoryOfficer", "Manager", "HeadOfFinance", "CEO"], icon: ShoppingCart, moduleKey: "purchase-orders" },
      { label: "Live Operations Map", to: "/operations/live-map", roles: ["Owner"], icon: MapIcon, moduleKey: "dispatch" }
    ]
  },
  {
    title: "Dispatch",
    items: [
      { label: "Dispatch Board", to: "/dispatch/board", roles: ["Manager", "Dispatcher", "CEO"], icon: Truck, moduleKey: "dispatch" },
      { label: "Live Operations Map", to: "/dispatch/live-map", roles: ["Manager", "Dispatcher"], icon: MapIcon, moduleKey: "dispatch" },
      { label: "Requests", to: "/dispatch/requests", roles: ["Manager", "Dispatcher"], icon: ClipboardList, moduleKey: "dispatch" },
      { label: "Planning", to: "/dispatch/planning", roles: ["Manager", "Dispatcher"], icon: CalendarClock, moduleKey: "dispatch" },
      { label: "Trips", to: "/dispatch/trips", roles: ["Manager", "Dispatcher", "CEO"], icon: Truck, moduleKey: "dispatch" },
      { label: "Optimization Settings", to: "/dispatch/optimization-settings", roles: ["Manager", "Owner"], icon: Sliders, moduleKey: "dispatch" },
      { label: "Documents", to: "/dispatch/documents", roles: ["Manager", "HeadOfFinance"], icon: FileText, moduleKey: "dispatch" },
      { label: "Trips", to: "/dispatch/my-trips", roles: ["Driver"], icon: Truck, moduleKey: "dispatch" }
    ]
  },
  {
    title: "Customer Portal",
    items: [
      { label: "Portal Dashboard", to: "/portal/dashboard", roles: ["Customer"], icon: LayoutDashboard, moduleKey: "portal" },
      { label: "Shipment Requests", to: "/portal/requests", roles: ["Customer"], icon: ClipboardList, moduleKey: "portal" },
      { label: "Shipments", to: "/portal/shipments", roles: ["Customer"], icon: Truck, moduleKey: "portal" }
    ]
  },
  {
    title: "Reports",
    items: [
      { label: "Inventory Reports", to: "/reports", roles: ["Manager", "InventoryOfficer", "HeadOfFinance", "CEO", "Admin", "SuperAdmin"], icon: FileText, moduleKey: "reports" },
      { label: "Dispatch Reports", to: "/reports/dispatch", roles: ["Dispatcher", "Manager", "HeadOfFinance", "CEO", "Admin", "SuperAdmin"], icon: FileText, moduleKey: "reports" }
    ]
  },
  {
    title: "Admin",
    items: [
      { label: "Customers", to: "/admin/customers", roles: ["Manager", "Admin", "SuperAdmin"], icon: User, moduleKey: "users" },
      { label: "Modules", to: "/admin/modules", roles: ["SuperAdmin"], icon: Settings },
      { label: "Audit Logs", to: "/admin/audit-logs", roles: ["SuperAdmin", "Admin", "Manager", "Dispatcher", "HeadOfFinance", "InventoryOfficer"], icon: FileText, moduleKey: "reports" },
      { label: "Auth Logs", to: "/admin/auth-events", roles: ["Admin", "SuperAdmin"], icon: FileText, moduleKey: "reports" },
      { label: "Users", to: "/admin/users", roles: ["Admin", "SuperAdmin"], icon: User, moduleKey: "users" },
      { label: "Integrity", to: "/admin/integrity", roles: ["Manager", "HeadOfFinance", "CEO", "Admin", "SuperAdmin"], icon: ShieldCheck, moduleKey: "reports" }
    ]
  }
];

export default function AppLayout() {
  const me = getMe();
  const nav = useNavigate();
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const { toasts, show } = useToast();
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const [notificationSummaryOpen, setNotificationSummaryOpen] = useState(false);
  const notificationRef = useRef<HTMLDivElement | null>(null);
  const [moduleStatus, setModuleStatus] = useState<
    { moduleKey: string; displayName: string; isEnabled: boolean; notes?: string | null }[]
  >([]);
  const [statusLoading, setStatusLoading] = useState(false);
  const [statusLoaded, setStatusLoaded] = useState(false);
  const [roleNotifications, setRoleNotifications] = useState<NotificationItem[]>([]);
  const [roleLoading, setRoleLoading] = useState(false);
  const [acknowledgedModules, setAcknowledgedModules] = useState<Set<string>>(() => {
    if (typeof window === "undefined") return new Set();
    try {
      const raw = window.localStorage.getItem("nvg_ack_modules");
      if (!raw) return new Set();
      const parsed = JSON.parse(raw) as string[];
      return new Set(parsed ?? []);
    } catch {
      return new Set();
    }
  });
  const [acknowledgedNotifications, setAcknowledgedNotifications] = useState<Set<string>>(() => {
    if (typeof window === "undefined") return new Set();
    try {
      const raw = window.localStorage.getItem("nvg_ack_notifications");
      if (!raw) return new Set();
      const parsed = JSON.parse(raw) as string[];
      return new Set(parsed ?? []);
    } catch {
      return new Set();
    }
  });

  const roles = me?.roles ?? [];
  const activeRole = resolvePrimaryRole(roles);
  const primaryRole = activeRole ?? "User";
  const canSeeSearch = ["InventoryOfficer", "Manager", "HeadOfFinance", "CEO"].includes(primaryRole);

  const moduleEnabledMap = useMemo(() => {
    const map = new Map<string, boolean>();
    moduleStatus.forEach((entry) => {
      map.set(entry.moduleKey, entry.isEnabled);
    });
    return map;
  }, [moduleStatus]);

  const disabledModules = useMemo(
    () => moduleStatus.filter((module) => !module.isEnabled),
    [moduleStatus]
  );
  const unacknowledgedDisabled = useMemo(
    () =>
      disabledModules.filter((module) => !acknowledgedModules.has(module.moduleKey)),
    [disabledModules, acknowledgedModules]
  );
  const unacknowledgedRoleNotifications = useMemo(
    () => roleNotifications.filter((item) => !acknowledgedNotifications.has(item.key)),
    [roleNotifications, acknowledgedNotifications]
  );
  const hasUnread = unacknowledgedDisabled.length > 0 || unacknowledgedRoleNotifications.length > 0;
  const unreadCount = unacknowledgedDisabled.length + unacknowledgedRoleNotifications.length;
  const notificationsLoading = statusLoading || roleLoading;
  const unreadKeys = useMemo(() => {
    const keys = [
      ...unacknowledgedDisabled.map((module) => `maintenance:${module.moduleKey}`),
      ...unacknowledgedRoleNotifications.map((item) => `role:${item.key}`)
    ];
    return keys.sort().join("|");
  }, [unacknowledgedDisabled, unacknowledgedRoleNotifications]);

  function persistAcknowledged(next: Set<string>) {
    if (typeof window === "undefined") return;
    window.localStorage.setItem("nvg_ack_modules", JSON.stringify(Array.from(next)));
  }

  function persistAcknowledgedNotifications(next: Set<string>) {
    if (typeof window === "undefined") return;
    window.localStorage.setItem("nvg_ack_notifications", JSON.stringify(Array.from(next)));
  }

  async function loadModuleStatus() {
    try {
      setStatusLoading(true);
      const data = await api<
        { moduleKey: string; displayName: string; isEnabled: boolean; notes?: string | null }[]
      >("/api/modules/status", { method: "GET" });
      setModuleStatus(data);
      setStatusLoaded(true);
    } catch (e) {
      console.error(e);
      setStatusLoaded(true);
    } finally {
      setStatusLoading(false);
    }
  }

  async function loadRoleAlerts(force = false) {
    if (!me) return;
    try {
      setRoleLoading(true);
      const items = await fetchRoleNotifications({ userId: me.userId, roles }, force);
      setRoleNotifications(items);
    } catch (e) {
      console.error(e);
    } finally {
      setRoleLoading(false);
    }
  }

  const filteredSections = useMemo(
    () =>
      navSections
        .map((section) => ({
          ...section,
          items: section.items.filter((item) =>
            (item.roles ? Boolean(activeRole && item.roles.includes(activeRole)) : true) &&
            (item.moduleKey ? moduleEnabledMap.get(item.moduleKey) !== false : true)
          )
        }))
        .filter((section) => section.items.length > 0),
    [activeRole, moduleEnabledMap]
  );

  useEffect(() => {
    return onToast((detail) => {
      show(detail.message, detail.type ?? "error");
    });
  }, [show]);

  useEffect(() => {
    if (!me) return;
    loadModuleStatus();
    loadRoleAlerts();
  }, [me]);

  useEffect(() => {
    return onMaintenance(() => {
      loadModuleStatus();
      loadRoleAlerts(true);
    });
  }, []);

  useEffect(() => {
    if (!me || !statusLoaded) return;
    if (unacknowledgedDisabled.length === 0 && unacknowledgedRoleNotifications.length === 0) return;
    const key = `nvg_announce_seen_${me.userId}`;
    if (typeof window !== "undefined") {
      const seen = window.sessionStorage.getItem(key);
      if (seen !== unreadKeys) {
        setNotificationSummaryOpen(true);
        window.sessionStorage.setItem(key, unreadKeys);
      }
    }
  }, [me, statusLoaded, unacknowledgedDisabled.length, unacknowledgedRoleNotifications.length, unreadKeys]);

  useEffect(() => {
    if (!notificationSummaryOpen) return;
    const timeout = window.setTimeout(() => {
      setNotificationSummaryOpen(false);
    }, 5000);
    return () => window.clearTimeout(timeout);
  }, [notificationSummaryOpen, unreadKeys]);

  useEffect(() => {
    if (!notificationsOpen) return;
    const handler = (event: MouseEvent) => {
      const target = event.target as Node | null;
      if (notificationRef.current && target && !notificationRef.current.contains(target)) {
        setNotificationsOpen(false);
      }
    };
    window.addEventListener("click", handler);
    return () => window.removeEventListener("click", handler);
  }, [notificationsOpen]);

  if (!me) {
    nav("/login");
    return null;
  }

  return (
    <TrackingProvider enabled={roles.includes("Driver")}>
      <div className="vaia-shell fixed inset-0 flex min-h-0 w-full overflow-hidden bg-background text-foreground">
        <ToastHost toasts={toasts} />
      <aside className="hidden h-full w-60 flex-col overflow-hidden border-r border-border bg-card lg:flex">
        <div className="px-5 py-5">
          <div className="flex items-center gap-3">
            <div className="grid h-10 w-10 place-items-center overflow-hidden rounded-lg bg-black">
              <img src={vaiaLogo} alt="VAIA emblem" className="h-full w-full object-contain" />
            </div>
            <div className="flex flex-col">
              <p className="text-[11px] font-semibold uppercase tracking-[0.12em] text-muted-foreground">Operations</p>
              <p className="text-lg font-bold text-foreground">VAIA</p>
            </div>
          </div>
        </div>

        <nav className="sidebar-nav flex-1 min-h-0 space-y-5 overflow-y-auto px-3 pb-5">
          {filteredSections.map((section) => (
            <div key={section.title}>
              <p className="mb-2 px-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
                {section.title}
              </p>
              <div className="space-y-1">
                {section.items.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    className={({ isActive }) =>
                      cn(
                        "relative flex min-h-10 items-center gap-3 rounded-lg px-3 text-sm font-medium transition-colors duration-150",
                        isActive
                          ? "bg-accent text-[#122442] before:absolute before:-left-3 before:h-6 before:w-[3px] before:rounded-r before:bg-primary"
                          : "text-muted-foreground hover:bg-muted hover:text-foreground"
                      )
                    }
                  >
                    {({ isActive }) => (
                      <>
                        <item.icon
                          className={cn(
                            "h-5 w-5 shrink-0",
                            isActive ? "text-primary" : "text-muted-foreground"
                          )}
                        />
                        <span className="whitespace-nowrap">{item.label}</span>
                      </>
                    )}
                  </NavLink>
                ))}
              </div>
            </div>
          ))}
        </nav>

        <div className="m-3 flex items-center gap-1 border-t border-border pt-3">
          <button type="button" onClick={() => nav("/settings")} className="flex min-w-0 flex-1 items-center gap-3 rounded-lg px-3 py-2 text-left hover:bg-muted">
            <span className="grid h-8 w-8 place-items-center rounded-lg bg-muted text-xs font-bold text-foreground">{me.username.slice(0, 1).toUpperCase()}</span>
            <span className="min-w-0"><span className="block truncate text-sm font-semibold text-foreground">{me.username}</span><span className="block text-xs text-muted-foreground">{primaryRole}</span></span>
          </button>
          <button type="button" onClick={() => { logout(); nav("/login"); }} className="grid h-10 w-10 place-items-center rounded-lg text-muted-foreground hover:bg-muted hover:text-destructive" aria-label="Log out" title="Log out"><LogOut className="h-4 w-4" /></button>
        </div>
      </aside>

      <div className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <header className="flex h-[72px] items-center justify-between border-b border-border bg-card px-4 md:px-6">
          <div className="flex items-center gap-3 lg:hidden">
            <button
              className="grid h-10 w-10 place-items-center overflow-hidden rounded-lg border border-border bg-black"
              onClick={() => setOpen(true)}
              aria-label="Open navigation"
            >
              <img src={vaiaLogo} alt="VAIA" className="h-full w-full object-contain" />
            </button>
          </div>
          {canSeeSearch ? (
            <div className="hidden items-center gap-3 lg:flex">
              <div className="relative">
                <input
                  placeholder="Search operations"
                  className="h-10 w-72 rounded-lg border border-border bg-card px-3 text-sm transition-colors hover:border-primary/50 focus:outline-none focus:ring-2 focus:ring-primary/40"
                />
                <kbd className="pointer-events-none absolute right-2.5 top-2 hidden h-5 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium opacity-100 sm:flex text-muted-foreground">
                  <span className="text-xs">⌘</span>K
                </kbd>
              </div>
            </div>
          ) : null}

          <div className="ml-auto flex items-center gap-3 text-sm">
            <div className="hidden items-center gap-2 lg:flex">
              {me.username.toLowerCase() !== primaryRole.toLowerCase() ? (
                <span className="text-slate-500">{me.username}</span>
              ) : null}
              <div className="relative" ref={notificationRef}>
                <button
                  type="button"
                  className="grid h-10 w-10 place-items-center rounded-lg border border-border bg-card text-muted-foreground hover:bg-muted hover:text-foreground"
                  onClick={() => {
                    setNotificationSummaryOpen(false);
                    setNotificationsOpen((value) => !value);
                  }}
                  aria-label="Notifications"
                >
                  <Bell className="h-4 w-4" />
                  {hasUnread ? (
                    <span className="absolute right-1 top-1 h-2 w-2 rounded-full bg-rose-500" />
                  ) : null}
                </button>
                {notificationSummaryOpen && !notificationsOpen && unreadCount > 0 ? (
                  <div className="absolute right-0 z-50 mt-2 w-64 rounded-xl border border-border bg-card px-4 py-3 text-sm font-medium text-foreground fade-in">
                    You have {unreadCount} notification{unreadCount === 1 ? "" : "s"}.
                  </div>
                ) : null}
                {notificationsOpen ? (
                  <div className="absolute right-0 z-50 mt-2 w-72 rounded-xl border border-border bg-card p-3 fade-in">
                    <div className="flex items-center justify-between">
                      <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">Notifications</p>
                      <div className="flex items-center gap-2">
                        {hasUnread ? (
                          <button
                            className="text-xs text-muted-foreground hover:text-foreground"
                            onClick={() => {
                              const next = new Set(acknowledgedModules);
                              unacknowledgedDisabled.forEach((module) => next.add(module.moduleKey));
                              setAcknowledgedModules(next);
                              persistAcknowledged(next);
                              const nextAlerts = new Set(acknowledgedNotifications);
                              unacknowledgedRoleNotifications.forEach((item) => nextAlerts.add(item.key));
                              setAcknowledgedNotifications(nextAlerts);
                              persistAcknowledgedNotifications(nextAlerts);
                            }}
                          >
                            Mark all read
                          </button>
                        ) : null}
                        <button
                          className="text-xs text-muted-foreground hover:text-foreground"
                          onClick={() => {
                            loadModuleStatus();
                            loadRoleAlerts(true);
                          }}
                          disabled={notificationsLoading}
                        >
                          {notificationsLoading ? "Refreshing..." : "Refresh"}
                        </button>
                      </div>
                    </div>
                    <div className="mt-3 space-y-3 text-sm text-muted-foreground">
                      <div className="space-y-2">
                        <p className="text-[11px] uppercase tracking-[0.2em] text-muted-foreground">
                          Announcements
                        </p>
                        {statusLoading && disabledModules.length === 0 ? (
                          <div className="rounded-lg border border-border bg-muted/50 px-3 py-2 text-xs">
                            Loading module status...
                          </div>
                        ) : disabledModules.length === 0 ? (
                          <div className="rounded-lg border border-border bg-muted/50 px-3 py-2 text-xs">
                            No announcements.
                          </div>
                        ) : (
                          disabledModules.map((module) => (
                            <div
                              key={module.moduleKey}
                              className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2"
                            >
                              <p className="font-medium text-amber-800">
                                {module.displayName} is under maintenance
                              </p>
                              {module.notes ? (
                                <p className="mt-1 text-xs text-amber-700">{module.notes}</p>
                              ) : null}
                              <div className="mt-2 text-xs text-amber-700">
                                {!acknowledgedModules.has(module.moduleKey) ? (
                                  <button
                                    className="underline-offset-4 hover:underline"
                                    onClick={() => {
                                      const next = new Set(acknowledgedModules);
                                      next.add(module.moduleKey);
                                      setAcknowledgedModules(next);
                                      persistAcknowledged(next);
                                    }}
                                  >
                                    Mark as read
                                  </button>
                                ) : (
                                  <span>Marked as read</span>
                                )}
                              </div>
                            </div>
                          ))
                        )}
                      </div>

                      <div className="space-y-2">
                        <p className="text-[11px] uppercase tracking-[0.2em] text-muted-foreground">Activity</p>
                        {roleLoading && roleNotifications.length === 0 ? (
                          <div className="rounded-lg border border-border bg-muted/50 px-3 py-2 text-xs">
                            Loading activity...
                          </div>
                        ) : roleNotifications.length === 0 ? (
                          <div className="rounded-lg border border-border bg-muted/50 px-3 py-2 text-xs">
                            No new activity.
                          </div>
                        ) : (
                          roleNotifications.map((item) => (
                            <div
                              key={item.key}
                              className={cn(
                                "rounded-lg border px-3 py-2",
                                item.tone === "warning"
                                  ? "border-amber-200 bg-amber-50 text-amber-800"
                                  : "border-slate-200 bg-slate-50 text-slate-700"
                              )}
                            >
                              <div className="flex items-start justify-between gap-2">
                                <div>
                                  <p className="font-medium">{item.title}</p>
                                  {item.body ? (
                                    <p className="mt-1 text-xs opacity-80">{item.body}</p>
                                  ) : null}
                                </div>
                                {item.href ? (
                                  <button
                                    className="text-xs text-primary hover:underline"
                                    onClick={() => {
                                      setNotificationsOpen(false);
                                      if (item.href) {
                                        nav(item.href);
                                      }
                                    }}
                                  >
                                    View
                                  </button>
                                ) : null}
                              </div>
                              <div className="mt-2 text-xs">
                                {!acknowledgedNotifications.has(item.key) ? (
                                  <button
                                    className="underline-offset-4 hover:underline"
                                    onClick={() => {
                                      const next = new Set(acknowledgedNotifications);
                                      next.add(item.key);
                                      setAcknowledgedNotifications(next);
                                      persistAcknowledgedNotifications(next);
                                    }}
                                  >
                                    Mark as read
                                  </button>
                                ) : (
                                  <span>Marked as read</span>
                                )}
                              </div>
                            </div>
                          ))
                        )}
                      </div>
                    </div>
                  </div>
                ) : null}
              </div>
            </div>
          </div>
        </header>

        <main className="flex min-h-0 flex-1 flex-col overflow-y-auto overscroll-contain">
          <div key={location.pathname} className="px-4 py-5 md:px-6 md:py-6">
            <Outlet />
          </div>
        </main>
      </div>

      {open ? (
        <div className="fixed inset-0 z-50 bg-[#122442]/35 lg:hidden fade-in">
          <div className="absolute left-0 top-0 h-full w-60 border-r border-border bg-card p-5 fade-in">
            <div className="mb-6 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="grid h-9 w-9 place-items-center overflow-hidden rounded-lg bg-black"><img src={vaiaLogo} alt="VAIA emblem" className="h-full w-full object-contain" /></div>
                <div>
                  <p className="text-xs uppercase tracking-[0.08em] text-muted-foreground">Operations</p>
                  <p className="text-base font-bold">VAIA</p>
                </div>
              </div>
              <button onClick={() => setOpen(false)} className="text-sm text-muted-foreground">
                Close
              </button>
            </div>
            <nav className="max-h-[calc(100vh-8rem)] space-y-6 overflow-y-auto pr-1">
              {filteredSections.map((section) => (
                <div key={section.title}>
                  <p className="mb-2 text-[11px] uppercase tracking-[0.2em] text-muted-foreground">
                    {section.title}
                  </p>
                  <div className="space-y-1">
                    {section.items.map((item) => (
                      <NavLink
                        key={item.to}
                        to={item.to}
                        onClick={() => setOpen(false)}
                        className={({ isActive }) =>
                          cn(
                            "flex min-h-10 items-center justify-between rounded-lg px-3 py-2 text-sm font-medium text-muted-foreground transition-colors",
                            isActive
                              ? "bg-accent text-foreground"
                              : "hover:bg-muted hover:text-foreground"
                          )
                        }
                      >
                        <span>{item.label}</span>
                      </NavLink>
                    ))}
                  </div>
                </div>
              ))}
            </nav>
            <div className="mt-4 flex items-center gap-1 border-t border-border pt-3">
              <button
                type="button"
                onClick={() => { setOpen(false); nav("/settings"); }}
                className="flex min-w-0 flex-1 items-center gap-3 rounded-lg px-3 py-2 text-left hover:bg-muted"
              >
                <span className="grid h-8 w-8 place-items-center rounded-lg bg-muted text-xs font-bold text-foreground">{me.username.slice(0, 1).toUpperCase()}</span>
                <span className="min-w-0"><span className="block truncate text-sm font-semibold text-foreground">{me.username}</span><span className="block text-xs text-muted-foreground">{primaryRole}</span></span>
              </button>
              <button type="button" onClick={() => { logout(); nav("/login"); }} className="grid h-10 w-10 place-items-center rounded-lg text-muted-foreground hover:bg-muted hover:text-destructive" aria-label="Log out"><LogOut className="h-4 w-4" /></button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
    </TrackingProvider>
  );
}
