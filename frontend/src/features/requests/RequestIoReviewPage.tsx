import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { api } from "@/lib/api";
import type { RequestDetail } from "./types";
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

type IoLineInput = {
  requestLineId: string;
  qtyApproved: number;
  remarks?: string | null;
};

export default function RequestIoReviewPage() {
  const nav = useNavigate();
  const { id } = useParams();
  const me = getMe();

  const [request, setRequest] = useState<RequestDetail | null>(null);
  const [ioLines, setIoLines] = useState<IoLineInput[]>([]);
  const [remarks, setRemarks] = useState("");
  const [loading, setLoading] = useState(false);
  const { toasts, show } = useToast();

  useEffect(() => {
    if (!me) {
      nav("/login");
      return;
    }
    if (!id) {
      show("Missing request id.", "error");
      return;
    }

    (async () => {
      try {
        const detail = await api<RequestDetail>(`/api/requests/${id}`, { method: "GET" });
        setRequest(detail);
        setIoLines(
          detail.lines.map((line) => ({
            requestLineId: line.id,
            qtyApproved: line.qtyApproved ?? line.qtyRequested,
            remarks: null
          }))
        );
      } catch (e: any) {
        handleApiError(e, show);
      }
    })();
  }, [id]);

  const roles = me?.roles ?? [];
  const canReview = roles.includes("InventoryOfficer") && request?.status === "PENDING_IO";

  const updateQtyApproved = (lineId: string, value: number) => {
    setIoLines((prev) =>
      prev.map((line) => (line.requestLineId === lineId ? { ...line, qtyApproved: value } : line))
    );
  };

  const validateLines = () => {
    for (const line of ioLines) {
      if (!Number.isFinite(line.qtyApproved)) {
        show("Qty approved must be a number.", "error");
        return false;
      }
      if (line.qtyApproved < 0) {
        show("Qty approved cannot be negative.", "error");
        return false;
      }
      if (!Number.isInteger(line.qtyApproved)) {
        show("Qty approved must be a whole number.", "error");
        return false;
      }
    }
    return true;
  };

  const submit = async (decision: "APPROVE" | "REJECT") => {
    if (!id) return;
    if (decision === "REJECT" && !remarks.trim()) {
      show("Remarks required for reject.", "error");
      return;
    }

    if (decision === "APPROVE" && !validateLines()) {
      return;
    }

    try {
      setLoading(true);
      const lines = decision === "REJECT"
        ? ioLines.map((line) => ({ ...line, qtyApproved: 0 }))
        : ioLines;

      await api(`/api/requests/${id}/io-review`, {
        method: "POST",
        body: JSON.stringify({
          lines,
          decision,
          remarks
        })
      });

      show(`IO ${decision.toLowerCase()}d request.`, "success");
      clearDashboardKpiCache();
      nav(`/requests/${id}`);
    } catch (e: any) {
      handleApiError(e, show);
    } finally {
      setLoading(false);
    }
  };

  const hasLineErrors = ioLines.some((line) => {
    const reqLine = request?.lines.find((req) => req.id === line.requestLineId);
    if (!reqLine) return false;
    const maxAllowed = Math.min(reqLine.qtyRequested, Math.trunc(reqLine.currentQuantity));
    return line.qtyApproved > maxAllowed;
  });

  return (
    <div>
      <ToastHost toasts={toasts} />
      <PageHeader
        title="IO Review"
        description="Approve quantities with inventory-aware constraints."
        actions={
          <button
            onClick={() => nav(`/requests/${id}`)}
            className="rounded-lg border border-border px-3 py-2 text-sm"
          >
            Back
          </button>
        }
      />

      {request ? (
        <div>
          <div className="mb-6 rounded-2xl border border-border bg-white p-5 text-sm">
            <div className="flex flex-wrap items-center gap-4">
              <div>
                <p className="text-xs uppercase text-muted-foreground">Request</p>
                <p className="mt-1 font-semibold">{request.id}</p>
              </div>
              <div>
                <p className="text-xs uppercase text-muted-foreground">Status</p>
                <div className="mt-1">
                  <StatusBadge status={request.status} />
                </div>
              </div>
            </div>
          </div>

          {canReview ? (
            <div>
              <h2 className="mb-2 text-lg font-semibold">Lines</h2>
              <DataTable>
                <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="px-4 py-3 text-left">Item</th>
                    <th className="px-4 py-3 text-right">Available</th>
                    <th className="px-4 py-3 text-right">Qty Requested</th>
                    <th className="px-4 py-3 text-right">Qty Approved</th>
                    <th className="px-4 py-3 text-right" />
                  </tr>
                </thead>
                <tbody>
                  {request.lines.map((line) => {
                    const input = ioLines.find((item) => item.requestLineId === line.id);
                    const maxAllowed = Math.min(line.qtyRequested, Math.trunc(line.currentQuantity));
                    const invalid = (input?.qtyApproved ?? 0) > maxAllowed;
                    return (
                      <tr key={line.id} className="border-t border-border">
                        <td className="px-4 py-3 text-sm">{line.inventoryName}</td>
                        <td className="px-4 py-3 text-right text-sm">
                          <span className="inline-flex rounded-full bg-muted px-2 py-1 text-xs">
                            {Math.trunc(line.currentQuantity)}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right text-sm">{line.qtyRequested}</td>
                        <td className="px-4 py-3 text-right text-sm">
                          <input
                            type="number"
                            value={input?.qtyApproved ?? 0}
                            onChange={(e) => updateQtyApproved(line.id, Number(e.target.value))}
                            min={0}
                            step={1}
                            className={`h-9 w-24 rounded-lg border px-2 text-sm ${
                              invalid ? "border-red-400" : "border-border"
                            }`}
                          />
                        </td>
                        <td className="px-4 py-3 text-right text-sm">
                          <button
                            type="button"
                            onClick={() => updateQtyApproved(line.id, maxAllowed)}
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

              <div className="mt-4 grid gap-3">
                <p className="text-xs text-muted-foreground">
                  Approve moves the request to Manager review. Reject closes the request.
                </p>
                <div>
                  <label className="text-xs uppercase text-muted-foreground">Remarks (required for reject)</label>
                  <textarea
                    value={remarks}
                    onChange={(e) => setRemarks(e.target.value)}
                    className="mt-1 min-h-[80px] w-full rounded-lg border border-border bg-white px-3 py-2 text-sm"
                  />
                </div>
                {hasLineErrors ? (
                  <p className="text-sm text-red-500">
                    One or more lines exceed the allowable maximum. Use Max to clamp.
                  </p>
                ) : null}
                <div className="flex flex-wrap gap-2">
                  <button
                    onClick={() => submit("REJECT")}
                    disabled={loading || !remarks.trim()}
                    className="rounded-lg bg-red-500 px-4 py-2 text-sm font-semibold text-white"
                  >
                    {loading ? (
                      <span className="inline-flex items-center gap-2">
                        <Spinner /> Rejecting
                      </span>
                    ) : (
                      "Reject"
                    )}
                  </button>
                  <button
                    onClick={() => submit("APPROVE")}
                    disabled={loading || hasLineErrors}
                    className="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white"
                  >
                    {loading ? (
                      <span className="inline-flex items-center gap-2">
                        <Spinner /> Approving
                      </span>
                    ) : (
                      "Approve"
                    )}
                  </button>
                </div>
              </div>
            </div>
          ) : (
            <EmptyState title="Not eligible for IO review." description="Status must be PENDING_IO." />
          )}
        </div>
      ) : null}
    </div>
  );
}
