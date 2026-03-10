import { useEffect, useMemo, useRef, useState, type ComponentType } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { getMe, logout } from "@/features/auth/authStore";
import { cn } from "@/lib/utils";
import nvgLogo from "@/assets/nvg-logo.png";
import ToastHost from "@/components/ToastHost";
import AccountSettingsModal from "@/components/AccountSettingsModal";
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
  Bell
} from "lucide-react";

type NavItem = {
  label: string;
  to: string;
  roles?: string[];
  moduleKey?: string;
  icon: ComponentType<{ className?: string }>;
};

const navSections: { title: string; items: NavItem[] }[] = [
  {
    title: "Core",
    items: [
      { label: "Inventory Dashboard", to: "/dashboard", roles: ["InventoryOfficer", "Manager", "HeadOfFinance", "CEO", "Driver"], icon: LayoutDashboard },
      { label: "Inventory", to: "/inventory", roles: ["InventoryOfficer", "Manager"], icon: Package, moduleKey: "inventory" }
    ]
  },
  {
    title: "Requests",
    items: [
      { label: "My Requests", to: "/my/requests", roles: ["Driver"], icon: User, moduleKey: "requests" },
      { label: "IO Queue", to: "/queue/io", roles: ["InventoryOfficer"], icon: ClipboardList, moduleKey: "requests" },
      { label: "Manager Queue", to: "/queue/manager", roles: ["Manager"], icon: ClipboardCheck, moduleKey: "requests" },
      { label: "Awaiting Issue", to: "/queue/issue", roles: ["InventoryOfficer"], icon: FileText, moduleKey: "requests" }
    ]
  },
  {
    title: "Operations",
    items: [
      { label: "Loans", to: "/loans", roles: ["InventoryOfficer", "Manager"], icon: Truck, moduleKey: "loans" },
      { label: "Purchase Orders", to: "/purchase-orders", roles: ["InventoryOfficer", "Manager", "HeadOfFinance", "CEO"], icon: ShoppingCart, moduleKey: "purchase-orders" }
    ]
  },
  {
    title: "Dispatch",
    items: [
      { label: "Dispatch Board", to: "/dispatch/board", roles: ["Manager", "Dispatcher", "CEO"], icon: Truck, moduleKey: "dispatch" },
      { label: "Requests", to: "/dispatch/requests", roles: ["Manager", "Dispatcher"], icon: ClipboardList, moduleKey: "dispatch" },
      { label: "Trips", to: "/dispatch/trips", roles: ["Manager", "Dispatcher", "CEO"], icon: Truck, moduleKey: "dispatch" },
      { label: "Documents", to: "/dispatch/documents", roles: ["Manager", "HeadOfFinance"], icon: FileText, moduleKey: "dispatch" },
      { label: "My Trips", to: "/dispatch/my-trips", roles: ["Driver"], icon: Truck, moduleKey: "dispatch" }
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
      { label: "Reports", to: "/reports", roles: ["InventoryOfficer", "Manager", "HeadOfFinance", "CEO", "Admin", "SuperAdmin"], icon: FileText, moduleKey: "reports" }
    ]
  },
  {
    title: "Admin",
    items: [
      { label: "Customers", to: "/admin/customers", roles: ["Manager", "Dispatcher"], icon: User, moduleKey: "users" },
      { label: "Modules", to: "/admin/modules", roles: ["SuperAdmin"], icon: Settings },
      { label: "Audit Logs", to: "/admin/audit-logs", roles: ["Manager", "HeadOfFinance", "CEO", "Admin", "SuperAdmin"], icon: FileText, moduleKey: "reports" },
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
  const [accountOpen, setAccountOpen] = useState(false);
  const accountRef = useRef<HTMLDivElement | null>(null);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [notificationsOpen, setNotificationsOpen] = useState(false);
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
  const rolePriority = [
    "SuperAdmin",
    "Admin",
    "Manager",
    "Dispatcher",
    "HeadOfFinance",
    "CEO",
    "InventoryOfficer",
    "Driver"
  ];
  const primaryRole =
    rolePriority.find((role) => roles.includes(role)) ?? roles[0] ?? "User";
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
            (item.roles ? item.roles.some((role) => roles.includes(role)) : true) &&
            (item.moduleKey ? moduleEnabledMap.get(item.moduleKey) !== false : true)
          )
        }))
        .filter((section) => section.items.length > 0),
    [roles, moduleEnabledMap]
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
        setNotificationsOpen(true);
        window.sessionStorage.setItem(key, unreadKeys);
      }
    }
  }, [me, statusLoaded, unacknowledgedDisabled.length, unacknowledgedRoleNotifications.length, unreadKeys]);

  useEffect(() => {
    if (!accountOpen && !notificationsOpen) return;
    const handler = (event: MouseEvent) => {
      const target = event.target as Node | null;
      if (
        accountRef.current &&
        target &&
        !accountRef.current.contains(target) &&
        notificationRef.current &&
        !notificationRef.current.contains(target)
      ) {
        setAccountOpen(false);
        setNotificationsOpen(false);
      }
    };
    window.addEventListener("click", handler);
    return () => window.removeEventListener("click", handler);
  }, [accountOpen, notificationsOpen]);

  if (!me) {
    nav("/login");
    return null;
  }

  return (
    <div className="flex h-screen w-full bg-slate-50 text-slate-900">
      <ToastHost toasts={toasts} />
      <aside className="group hidden h-full w-16 flex-col overflow-hidden border-r border-border bg-white/80 backdrop-blur-xl transition-[width] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] hover:w-64 md:flex dark:bg-black/60 shadow-sm z-10">
        <div className="px-2 py-6">
          <div className="flex items-center justify-center group-hover:justify-start">
            <div className="grid h-10 w-10 place-items-center overflow-hidden rounded-2xl bg-white shadow-sm">
              <img src={nvgLogo} alt="NVG" className="h-8 w-8 object-contain" />
            </div>
            <div className="ml-3 hidden flex-col opacity-0 transition-opacity duration-200 group-hover:flex group-hover:opacity-100">
              <p className="text-xs uppercase tracking-[0.2em] text-slate-400">NVG</p>
              <p className="text-lg font-semibold text-slate-900">ERP Portal</p>
            </div>
          </div>
        </div>

        <nav className="flex-1 min-h-0 space-y-6 overflow-y-auto">
          {filteredSections.map((section) => (
            <div key={section.title}>
              <p className="mb-2 px-6 text-[11px] uppercase tracking-[0.2em] text-slate-400 opacity-0 transition-opacity duration-200 group-hover:opacity-100">
                {section.title}
              </p>
              <div className="space-y-1">
                {section.items.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    className={({ isActive }) =>
                      cn(
                        "mx-3 my-1 flex h-10 items-center justify-center gap-3 rounded-xl px-3 text-sm font-medium transition-all duration-200 nav-link-shift group-hover:justify-start",
                        isActive
                          ? "bg-primary text-primary-foreground shadow-md shadow-primary/20"
                          : "text-muted-foreground hover:bg-muted/80 hover:text-foreground hover:shadow-sm"
                      )
                    }
                  >
                    {({ isActive }) => (
                      <>
                        <item.icon
                          className={cn(
                            "h-5 w-5 shrink-0 transition-colors duration-200",
                            isActive ? "text-primary-foreground" : "text-primary/70"
                          )}
                        />
                        <span className="hidden whitespace-nowrap opacity-0 transition-opacity duration-200 group-hover:inline group-hover:opacity-100">
                          {item.label}
                        </span>
                      </>
                    )}
                  </NavLink>
                ))}
              </div>
            </div>
          ))}
        </nav>

        <div className="h-6" />
      </aside>

      <div className="flex flex-1 flex-col h-screen overflow-y-auto">
        <header className="h-16 border-b border-slate-200 bg-white px-8 flex items-center justify-between">
          <div className="flex items-center gap-3 md:hidden">
            <button
              className="grid h-9 w-9 place-items-center rounded-lg border border-slate-200 bg-white"
              onClick={() => setOpen(true)}
              aria-label="Open navigation"
            >
              <img src={nvgLogo} alt="NVG" className="h-5 w-5 object-contain" />
            </button>
          </div>
          {canSeeSearch ? (
            <div className="hidden items-center gap-3 md:flex">
              <div className="relative group/search">
                <input
                  placeholder="Search by request / PO id"
                  className="h-9 w-72 rounded-full border border-border bg-muted/30 px-4 text-sm transition-all duration-200 hover:bg-muted/50 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:bg-background shadow-sm"
                />
                <kbd className="pointer-events-none absolute right-2.5 top-2 hidden h-5 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium opacity-100 sm:flex text-muted-foreground">
                  <span className="text-xs">⌘</span>K
                </kbd>
              </div>
            </div>
          ) : null}

          <div className="ml-auto flex items-center gap-4 text-sm">
            <div className="hidden items-center gap-2 md:flex">
              {me.username.toLowerCase() !== primaryRole.toLowerCase() ? (
                <span className="text-slate-500">{me.username}</span>
              ) : null}
              <div className="relative" ref={notificationRef}>
                <button
                  type="button"
                  className="grid h-9 w-9 place-items-center rounded-full border border-border bg-muted/30 text-muted-foreground hover:text-foreground"
                  onClick={() => setNotificationsOpen((value) => !value)}
                  aria-label="Notifications"
                >
                  <Bell className="h-4 w-4" />
                  {hasUnread ? (
                    <span className="absolute right-1 top-1 h-2 w-2 rounded-full bg-rose-500" />
                  ) : null}
                </button>
                {notificationsOpen ? (
                  <div className="absolute right-0 mt-2 w-72 rounded-xl border border-border bg-card p-3 shadow-card z-50 fade-in glass">
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
              <div className="relative" ref={accountRef}>
                <button
                  type="button"
                  className="rounded-full border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs font-semibold text-slate-600 hover:text-slate-900"
                  onClick={() => setAccountOpen((value) => !value)}
                >
                  {primaryRole}
                </button>
                {accountOpen ? (
                  <div className="absolute right-0 mt-2 w-48 rounded-xl border border-border bg-card p-2 shadow-card z-50 fade-in glass">
                    <button
                      className="w-full rounded-lg px-3 py-2 text-left text-sm font-medium text-foreground transition-colors hover:bg-muted hover:text-primary"
                      onClick={() => {
                        setAccountOpen(false);
                        setSettingsOpen(true);
                      }}
                    >
                      Settings
                    </button>
                    <div className="my-1 h-px bg-border/50" />
                    <button
                      className="w-full rounded-lg px-3 py-2 text-left text-sm font-medium text-destructive transition-colors hover:bg-destructive/10"
                      onClick={() => {
                        setAccountOpen(false);
                        logout();
                        nav("/login");
                      }}
                    >
                      Logout
                    </button>
                  </div>
                ) : null}
              </div>
            </div>
          </div>
        </header>

        <main className="flex-1 flex flex-col h-screen overflow-y-auto">
          <div key={location.pathname} className="px-8 py-8 fade-up">
            <Outlet />
          </div>
        </main>
      </div>

      {open ? (
        <div className="fixed inset-0 z-50 bg-black/40 md:hidden fade-in">
          <div className="absolute left-0 top-0 h-full w-72 bg-white p-6 shadow-lg fade-up">
            <div className="mb-6 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="h-9 w-9 rounded-xl bg-nvg-gradient" />
                <div>
                  <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground">NVG</p>
                  <p className="text-base font-semibold">ERP Portal</p>
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
                            "flex items-center justify-between rounded-lg px-3 py-2 text-sm font-medium text-muted-foreground transition nav-link-shift",
                            isActive
                              ? "bg-primary/10 text-primary"
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
          </div>
        </div>
      ) : null}

      {me ? (
        <AccountSettingsModal
          open={settingsOpen}
          onClose={() => setSettingsOpen(false)}
          username={me.username}
          userId={me.userId}
          roles={roles}
          primaryRole={primaryRole}
        />
      ) : null}
    </div>
  );
}
