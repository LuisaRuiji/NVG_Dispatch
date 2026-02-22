import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import StatusBadge from "@/components/StatusBadge";
import EmptyState from "@/components/EmptyState";
import { clearDashboardKpiCache } from "@/features/dashboard/kpis";
import { handleApiError } from "@/lib/apiError";
import Spinner from "@/components/Spinner";

type LoanLine = {
  id: string;
  inventoryName: string;
  qtyIssued: number;
  qtyReturned: number;
  returns: {
    id: string;
    qtyReturned: number;
    condition: string;
    receivedByUserId: string;
    receivedByUsername?: string | null;
    returnedAt: string;
  }[];
};

type LoanDetail = {
  id: string;
  status: string;
  borrowerUsername: string;
  lines: LoanLine[];
  history?: {
    type: string;
    actor?: string | null;
    decision?: string | null;
    step?: number | null;
    movementType?: string | null;
    qty?: number | null;
    condition?: string | null;
    timestamp: string;
  }[];
};

type ReturnLineInput = {
  loanLineId: string;
  qtyReturnedIncrement: number;
  condition: "GOOD" | "DAMAGED" | "LOST";
};

export default function LoanDetailPage() {
  const nav = useNavigate();
  const { id } = useParams();
  const me = getMe();
  const { toasts, show } = useToast();

  const [loan, setLoan] = useState<LoanDetail | null>(null);
  const [returns, setReturns] = useState<ReturnLineInput[]>([]);
  const [loading, setLoading] = useState(false);

  const fetchLoan = async () => {
    if (!id) return;
    const detail = await api<LoanDetail>(`/api/loans/${id}`, { method: "GET" });
    setLoan(detail);
    setReturns(
      detail.lines.map((line) => ({
        loanLineId: line.id,
        qtyReturnedIncrement: 0,
        condition: "GOOD"
      }))
    );
  };

  useEffect(() => {
    if (!me) {
      nav("/login");
      return;
    }
    if (!me.roles?.includes("InventoryOfficer") && !me.roles?.includes("Manager")) {
      show("Access denied.", "error");
      return;
    }
    if (!id) {
      show("Missing loan id.", "error");
      return;
    }

    (async () => {
      try {
        await fetchLoan();
      } catch (e: any) {
        handleApiError(e, show);
      }
    })();
  }, [id]);

  const remainingByLine = useMemo(() => {
    const map = new Map<string, number>();
    loan?.lines.forEach((line) => {
      map.set(line.id, line.qtyIssued - line.qtyReturned);
    });
    return map;
  }, [loan]);

  const updateReturn = (lineId: string, value: number) => {
    setReturns((prev) =>
      prev.map((line) =>
        line.loanLineId === lineId ? { ...line, qtyReturnedIncrement: value } : line
      )
    );
  };

  const updateCondition = (lineId: string, value: ReturnLineInput["condition"]) => {
    setReturns((prev) =>
      prev.map((line) =>
        line.loanLineId === lineId ? { ...line, condition: value } : line
      )
    );
  };

  const formatDate = (value?: string | null) => {
    if (!value) return "-";
    const dt = new Date(value);
    return Number.isNaN(dt.getTime()) ? value : dt.toLocaleString();
  };

  const hasReturnErrors = useMemo(() => {
    return returns.some((line) => {
      const remaining = remainingByLine.get(line.loanLineId) ?? 0;
      return line.qtyReturnedIncrement > remaining || line.qtyReturnedIncrement < 0;
    });
  }, [returns, remainingByLine]);

  const submitReturn = async () => {
    if (!id) return;
    const payload = returns.filter((line) => line.qtyReturnedIncrement > 0);
    if (payload.length === 0) {
      show("Enter a return quantity.", "error");
      return;
    }

    for (const line of payload) {
      if (!Number.isInteger(line.qtyReturnedIncrement)) {
        show("Return quantity must be a whole number.", "error");
        return;
      }
      const remaining = remainingByLine.get(line.loanLineId) ?? 0;
      if (line.qtyReturnedIncrement > remaining) {
        show("Return quantity exceeds remaining.", "error");
        return;
      }
    }

    try {
      setLoading(true);
      await api(`/api/loans/${id}/return`, {
        method: "POST",
        body: JSON.stringify({ lines: payload })
      });
      show("Return recorded.", "success");
      await fetchLoan();
      clearDashboardKpiCache();
    } catch (e: any) {
      handleApiError(e, show);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Loan Detail"
        description="Manage partial returns and condition tracking."
        actions={
          <>
            <StatusBadge status={loan?.status} />
            <button onClick={() => nav("/loans")} className="rounded-lg border border-border px-3 py-2 text-sm">
              Back
            </button>
          </>
        }
      />

      {loan ? (
        <div>
          <div className="mb-6 flex flex-wrap items-center gap-6 rounded-2xl border border-border bg-white p-5 text-sm">
            <div>
              <p className="text-xs uppercase text-muted-foreground">Borrower</p>
              <p className="mt-1 font-semibold">{loan.borrowerUsername}</p>
            </div>
          </div>

          <h2 className="mb-2 text-lg font-semibold">Lines</h2>
          <DataTable>
            <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-4 py-3 text-left">Item</th>
                <th className="px-4 py-3 text-right">Issued</th>
                <th className="px-4 py-3 text-right">Returned</th>
                <th className="px-4 py-3 text-right">Remaining</th>
              </tr>
            </thead>
            <tbody>
              {loan.lines.map((line) => (
                <tr key={line.id} className="border-t border-border">
                  <td className="px-4 py-3 text-sm">{line.inventoryName}</td>
                  <td className="px-4 py-3 text-right text-sm">{line.qtyIssued}</td>
                  <td className="px-4 py-3 text-right text-sm">{line.qtyReturned}</td>
                  <td className="px-4 py-3 text-right text-sm">{line.qtyIssued - line.qtyReturned}</td>
                </tr>
              ))}
            </tbody>
          </DataTable>

          <div className="mt-6 rounded-2xl border border-border bg-white p-5">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-lg font-semibold">Return Items</h2>
                <p className="text-sm text-muted-foreground">
                  GOOD returns add stock. DAMAGED/LOST do not.
                </p>
              </div>
            </div>

            <div className="mt-4">
              <DataTable>
                <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="px-4 py-3 text-left">Item</th>
                    <th className="px-4 py-3 text-right">Qty</th>
                    <th className="px-4 py-3 text-left">Condition</th>
                    <th className="px-4 py-3 text-right" />
                  </tr>
                </thead>
                <tbody>
                  {loan.lines.map((line) => {
                    const input = returns.find((ret) => ret.loanLineId === line.id);
                    const remaining = line.qtyIssued - line.qtyReturned;
                    const invalid = (input?.qtyReturnedIncrement ?? 0) > remaining;
                    return (
                      <tr key={line.id} className="border-t border-border">
                        <td className="px-4 py-3 text-sm">{line.inventoryName}</td>
                        <td className="px-4 py-3 text-right text-sm">
                          <input
                            type="number"
                            min={0}
                            step={1}
                            value={input?.qtyReturnedIncrement ?? 0}
                            onChange={(e) => updateReturn(line.id, Number(e.target.value))}
                            className={`h-9 w-24 rounded-lg border px-2 text-sm ${
                              invalid ? "border-red-400" : "border-border"
                            }`}
                          />
                        </td>
                        <td className="px-4 py-3 text-sm">
                          <select
                            value={input?.condition ?? "GOOD"}
                            onChange={(e) => updateCondition(line.id, e.target.value as ReturnLineInput["condition"])}
                            className="h-9 rounded-lg border border-border bg-white px-2 text-sm"
                          >
                            <option value="GOOD">GOOD</option>
                            <option value="DAMAGED">DAMAGED</option>
                            <option value="LOST">LOST</option>
                          </select>
                        </td>
                        <td className="px-4 py-3 text-right text-sm">
                          <button
                            type="button"
                            onClick={() => updateReturn(line.id, remaining)}
                            className="rounded-lg border border-border px-2 py-1 text-xs"
                          >
                            Max
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </DataTable>
            </div>

            <div className="mt-4">
              <button
                onClick={submitReturn}
                disabled={loading || hasReturnErrors}
                className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white"
              >
                {loading ? (
                  <span className="inline-flex items-center gap-2">
                    <Spinner /> Submitting
                  </span>
                ) : (
                  "Submit Return"
                )}
              </button>
              {hasReturnErrors ? (
                <p className="mt-2 text-sm text-red-500">One or more lines exceed remaining quantity.</p>
              ) : null}
            </div>
          </div>

          <h2 className="mt-8 mb-2 text-lg font-semibold">History</h2>
          <DataTable>
            <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-4 py-3 text-left">Type</th>
                <th className="px-4 py-3 text-left">Actor</th>
                <th className="px-4 py-3 text-left">Decision</th>
                <th className="px-4 py-3 text-left">Step</th>
                <th className="px-4 py-3 text-left">Movement</th>
                <th className="px-4 py-3 text-right">Qty</th>
                <th className="px-4 py-3 text-left">Condition</th>
                <th className="px-4 py-3 text-left">When</th>
              </tr>
            </thead>
            <tbody>
              {(loan.history ?? []).map((entry, idx) => (
                <tr key={`${entry.type}-${entry.timestamp}-${idx}`} className="border-t border-border">
                  <td className="px-4 py-3 text-sm">{entry.type}</td>
                  <td className="px-4 py-3 text-sm">{entry.actor ?? "-"}</td>
                  <td className="px-4 py-3 text-sm">{entry.decision ?? "-"}</td>
                  <td className="px-4 py-3 text-sm">{entry.step ?? "-"}</td>
                  <td className="px-4 py-3 text-sm">{entry.movementType ?? "-"}</td>
                  <td className="px-4 py-3 text-right text-sm">{entry.qty ?? "-"}</td>
                  <td className="px-4 py-3 text-sm">{entry.condition ?? "-"}</td>
                  <td className="px-4 py-3 text-sm">{formatDate(entry.timestamp)}</td>
                </tr>
              ))}
            </tbody>
          </DataTable>

          {(loan.history ?? []).length === 0 ? (
            <EmptyState title="No history recorded." description="Returns will appear here." />
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
