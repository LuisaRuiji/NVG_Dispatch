import { useEffect, useMemo, useState, type ComponentType } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { getMe, logout } from "@/features/auth/authStore";
import { cn } from "@/lib/utils";
import nvgLogo from "@/assets/nvg-logo.png";
import ToastHost from "@/components/ToastHost";
import { useToast } from "@/lib/useToast";
import { onToast } from "@/lib/toastBus";
import {
  LayoutDashboard,
  Package,
  FileText,
  Truck,
  ShoppingCart,
  ShieldCheck,
  ClipboardList,
  ClipboardCheck,
  User
} from "lucide-react";

type NavItem = {
  label: string;
  to: string;
  roles?: string[];
  icon: ComponentType<{ className?: string }>;
};

const navSections: { title: string; items: NavItem[] }[] = [
  {
    title: "Core",
    items: [
      { label: "Dashboard", to: "/dashboard", roles: ["InventoryOfficer", "Manager", "HeadOfFinance", "CEO", "Driver"], icon: LayoutDashboard },
      { label: "Inventory", to: "/inventory", roles: ["InventoryOfficer", "Manager"], icon: Package }
    ]
  },
  {
    title: "Requests",
    items: [
      { label: "My Requests", to: "/my/requests", roles: ["Driver"], icon: User },
      { label: "IO Queue", to: "/queue/io", roles: ["InventoryOfficer"], icon: ClipboardList },
      { label: "Manager Queue", to: "/queue/manager", roles: ["Manager"], icon: ClipboardCheck },
      { label: "Awaiting Issue", to: "/queue/issue", roles: ["InventoryOfficer"], icon: FileText }
    ]
  },
  {
    title: "Operations",
    items: [
      { label: "Loans", to: "/loans", roles: ["InventoryOfficer", "Manager"], icon: Truck },
      { label: "Purchase Orders", to: "/purchase-orders", roles: ["InventoryOfficer", "Manager", "HeadOfFinance", "CEO"], icon: ShoppingCart }
    ]
  },
  {
    title: "Reports",
    items: [
      { label: "Reports", to: "/reports", roles: ["InventoryOfficer", "Manager", "HeadOfFinance", "CEO", "Admin", "SuperAdmin"], icon: FileText }
    ]
  },
  {
    title: "Admin",
    items: [
      { label: "Users", to: "/admin/users", roles: ["Admin", "SuperAdmin"], icon: User },
      { label: "Integrity", to: "/admin/integrity", roles: ["Manager", "HeadOfFinance", "CEO", "Admin", "SuperAdmin"], icon: ShieldCheck }
    ]
  }
];

export default function AppLayout() {
  const me = getMe();
  const nav = useNavigate();
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const { toasts, show } = useToast();

  const roles = me?.roles ?? [];
  const filteredSections = useMemo(
    () =>
      navSections
        .map((section) => ({
          ...section,
          items: section.items.filter((item) =>
            item.roles ? item.roles.some((role) => roles.includes(role)) : true
          )
        }))
        .filter((section) => section.items.length > 0),
    [roles]
  );

  useEffect(() => {
    return onToast((detail) => {
      show(detail.message, detail.type ?? "error");
    });
  }, [show]);

  if (!me) {
    nav("/login");
    return null;
  }

  return (
    <div className="flex h-screen w-full bg-slate-50 text-slate-900">
      <ToastHost toasts={toasts} />
      <aside className="group hidden h-full w-16 flex-col overflow-hidden border-r border-slate-200 bg-white transition-[width] duration-200 ease-out hover:w-64 md:flex">
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

        <nav className="flex-1 space-y-6">
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
                        "mx-3 my-1 flex h-10 items-center justify-center gap-3 rounded-xl px-3 text-sm font-medium transition-colors nav-link-shift group-hover:justify-start",
                        isActive
                          ? "bg-[#175C99] text-white shadow-md"
                          : "text-slate-500 hover:bg-slate-100"
                      )
                    }
                  >
                    {({ isActive }) => (
                      <>
                        <item.icon
                          className={cn(
                            "h-4 w-4 shrink-0",
                            isActive ? "text-white" : "text-[#175C99]"
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

        <div className="mx-4 my-6 hidden rounded-xl border border-slate-200 bg-slate-50 p-3 text-xs text-slate-500 opacity-0 transition-opacity duration-200 group-hover:block group-hover:opacity-100">
          Logged in as <span className="font-semibold text-slate-900">{me.username}</span>
        </div>
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
          <div className="hidden items-center gap-3 md:flex">
            <div className="relative">
              <input
                placeholder="Search by request / PO id"
                className="h-9 w-72 rounded-lg border border-slate-200 bg-white px-3 text-sm focus:outline-none focus:ring-2 focus:ring-primary/40"
              />
            </div>
          </div>

          <div className="flex items-center gap-4 text-sm">
            <span className="hidden text-slate-500 md:block">{me.username}</span>
            <button
              className="rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-500 hover:text-slate-900"
              onClick={() => {
                logout();
                nav("/login");
              }}
            >
              Logout
            </button>
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
            <nav className="space-y-6">
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
    </div>
  );
}
