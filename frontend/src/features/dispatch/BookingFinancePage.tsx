import { useEffect, useState } from "react";
import { AlertTriangle, CheckCircle2, CreditCard, RefreshCw } from "lucide-react";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import { Button } from "@/components/ui/button";
import { getMe } from "@/features/auth/authStore";
import { api } from "@/lib/api";
import { useToast } from "@/lib/useToast";
import { useDispatchHub } from "@/hooks/useDispatchHub";

type BookingFinanceItem = {
  bookingId: string;
  bookingStatus: string;
  financeStatus: string;
  quotedAmount?: number | null;
  requiredDepositAmount?: number | null;
  verifiedPaymentAmount: number;
  verifiedDepositAmount: number;
  customerName?: string | null;
  bookingNumber?: string | null;
  pickupLocation: string;
  dropoffLocation: string;
  requestedPickupTime?: string | null;
  customerAccountStatus?: string | null;
  customerCreditStatus?: string | null;
  hasOverdueBalance: boolean;
};

export default function BookingFinancePage() {
  const { toasts, show } = useToast();
  const [items, setItems] = useState<BookingFinanceItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const isFinance = getMe()?.roles?.includes("HeadOfFinance") ?? false;

  const load = async () => {
    try {
      setLoading(true);
      setItems(await api<BookingFinanceItem[]>("/api/finance/bookings/pending", { method: "GET" }));
    } catch (error: any) {
      show(error?.message ?? "Unable to load bookings awaiting finance.", "error");
    } finally {
      setLoading(false);
    }
  };

  useDispatchHub({ onBookingFinanceChanged: () => { void load(); } });

  useEffect(() => { void load(); }, []);

  const post = async (item: BookingFinanceItem, action: string, body: object) => {
    try {
      setBusyId(item.bookingId);
      await api(`/api/finance/bookings/${item.bookingId}/${action}`, { method: "POST", body: JSON.stringify(body) });
      show("Finance decision recorded; planning eligibility was recalculated.", "success");
      await load();
    } catch (error: any) {
      show(error?.message ?? "Finance decision failed.", "error");
    } finally {
      setBusyId(null);
    }
  };

  const verifyPayment = (item: BookingFinanceItem) => {
    const amount = Number(window.prompt("Verified payment amount:", String(item.requiredDepositAmount ?? item.quotedAmount ?? "")));
    const referenceNumber = window.prompt("Payment reference number:")?.trim();
    const reason = window.prompt("Verification note:")?.trim();
    if (!Number.isFinite(amount) || amount <= 0 || !referenceNumber || !reason) return;
    const isDeposit = Boolean(item.requiredDepositAmount && amount < (item.quotedAmount ?? Number.MAX_SAFE_INTEGER));
    void post(item, "verify-payment", { amount, isDeposit, approveDepositForDispatch: isDeposit, paymentMethod: "BANK_TRANSFER", referenceNumber, reason });
  };

  const clearCredit = (item: BookingFinanceItem) => {
    const bookingAmountRaw = window.prompt("Booking amount for credit-limit evaluation:", String(item.quotedAmount ?? ""));
    const reason = window.prompt("Credit clearance note:")?.trim();
    if (!reason) return;
    const bookingAmount = bookingAmountRaw?.trim() ? Number(bookingAmountRaw) : null;
    if (bookingAmount !== null && (!Number.isFinite(bookingAmount) || bookingAmount < 0)) return;
    void post(item, "clear-credit", { bookingAmount, reason });
  };

  const reasonAction = (item: BookingFinanceItem, action: "authorize-exception" | "block") => {
    const reason = window.prompt(action === "block" ? "Reason for finance block:" : "Documented exception reason:")?.trim();
    if (reason) void post(item, action, { reason });
  };

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader title="Booking Finance Clearance" description="Verify prepaid funds or credit eligibility before a booking enters dispatch planning." actions={<Button variant="outline" onClick={() => void load()} disabled={loading}><RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />Refresh</Button>} />

      {loading && items.length === 0 ? <LoadingSkeleton rows={6} /> : items.length === 0 ? <EmptyState title="Finance queue is clear" description="Approved bookings awaiting payment or credit review will appear here." /> : (
        <div className="grid gap-4 xl:grid-cols-2">
          {items.map((item) => {
            const prepaid = item.customerAccountStatus === "ACTIVE_PREPAID";
            const credit = item.customerAccountStatus === "ACTIVE_CREDIT";
            return <article key={item.bookingId} className="surface-card p-5">
              <div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">{item.bookingNumber || item.bookingId.slice(0, 8).toUpperCase()}</p><h2 className="mt-1 text-lg font-semibold">{item.customerName ?? "Customer"}</h2><p className="mt-1 text-sm text-muted-foreground">{item.pickupLocation} → {item.dropoffLocation}</p></div><span className={`rounded-full border px-2.5 py-1 text-[10px] font-semibold uppercase ${item.hasOverdueBalance ? "border-destructive/30 bg-destructive/10 text-destructive" : "border-amber-500/30 bg-amber-500/10 text-amber-700"}`}>{item.hasOverdueBalance ? "Overdue block" : item.financeStatus.replace(/_/g, " ")}</span></div>
              <dl className="mt-5 grid grid-cols-2 gap-3 rounded-xl bg-muted/35 p-4 text-sm"><div><dt className="text-xs text-muted-foreground">Account</dt><dd className="mt-1 font-semibold">{item.customerAccountStatus?.replace(/_/g, " ") ?? "Unknown"}</dd></div><div><dt className="text-xs text-muted-foreground">Credit</dt><dd className="mt-1 font-semibold">{item.customerCreditStatus?.replace(/_/g, " ") ?? "Not granted"}</dd></div><div><dt className="text-xs text-muted-foreground">Quoted</dt><dd className="mt-1 font-mono font-semibold">{item.quotedAmount?.toLocaleString() ?? "Not set"}</dd></div><div><dt className="text-xs text-muted-foreground">Deposit required</dt><dd className="mt-1 font-mono font-semibold">{item.requiredDepositAmount?.toLocaleString() ?? "None"}</dd></div></dl>
              {item.hasOverdueBalance ? <p className="mt-4 flex items-center gap-2 text-xs font-medium text-destructive"><AlertTriangle className="h-4 w-4" />Credit clearance is blocked unless an authorized exception is recorded.</p> : null}
              <div className="mt-5 flex flex-wrap gap-2">{isFinance && prepaid ? <Button onClick={() => verifyPayment(item)} disabled={busyId === item.bookingId}><CreditCard className="h-4 w-4" />Verify payment</Button> : null}{isFinance && credit ? <Button onClick={() => clearCredit(item)} disabled={busyId === item.bookingId}><CheckCircle2 className="h-4 w-4" />Clear credit</Button> : null}<Button variant="outline" onClick={() => reasonAction(item, "authorize-exception")} disabled={busyId === item.bookingId}>Authorize exception</Button>{isFinance ? <Button variant="destructive" onClick={() => reasonAction(item, "block")} disabled={busyId === item.bookingId}>Block</Button> : null}</div>
            </article>;
          })}
        </div>
      )}
    </div>
  );
}
