import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import StatusBadge from "@/components/StatusBadge";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import ToastHost from "@/components/ToastHost";
import { useToast } from "@/lib/useToast";
import type { PurchaseOrderDetail } from "./types";
import { clearDashboardKpiCache } from "@/features/dashboard/kpis";
import { handleApiError } from "@/lib/apiError";
import Spinner from "@/components/Spinner";

type ReceiveLine = {
  lineId: string;
  receiveQty: number;
  remaining: number;
};

export default function PurchaseOrderDetailPage() {
  const nav = useNavigate();
  const { id } = useParams();
  const me = getMe();
  const roles = me?.roles ?? [];
  const { toasts, show } = useToast();

  const [detail, setDetail] = useState<PurchaseOrderDetail | null>(null);
  const [remarks, setRemarks] = useState("");
  const [loading, setLoading] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [receiveLines, setReceiveLines] = useState<Record<string, ReceiveLine>>({});
  const [receiveRemarks, setReceiveRemarks] = useState("");
  const [receiveSubmitting, setReceiveSubmitting] = useState(false);

  const canReceive =
    roles.includes("InventoryOfficer") &&
    (detail?.status === "APPROVED" || detail?.status === "PARTIALLY_RECEIVED");

  const nextApproverRole = detail?.approval?.nextApproverRole;
  const canDecide = Boolean(
    nextApproverRole && roles.some((role) => role === nextApproverRole)
  );

  const decisionSummary = useMemo(() => {
    if (!detail?.approval) return "";
    const nextRole = detail.approval.nextApproverRole;
    if (nextRole) {
      return `Approve moves to ${nextRole}. Reject closes the PO approval flow.`;
    }
    return "Approve finalizes the PO approval. Reject closes the PO approval flow.";
  }, [detail?.approval]);

  const fetchDetail = async () => {
    if (!id) return;
    const result = await api<PurchaseOrderDetail>(`/api/purchase-orders/${id}`, { method: "GET" });
    setDetail(result);
    const map: Record<string, ReceiveLine> = {};
    result.lines.forEach((line) => {
      const remaining = Math.max(0, line.qtyOrdered - line.qtyReceived);
      map[line.id] = { lineId: line.id, receiveQty: 0, remaining };
    });
    setReceiveLines(map);
  };

  useEffect(() => {
    if (!me) {
      nav("/login");
      return;
    }
    if (!id) {
      show("Missing purchase order id.", "error");
      return;
    }

    (async () => {
      try {
        setDetailLoading(true);
        await fetchDetail();
      } catch (e: any) {
        handleApiError(e, show);
      } finally {
        setDetailLoading(false);
      }
    })();
  }, [id, nav]);

  const formatDate = (value?: string | null) => {
    if (!value) return "-";
    const dt = new Date(value);
    return Number.isNaN(dt.getTime()) ? value : dt.toLocaleString();
  };

  const updateReceiveQty = (lineId: string, qty: number) => {
    setReceiveLines((prev) => {
      const existing = prev[lineId];
      if (!existing) return prev;
      const safe = Math.max(0, Math.min(existing.remaining, qty));
      return { ...prev, [lineId]: { ...existing, receiveQty: safe } };
    });
  };

  const totalReceiveQty = useMemo(() => {
    return Object.values(receiveLines).reduce((sum, line) => sum + (line.receiveQty || 0), 0);
  }, [receiveLines]);

  const receiveHasErrors = useMemo(() => {
    return Object.values(receiveLines).some((line) => line.receiveQty > line.remaining);
  }, [receiveLines]);

  const handleDecision = async (decision: "APPROVE" | "REJECT") => {
    if (!id) return;
    try {
      setLoading(true);
      await api(`/api/purchase-orders/${id}/decision`, {
        method: "POST",
        body: JSON.stringify({
          decision,
          remarks: remarks || null
        })
      });
      await fetchDetail();
      clearDashboardKpiCache();
      setRemarks("");
      show(`PO ${decision.toLowerCase()}d.`, "success");
    } catch (e: any) {
      handleApiError(e, show);
    } finally {
      setLoading(false);
    }
  };

  const handleReceive = async () => {
    if (!id) return;
    const lines = Object.values(receiveLines)
      .filter((line) => line.receiveQty > 0)
      .map((line) => ({ purchaseOrderLineId: line.lineId, qtyReceived: line.receiveQty, remarks: null }));

    if (lines.length === 0) {
      show("Enter at least one receive quantity.", "error");
      return;
    }

    try {
      setReceiveSubmitting(true);
      await api(`/api/purchase-orders/${id}/receive`, {
        method: "POST",
        body: JSON.stringify({
          lines,
          remarks: receiveRemarks || null
        })
      });
      await fetchDetail();
      clearDashboardKpiCache();
      setReceiveRemarks("");
      show("Receipt saved.", "success");
    } catch (e: any) {
      handleApiError(e, show);
    } finally {
      setReceiveSubmitting(false);
    }
  };

  return (
    <div>
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Purchase Order Detail"
        description="Approval workflow and receiving."
        actions={
          <>
            <StatusBadge status={detail?.status} />
            <button className="rounded-lg border border-border px-3 py-2 text-sm" onClick={() => nav("/purchase-orders")}>
              Back
            </button>
          </>
        }
      />

      {detailLoading ? (
        <LoadingSkeleton rows={6} />
      ) : detail ? (
        <>
          <div className="mb-6 grid gap-4 rounded-2xl border border-border bg-white p-5 text-sm md:grid-cols-3">
            <div>
              <p className="text-xs uppercase text-muted-foreground">Supplier</p>
              <p className="mt-1 font-semibold">{detail.supplierName}</p>
            </div>
            <div>
              <p className="text-xs uppercase text-muted-foreground">Created By</p>
              <p className="mt-1 font-semibold">{detail.createdByUsername}</p>
            </div>
            <div>
              <p className="text-xs uppercase text-muted-foreground">Submitted</p>
              <p className="mt-1">{formatDate(detail.submittedAt)}</p>
            </div>
            <div>
              <p className="text-xs uppercase text-muted-foreground">Approved</p>
              <p className="mt-1">{formatDate(detail.approvedAt)}</p>
            </div>
            <div>
              <p className="text-xs uppercase text-muted-foreground">Received</p>
              <p className="mt-1">{formatDate(detail.receivedAt)}</p>
            </div>
          </div>

          <div className="grid gap-6 lg:grid-cols-[1.6fr_1fr]">
            <div>
              <h2 className="mb-2 text-lg font-semibold">Line Items</h2>
              <DataTable>
                <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="px-4 py-3 text-left">Item</th>
                    <th className="px-4 py-3 text-right">Ordered</th>
                    <th className="px-4 py-3 text-right">Received</th>
                    <th className="px-4 py-3 text-right">Remaining</th>
                    <th className="px-4 py-3 text-right">Unit Price</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.lines.map((line) => {
                    const remaining = Math.max(0, line.qtyOrdered - line.qtyReceived);
                    return (
                      <tr key={line.id} className="border-t border-border">
                        <td className="px-4 py-3 text-sm">{line.inventoryName}</td>
                        <td className="px-4 py-3 text-right text-sm">{line.qtyOrdered}</td>
                        <td className="px-4 py-3 text-right text-sm">{line.qtyReceived}</td>
                        <td className="px-4 py-3 text-right text-sm">{remaining}</td>
                        <td className="px-4 py-3 text-right text-sm">
                          {line.unitPrice ? line.unitPrice.toFixed(2) : "-"}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </DataTable>
            </div>

            <div className="space-y-4">
              <div className="rounded-2xl border border-border bg-white p-4 text-sm">
                <p className="text-xs uppercase tracking-widest text-muted-foreground">Approval Timeline</p>
                <div className="mt-3 space-y-2">
                  {detail.approvalActions.map((actionItem) => (
                    <div key={actionItem.id} className="rounded-lg border border-border p-3 text-xs">
                      <div className="flex items-center justify-between">
                        <span className="font-semibold">{actionItem.actorUsername ?? actionItem.actorUserId}</span>
                        <StatusBadge status={actionItem.decision} />
                      </div>
                      <p className="mt-1 text-muted-foreground">{formatDate(actionItem.createdAt)}</p>
                      {actionItem.remarks ? <p className="mt-1 text-muted-foreground">{actionItem.remarks}</p> : null}
                    </div>
                  ))}
                  {detail.approvalActions.length === 0 ? (
                    <EmptyState title="No approvals yet." description="Workflow actions will appear here." />
                  ) : null}
                </div>
              </div>

            </div>
          </div>

          {canDecide ? (
            <div className="mt-6 rounded-2xl border border-border bg-white p-4 text-sm">
              <p className="text-xs uppercase tracking-widest text-muted-foreground">Decision Panel</p>
              <p className="mt-2 text-sm text-foreground">
                Next action: {detail.approval?.nextApproverRole ?? "Final approval"}.
              </p>
              <p className="mt-2 text-xs text-muted-foreground">{decisionSummary}</p>
              <div className="mt-3 space-y-2">
                <label className="text-xs uppercase text-muted-foreground">Remarks (optional)</label>
                <textarea
                  value={remarks}
                  onChange={(e) => setRemarks(e.target.value)}
                  className="min-h-[90px] w-full rounded-lg border border-border bg-white px-3 py-2 text-sm"
                />
              </div>
              <div className="mt-3 flex gap-2">
                <button
                  onClick={() => handleDecision("REJECT")}
                  disabled={loading}
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
                  onClick={() => handleDecision("APPROVE")}
                  disabled={loading}
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
          ) : null}

          <div className="mt-8 rounded-2xl border border-border bg-white p-5">
            <div className="flex items-start justify-between">
              <div>
                <p className="text-lg font-semibold">Receive Stock</p>
                <p className="text-sm text-muted-foreground">
                  Enter quantities received. Over-receive is blocked. Pricing is locked to PO lines.
                </p>
              </div>
            </div>

            {!canReceive ? (
              <p className="mt-4 text-sm text-muted-foreground">
                Receiving is only available after approval and for Inventory Officers.
              </p>
            ) : (
              <>
                <div className="mt-4">
                  <DataTable>
                    <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                      <tr>
                        <th className="px-4 py-3 text-left">Item</th>
                        <th className="px-4 py-3 text-right">Remaining</th>
                        <th className="px-4 py-3 text-right">Receive Now</th>
                        <th className="px-4 py-3 text-right" />
                      </tr>
                    </thead>
                    <tbody>
                      {detail.lines.map((line) => {
                        const receive = receiveLines[line.id];
                        const remaining = receive?.remaining ?? 0;
                        const value = receive?.receiveQty ?? 0;
                        return (
                          <tr key={line.id} className="border-t border-border">
                            <td className="px-4 py-3 text-sm">{line.inventoryName}</td>
                            <td className="px-4 py-3 text-right text-sm">{remaining}</td>
                            <td className="px-4 py-3 text-right text-sm">
                              <input
                                type="number"
                                value={value}
                                min={0}
                                max={remaining}
                                onChange={(e) => updateReceiveQty(line.id, Number(e.target.value))}
                                className="h-9 w-24 rounded-lg border border-border bg-white px-2 text-sm"
                              />
                            </td>
                            <td className="px-4 py-3 text-right text-sm">
                              <button
                                type="button"
                                onClick={() => updateReceiveQty(line.id, remaining)}
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

                <div className="mt-4 grid gap-3">
                  <div>
                    <label className="text-xs uppercase text-muted-foreground">Receipt Notes</label>
                    <textarea
                      value={receiveRemarks}
                      onChange={(e) => setReceiveRemarks(e.target.value)}
                      className="mt-1 min-h-[80px] w-full rounded-lg border border-border bg-white px-3 py-2 text-sm"
                    />
                  </div>
                  <div className="text-sm text-muted-foreground">
                    Total to receive: <span className="font-semibold text-foreground">{totalReceiveQty}</span>
                  </div>
                  {receiveHasErrors ? (
                    <div className="text-sm text-red-500">
                      Receive quantity exceeds remaining allowed.
                    </div>
                  ) : null}
                  <button
                    onClick={handleReceive}
                    disabled={receiveSubmitting || receiveHasErrors || totalReceiveQty <= 0}
                    className="w-fit rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white"
                  >
                    {receiveSubmitting ? (
                      <span className="inline-flex items-center gap-2">
                        <Spinner /> Receiving
                      </span>
                    ) : (
                      "Confirm Receipt"
                    )}
                  </button>
                </div>
              </>
            )}
          </div>

          <div className="mt-8">
            <h2 className="mb-2 text-lg font-semibold">Receipts</h2>
            <DataTable>
              <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left">Line</th>
                  <th className="px-4 py-3 text-right">Qty Received</th>
                  <th className="px-4 py-3 text-left">Received By</th>
                  <th className="px-4 py-3 text-left">Received At</th>
                </tr>
              </thead>
              <tbody>
                {detail.receipts.map((receipt) => (
                  <tr key={receipt.id} className="border-t border-border">
                    <td className="px-4 py-3 text-sm">{receipt.purchaseOrderLineId}</td>
                    <td className="px-4 py-3 text-right text-sm">{receipt.qtyReceivedIncrement}</td>
                    <td className="px-4 py-3 text-sm">{receipt.receivedByUsername ?? receipt.receivedByUserId}</td>
                    <td className="px-4 py-3 text-sm">{formatDate(receipt.receivedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </DataTable>
            {detail.receipts.length === 0 ? (
              <EmptyState title="No receipts yet." description="Receipts will appear after receiving." />
            ) : null}
          </div>
        </>
      ) : null}
    </div>
  );
}
