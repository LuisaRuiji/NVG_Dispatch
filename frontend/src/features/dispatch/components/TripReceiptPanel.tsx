import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Plus, Printer, ReceiptText, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { api } from "@/lib/api";
import type { DispatchTripDetail, GenerateTripReceiptPayload, TripReceipt } from "../types";

type ChargeDraft = { id: string; description: string; amount: string };
type DiscountMode = "amount" | "percent";

const deliveredStatuses = new Set(["DELIVERY_COMPLETED", "DOCUMENTS_PENDING", "OPERATIONALLY_CLOSED", "DELIVERED", "CLOSED"]);
const money = new Intl.NumberFormat("en-PH", { style: "currency", currency: "PHP" });
const inputClasses = "h-10 rounded-md border border-input bg-background px-3 text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring";

function amount(value: string) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function optionalAmount(value: string) {
  if (!value.trim()) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

export default function TripReceiptPanel({ trip, canGenerate }: { trip: DispatchTripDetail; canGenerate: boolean }) {
  const [receiptNumber, setReceiptNumber] = useState(trip.financials?.officialReceiptNumber ?? "");
  const [baseCharge, setBaseCharge] = useState(trip.financials?.rate?.toString() ?? "");
  const [charges, setCharges] = useState<ChargeDraft[]>([]);
  const [discountMode, setDiscountMode] = useState<DiscountMode>("amount");
  const [discountValue, setDiscountValue] = useState("");
  const [discountReason, setDiscountReason] = useState("");
  const [taxAmount, setTaxAmount] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("");
  const [paymentReference, setPaymentReference] = useState("");
  const [notes, setNotes] = useState("");
  const [receipt, setReceipt] = useState<TripReceipt | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setReceiptNumber(trip.financials?.officialReceiptNumber ?? "");
    setBaseCharge(trip.financials?.rate?.toString() ?? "");
  }, [trip.id, trip.financials?.officialReceiptNumber, trip.financials?.rate]);

  const liveTotals = useMemo(() => {
    const base = amount(baseCharge);
    const additional = charges.reduce((sum, item) => sum + amount(item.amount), 0);
    const subtotal = base + additional;
    const rawDiscount = amount(discountValue);
    const discount = discountMode === "percent" ? subtotal * Math.min(rawDiscount, 100) / 100 : rawDiscount;
    const tax = amount(taxAmount);
    return { subtotal, discount, tax, total: Math.max(0, subtotal - discount + tax) };
  }, [baseCharge, charges, discountMode, discountValue, taxAmount]);

  const eligible = deliveredStatuses.has(trip.status);

  const addCharge = () => {
    setCharges((items) => [...items, { id: `${Date.now()}-${items.length}`, description: "", amount: "" }]);
  };

  const generateReceipt = async () => {
    setError(null);
    if (!eligible) {
      setError("Complete the delivery before generating a receipt.");
      return;
    }
    if (!receiptNumber.trim()) {
      setError("Enter the official receipt number.");
      return;
    }
    if (amount(baseCharge) <= 0) {
      setError("Enter a positive base charge.");
      return;
    }
    if (charges.some((item) => !item.description.trim() || amount(item.amount) <= 0)) {
      setError("Complete or remove each additional charge line.");
      return;
    }

    const payload: GenerateTripReceiptPayload = {
      receiptNumber: receiptNumber.trim(),
      baseCharge: amount(baseCharge),
      additionalCharges: charges.map((item) => ({ description: item.description.trim(), amount: amount(item.amount) })),
      discountAmount: discountMode === "amount" ? optionalAmount(discountValue) : null,
      discountPercent: discountMode === "percent" ? optionalAmount(discountValue) : null,
      discountReason: discountReason.trim() || null,
      taxAmount: optionalAmount(taxAmount),
      paymentMethod: paymentMethod || null,
      paymentReference: paymentReference.trim() || null,
      notes: notes.trim() || null
    };

    setPending(true);
    try {
      const generated = await api<TripReceipt>(`/api/dispatch/trips/${trip.id}/receipt`, {
        method: "POST",
        body: JSON.stringify(payload)
      });
      setReceipt(generated);
    } catch (requestError: any) {
      setError(requestError?.message ?? "Receipt generation failed. Review the amounts and try again.");
    } finally {
      setPending(false);
    }
  };

  const printReceipt = () => {
    document.body.classList.add("printing-trip-receipt");
    window.print();
    document.body.classList.remove("printing-trip-receipt");
  };

  if (!canGenerate) return null;

  return (
    <section className="surface-card overflow-hidden" aria-labelledby="receipt-calculator-title">
      <div className="flex flex-col gap-3 border-b border-border px-5 py-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <ReceiptText className="h-5 w-5 text-primary" aria-hidden="true" />
            <h2 id="receipt-calculator-title" className="text-lg font-semibold text-foreground">Trip charge receipt</h2>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">Calculate the final charge and create a printable receipt. This does not process payments.</p>
        </div>
        <span className={`w-fit rounded-md border px-2.5 py-1 text-xs font-semibold ${eligible ? "border-emerald-200 bg-emerald-50 text-emerald-700" : "border-amber-200 bg-amber-50 text-amber-800"}`}>
          {eligible ? "Ready to calculate" : "Delivery required"}
        </span>
      </div>

      <div className="grid lg:grid-cols-[minmax(0,1fr)_280px]">
        <div className="space-y-5 p-5">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Official receipt number" required>
              <input className={`${inputClasses} w-full`} value={receiptNumber} maxLength={80} onChange={(event) => setReceiptNumber(event.target.value)} placeholder="e.g. OR-2026-00142" />
            </Field>
            <Field label="Base trip charge" required>
              <MoneyInput value={baseCharge} onChange={setBaseCharge} placeholder="0.00" />
            </Field>
          </div>

          <div>
            <div className="flex items-center justify-between gap-3">
              <div>
                <h3 className="text-sm font-semibold text-foreground">Additional charges</h3>
                <p className="text-xs text-muted-foreground">Optional tolls, handling, waiting time, or other agreed charges.</p>
              </div>
              <Button type="button" variant="outline" size="sm" onClick={addCharge}><Plus className="h-4 w-4" />Add charge</Button>
            </div>
            {charges.length > 0 ? (
              <div className="mt-3 space-y-2">
                {charges.map((charge) => (
                  <div key={charge.id} className="grid gap-2 sm:grid-cols-[minmax(0,1fr)_160px_40px]">
                    <input aria-label="Charge description" className={`${inputClasses} w-full`} maxLength={120} value={charge.description} onChange={(event) => setCharges((items) => items.map((item) => item.id === charge.id ? { ...item, description: event.target.value } : item))} placeholder="Charge description" />
                    <MoneyInput label="Charge amount" value={charge.amount} onChange={(value) => setCharges((items) => items.map((item) => item.id === charge.id ? { ...item, amount: value } : item))} placeholder="0.00" />
                    <Button type="button" variant="ghost" size="icon" aria-label="Remove charge" onClick={() => setCharges((items) => items.filter((item) => item.id !== charge.id))}><Trash2 className="h-4 w-4" /></Button>
                  </div>
                ))}
              </div>
            ) : null}
          </div>

          <div className="grid gap-4 border-t border-border pt-5 md:grid-cols-3">
            <Field label="Discount type">
              <select className={`${inputClasses} w-full`} value={discountMode} onChange={(event) => { setDiscountMode(event.target.value as DiscountMode); setDiscountValue(""); }}>
                <option value="amount">Fixed amount</option>
                <option value="percent">Percentage</option>
              </select>
            </Field>
            <Field label={discountMode === "percent" ? "Discount percentage" : "Discount amount"}>
              <MoneyInput value={discountValue} onChange={setDiscountValue} placeholder={discountMode === "percent" ? "0–100" : "0.00"} />
            </Field>
            <Field label="Discount reason">
              <input className={`${inputClasses} w-full`} value={discountReason} maxLength={300} onChange={(event) => setDiscountReason(event.target.value)} placeholder="Optional" />
            </Field>
            <Field label="Tax amount">
              <MoneyInput value={taxAmount} onChange={setTaxAmount} placeholder="Optional" />
            </Field>
            <Field label="Payment method">
              <select className={`${inputClasses} w-full`} value={paymentMethod} onChange={(event) => setPaymentMethod(event.target.value)}>
                <option value="">Not specified</option>
                <option value="Cash">Cash</option>
                <option value="Bank transfer">Bank transfer</option>
                <option value="Check">Check</option>
                <option value="GCash">GCash</option>
                <option value="Other">Other</option>
              </select>
            </Field>
            <Field label="Payment reference">
              <input className={`${inputClasses} w-full`} value={paymentReference} maxLength={120} onChange={(event) => setPaymentReference(event.target.value)} placeholder="Optional" />
            </Field>
          </div>

          <Field label="Receipt notes">
            <textarea className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring" value={notes} maxLength={600} onChange={(event) => setNotes(event.target.value)} placeholder="Optional customer-facing note" />
          </Field>

          {error ? <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800" role="alert">{error}</p> : null}
        </div>

        <aside className="border-t border-border bg-muted/30 p-5 lg:border-l lg:border-t-0" aria-label="Receipt total">
          <p className="text-xs font-semibold uppercase tracking-[0.08em] text-muted-foreground">Calculated total</p>
          <p className="mt-2 text-3xl font-bold tabular-nums text-foreground">{money.format(liveTotals.total)}</p>
          <dl className="mt-5 space-y-2 border-y border-border py-4 text-sm">
            <TotalLine label="Subtotal" value={liveTotals.subtotal} />
            <TotalLine label="Discount" value={-liveTotals.discount} />
            <TotalLine label="Tax" value={liveTotals.tax} />
          </dl>
          <Button className="mt-5 w-full" disabled={pending || !eligible} onClick={() => void generateReceipt()}>{pending ? "Generating…" : "Generate receipt"}</Button>
          <p className="mt-2 text-xs text-muted-foreground">Amounts are recalculated and validated by the server.</p>
        </aside>
      </div>

      {receipt ? <ReceiptPreview receipt={receipt} onPrint={printReceipt} /> : null}
    </section>
  );
}

function Field({ label, required = false, children }: { label: string; required?: boolean; children: ReactNode }) {
  return <label className="block text-sm font-medium text-foreground"><span className="mb-1.5 block">{label}{required ? <span className="text-primary"> *</span> : null}</span>{children}</label>;
}

function MoneyInput({ value, onChange, placeholder, label }: { value: string; onChange: (value: string) => void; placeholder: string; label?: string }) {
  return <input aria-label={label} type="number" min="0" step="0.01" inputMode="decimal" className={`${inputClasses} w-full tabular-nums`} value={value} onChange={(event) => onChange(event.target.value)} placeholder={placeholder} />;
}

function TotalLine({ label, value }: { label: string; value: number }) {
  return <div className="flex items-center justify-between gap-4"><dt className="text-muted-foreground">{label}</dt><dd className="font-medium tabular-nums text-foreground">{money.format(value)}</dd></div>;
}

function ReceiptPreview({ receipt, onPrint }: { receipt: TripReceipt; onPrint: () => void }) {
  return (
    <div className="trip-receipt-print border-t border-border bg-background p-5 sm:p-7">
      <div className="no-print mb-5 flex items-center justify-between gap-3">
        <div><h3 className="font-semibold text-foreground">Receipt ready</h3><p className="text-sm text-muted-foreground">Review the calculated copy before printing.</p></div>
        <Button variant="outline" onClick={onPrint}><Printer className="h-4 w-4" />Print receipt</Button>
      </div>
      <article className="mx-auto max-w-3xl border border-border bg-white p-6 text-slate-900 sm:p-8" aria-label={`Receipt ${receipt.receiptNumber}`}>
        <header className="flex items-start justify-between gap-5 border-b border-slate-300 pb-5">
          <div><p className="text-xs font-bold uppercase tracking-[0.12em] text-slate-500">VAIA dispatch operations</p><h3 className="mt-1 text-2xl font-bold text-foreground">Official receipt</h3></div>
          <div className="text-right text-sm"><p className="font-mono font-bold">{receipt.receiptNumber}</p><p className="mt-1 text-slate-500">{new Date(receipt.generatedAt).toLocaleString()}</p></div>
        </header>
        <dl className="grid gap-x-8 gap-y-3 border-b border-slate-200 py-5 text-sm sm:grid-cols-2">
          <ReceiptField label="Received from" value={receipt.customerName} />
          <ReceiptField label="Trip" value={receipt.tripId.slice(0, 8).toUpperCase()} mono />
          <ReceiptField label="Route" value={[receipt.pickupLocation, receipt.dropoffLocation].filter(Boolean).join(" → ") || "Not specified"} />
          <ReceiptField label="Container / booking" value={[receipt.containerNumber, receipt.bookingNumber].filter(Boolean).join(" / ") || "Not specified"} mono />
        </dl>
        <table className="w-full border-collapse text-sm">
          <thead><tr className="border-b border-slate-300 text-left text-xs uppercase tracking-wide text-slate-500"><th className="py-3 font-semibold">Description</th><th className="py-3 text-right font-semibold">Amount</th></tr></thead>
          <tbody>
            <tr className="border-b border-slate-200"><td className="py-3">Base trip charge</td><td className="py-3 text-right tabular-nums">{money.format(receipt.baseCharge)}</td></tr>
            {receipt.additionalCharges.map((charge, index) => <tr key={`${charge.description}-${index}`} className="border-b border-slate-200"><td className="py-3">{charge.description}</td><td className="py-3 text-right tabular-nums">{money.format(charge.amount)}</td></tr>)}
          </tbody>
        </table>
        <dl className="ml-auto mt-4 w-full max-w-xs space-y-2 text-sm">
          <TotalLine label="Subtotal" value={receipt.subtotal} />
          {receipt.discountAmount > 0 ? <TotalLine label={`Discount${receipt.discountPercent ? ` (${receipt.discountPercent}%)` : ""}`} value={-receipt.discountAmount} /> : null}
          {receipt.taxAmount > 0 ? <TotalLine label="Tax" value={receipt.taxAmount} /> : null}
          <div className="flex items-center justify-between border-t border-slate-400 pt-3 text-base"><dt className="font-bold">Total</dt><dd className="font-bold tabular-nums">{money.format(receipt.total)}</dd></div>
        </dl>
        {(receipt.discountReason || receipt.paymentMethod || receipt.paymentReference || receipt.notes) ? <div className="mt-6 grid gap-2 border-t border-slate-200 pt-4 text-sm sm:grid-cols-2"><ReceiptField label="Discount note" value={receipt.discountReason} /><ReceiptField label="Payment" value={[receipt.paymentMethod, receipt.paymentReference].filter(Boolean).join(" · ") || null} /><ReceiptField label="Notes" value={receipt.notes} /></div> : null}
      </article>
    </div>
  );
}

function ReceiptField({ label, value, mono = false }: { label: string; value?: string | null; mono?: boolean }) {
  if (!value) return null;
  return <div><dt className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</dt><dd className={`mt-1 font-medium ${mono ? "font-mono" : ""}`}>{value}</dd></div>;
}
