import { useMemo, useState } from "react";
import {
  LayoutDashboard,
  Package,
  FileText,
  Truck,
  ShoppingCart
} from "lucide-react";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow
} from "@/components/ui/table";
import { cn } from "@/lib/utils";

type LoanRow = {
  id: string;
  borrower: string;
  asset: string;
  issuedAt: string;
  status: "OPEN" | "PARTIALLY_RETURNED" | "CLOSED" | "OVERDUE";
};

type PoRow = {
  id: string;
  supplier: string;
  total: string;
  createdAt: string;
  status: "PENDING" | "APPROVED" | "PARTIALLY_RECEIVED" | "CLOSED" | "OVERDUE";
};

const mockLoans: LoanRow[] = [
  { id: "LN-2045", borrower: "drv_demo", asset: "TRK-001", issuedAt: "Feb 21, 2026", status: "OPEN" },
  { id: "LN-2046", borrower: "drv_demo", asset: "TRK-002", issuedAt: "Feb 20, 2026", status: "PARTIALLY_RETURNED" },
  { id: "LN-2047", borrower: "drv_demo", asset: "TRA-001", issuedAt: "Feb 18, 2026", status: "OVERDUE" }
];

const mockPos: PoRow[] = [
  { id: "PO-1081", supplier: "Demo Supplier A", total: "$12,450", createdAt: "Feb 21, 2026", status: "PENDING" },
  { id: "PO-1082", supplier: "Demo Supplier B", total: "$7,900", createdAt: "Feb 20, 2026", status: "PARTIALLY_RECEIVED" },
  { id: "PO-1083", supplier: "Demo Supplier A", total: "$4,750", createdAt: "Feb 18, 2026", status: "APPROVED" }
];

const navItems = [
  { label: "Dashboard", icon: LayoutDashboard },
  { label: "Inventory", icon: Package },
  { label: "IO Queue", icon: FileText },
  { label: "Loans", icon: Truck },
  { label: "Purchase Orders", icon: ShoppingCart }
];

const badgeClass = (status: string) => {
  if (status === "APPROVED" || status === "CLOSED")
    return "bg-emerald-50 text-emerald-700 border-emerald-200";
  if (status === "PENDING" || status === "PARTIALLY_RECEIVED" || status === "PARTIALLY_RETURNED")
    return "bg-amber-50 text-amber-700 border-amber-200";
  if (status === "OVERDUE") return "bg-red-50 text-red-700 border-red-200";
  return "bg-slate-50 text-slate-600 border-slate-200";
};

export default function LoansPurchaseOrdersMockPage() {
  const [view, setView] = useState<"loans" | "purchaseOrders">("loans");
  const isLoans = view === "loans";

  const kpis = useMemo(() => {
    if (isLoans) {
      return [
        { label: "Active Loans", value: "12", icon: Truck },
        { label: "Overdue Items", value: "3", icon: FileText },
        { label: "Returns Pending", value: "5", icon: Package }
      ];
    }
    return [
      { label: "Pending POs", value: "4", icon: ShoppingCart },
      { label: "Received This Week", value: "9", icon: Package },
      { label: "Overdue Deliveries", value: "1", icon: FileText }
    ];
  }, [isLoans]);

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <div className="flex">
        <aside className="hidden w-64 flex-col gap-6 border-r border-slate-100 bg-white px-6 py-6 md:flex">
          <div>
            <p className="text-xs uppercase tracking-[0.2em] text-slate-400">NVG</p>
            <p className="text-lg font-semibold">ERP Portal</p>
          </div>
          <nav className="space-y-2">
            {navItems.map((item) => {
              const active =
                (item.label === "Loans" && isLoans) ||
                (item.label === "Purchase Orders" && !isLoans);
              const Icon = item.icon;
              return (
                <button
                  key={item.label}
                  onClick={() =>
                    item.label === "Loans"
                      ? setView("loans")
                      : item.label === "Purchase Orders"
                        ? setView("purchaseOrders")
                        : null
                  }
                  className={cn(
                    "flex w-full items-center gap-3 rounded-lg px-4 py-2 text-sm font-medium",
                    active
                      ? "bg-[#175C99] text-white"
                      : "text-slate-500 hover:bg-slate-50 hover:text-slate-900"
                  )}
                >
                  <Icon className={cn("h-4 w-4", active ? "text-white" : "text-[#175C99]")} />
                  {item.label}
                </button>
              );
            })}
          </nav>
        </aside>

        <main className="flex-1 px-6 py-8">
          <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
            <div>
              <h1 className="text-2xl font-semibold">
                {isLoans ? "Loans" : "Purchase Orders"}
              </h1>
              <p className="mt-1 text-sm text-slate-500">
                {isLoans
                  ? "Track borrowed assets and return status."
                  : "Monitor approvals, receipts, and supplier deliveries."}
              </p>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setView("loans")}
                className={cn(
                  "rounded-full px-4 py-2 text-sm font-semibold",
                  isLoans ? "bg-[#175C99] text-white" : "bg-white text-slate-500 shadow-sm"
                )}
              >
                Loans
              </button>
              <button
                onClick={() => setView("purchaseOrders")}
                className={cn(
                  "rounded-full px-4 py-2 text-sm font-semibold",
                  !isLoans ? "bg-[#175C99] text-white" : "bg-white text-slate-500 shadow-sm"
                )}
              >
                Purchase Orders
              </button>
            </div>
          </div>

          <div className="mb-6 grid gap-4 md:grid-cols-3">
            {kpis.map((kpi) => {
              const Icon = kpi.icon;
              return (
                <Card
                  key={kpi.label}
                  className="border-none bg-white p-5 shadow-[0_2px_10px_-3px_rgba(6,81,237,0.1)] rounded-xl"
                >
                  <div className="flex items-start justify-between">
                    <div>
                      <p className="text-sm text-slate-500">{kpi.label}</p>
                      <p className="mt-2 text-3xl font-bold text-slate-900">{kpi.value}</p>
                    </div>
                    <Icon className="h-5 w-5 text-[#175C99]" />
                  </div>
                </Card>
              );
            })}
          </div>

          <div className="rounded-xl border-none bg-white p-4 shadow-sm overflow-hidden">
            <Table>
              <TableHeader className="bg-slate-50/50 border-b border-slate-100">
                <TableRow>
                  {isLoans ? (
                    <>
                      <TableHead className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Loan</TableHead>
                      <TableHead className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Borrower</TableHead>
                      <TableHead className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Asset</TableHead>
                      <TableHead className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Issued</TableHead>
                      <TableHead className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Status</TableHead>
                    </>
                  ) : (
                    <>
                      <TableHead className="text-xs font-semibold text-slate-500 uppercase tracking-wider">PO</TableHead>
                      <TableHead className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Supplier</TableHead>
                      <TableHead className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Total</TableHead>
                      <TableHead className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Created</TableHead>
                      <TableHead className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Status</TableHead>
                    </>
                  )}
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoans
                  ? mockLoans.map((loan) => (
                      <TableRow key={loan.id}>
                        <TableCell className="font-medium">{loan.id}</TableCell>
                        <TableCell>{loan.borrower}</TableCell>
                        <TableCell>{loan.asset}</TableCell>
                        <TableCell>{loan.issuedAt}</TableCell>
                        <TableCell>
                          <Badge className={cn("rounded-full px-2.5 py-0.5 text-xs font-medium border", badgeClass(loan.status))}>
                            {loan.status}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))
                  : mockPos.map((po) => (
                      <TableRow key={po.id}>
                        <TableCell className="font-medium">{po.id}</TableCell>
                        <TableCell>{po.supplier}</TableCell>
                        <TableCell>{po.total}</TableCell>
                        <TableCell>{po.createdAt}</TableCell>
                        <TableCell>
                          <Badge className={cn("rounded-full px-2.5 py-0.5 text-xs font-medium border", badgeClass(po.status))}>
                            {po.status}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))}
              </TableBody>
            </Table>
          </div>
        </main>
      </div>
    </div>
  );
}
