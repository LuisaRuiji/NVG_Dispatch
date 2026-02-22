import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { api } from "@/lib/api";
import type { RequestDetail } from "./types";
import { getMe } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";
import PageHeader from "@/components/PageHeader";
import StatusBadge from "@/components/StatusBadge";
import DataTable from "@/components/DataTable";
import EmptyState from "@/components/EmptyState";
import { clearDashboardKpiCache } from "@/features/dashboard/kpis";
import { handleApiError } from "@/lib/apiError";
import Spinner from "@/components/Spinner";

export default function RequestDetailPage() {
  const nav = useNavigate();
  const { id } = useParams();
  const me = getMe();

  const [request, setRequest] = useState<RequestDetail | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [activeAction, setActiveAction] = useState<"approve" | "reject" | "issue" | null>(null);
  const [remarks, setRemarks] = useState("");
  const [confirmIssue, setConfirmIssue] = useState(false);
  const { toasts, show } = useToast();

  const fetchRequest = async () => {
    if (!id) return;
    const detail = await api<RequestDetail>(`/api/requests/${id}`, { method: "GET" });
    setRequest(detail);
  };

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
        await fetchRequest();
      } catch (e: any) {
        handleApiError(e, show);
      }
    })();
  }, [id]);

  const roles = me?.roles ?? [];
  const status = request?.status ?? "";

  const canIoReview = roles.includes("InventoryOfficer") && status === "PENDING_IO";
  const canManagerDecision = roles.includes("Manager") && status === "PENDING_MANAGER";
  const canIssue = roles.includes("InventoryOfficer") && status === "APPROVED";
  const hasLoanLink = request?.requestType === "BORROW" && request.loanId;

  const actionSummary = useMemo(() => {
    if (!request || !activeAction) return null;
    const lineCount = request.lines.length;
    if (activeAction === "issue") {
      return `Issue stock for ${lineCount} line(s). This will reduce inventory and close the request.`;
    }
    if (activeAction === "approve") {
      return `Approve request with ${lineCount} line(s). This moves to Approved status. No stock is deducted until Issued.`;
    }
    return `Reject request with ${lineCount} line(s). This ends the workflow.`;
  }, [activeAction, request]);

  const formatDate = (value?: string | null) => {
    if (!value) return "-";
    const dt = new Date(value);
    return Number.isNaN(dt.getTime()) ? value : dt.toLocaleString();
  };

  const handleManagerDecision = async (decision: "APPROVE" | "REJECT") => {
    if (!id) return;
    try {
      setActionLoading(true);
      await api(`/api/requests/${id}/manager-decision`, {
        method: "POST",
        body: JSON.stringify({
          decision,
          remarks: remarks || null
        })
      });
      await fetchRequest();
      clearDashboardKpiCache();
      setActiveAction(null);
      setRemarks("");
      show(`Manager ${decision.toLowerCase()}d request.`, "success");
    } catch (e: any) {
      handleApiError(e, show);
    } finally {
      setActionLoading(false);
    }
  };

  const handleIssue = async () => {
    if (!id) return;
    try {
      setActionLoading(true);
      await api(`/api/requests/${id}/issue-stock`, {
        method: "POST"
      });
      await fetchRequest();
      clearDashboardKpiCache();
      setActiveAction(null);
      setConfirmIssue(false);
      show("Issue completed.", "success");
    } catch (e: any) {
      handleApiError(e, show);
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <div>
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Request Detail"
        description="Track approvals, issuance, and stock movements."
        actions={
          <>
            <StatusBadge status={request?.status} />
            <button
              onClick={() => nav("/requests")}
              className="rounded-lg border border-border px-3 py-2 text-sm"
            >
              Back
            </button>
          </>
        }
      />

      {request ? (
        <div>
          <div className="mb-6 grid gap-4 rounded-2xl border border-border bg-white p-5 text-sm md:grid-cols-2">
            <div>
              <p className="text-xs uppercase text-muted-foreground">Type</p>
              <p className="mt-1 font-semibold">{request.requestType}</p>
            </div>
            <div>
              <p className="text-xs uppercase text-muted-foreground">Asset</p>
              <p className="mt-1 font-semibold">{request.assetCode || request.assetId || "-"}</p>
            </div>
            <div>
              <p className="text-xs uppercase text-muted-foreground">Requester</p>
              <p className="mt-1 font-semibold">
                {request.requesterUsername} ({request.requesterUserId})
              </p>
            </div>
            <div>
              <p className="text-xs uppercase text-muted-foreground">Workflow</p>
              <p className="mt-1">
                {request.approval
                  ? `${request.approval.workflowKey ?? "n/a"} step ${request.approval.currentStep ?? "-"} (${
                      request.approval.status ?? "-"
                    })`
                  : "-"}
              </p>
            </div>
            <div>
              <p className="text-xs uppercase text-muted-foreground">Next Approver</p>
              <p className="mt-1">{request.approval?.nextApproverRole ?? "-"}</p>
            </div>
          </div>

          <h2 className="mb-2 text-lg font-semibold">Lines</h2>
          <DataTable>
            <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-4 py-3 text-left">Item</th>
                <th className="px-4 py-3 text-right">Available</th>
                <th className="px-4 py-3 text-right">Qty Requested</th>
                <th className="px-4 py-3 text-right">Qty Approved</th>
              </tr>
            </thead>
            <tbody>
              {request.lines.map((line) => (
                <tr key={line.id} className="border-t border-border">
                  <td className="px-4 py-3 text-sm">{line.inventoryName}</td>
                  <td className="px-4 py-3 text-right text-sm">
                    <span className="inline-flex rounded-full bg-muted px-2 py-1 text-xs">
                      {Math.trunc(line.currentQuantity)}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right text-sm">{line.qtyRequested}</td>
                  <td className="px-4 py-3 text-right text-sm">{line.qtyApproved ?? "-"}</td>
                </tr>
              ))}
            </tbody>
          </DataTable>

          {canIoReview && (
            <div className="mt-6">
              <button
                onClick={() => nav(`/requests/${id}/io-review`)}
                disabled={actionLoading}
                className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white"
              >
                IO Review
              </button>
            </div>
          )}

          {canManagerDecision && (
            <div className="mt-6 flex gap-2">
              <button
                onClick={() => setActiveAction("reject")}
                disabled={actionLoading}
                className="rounded-lg bg-red-500 px-4 py-2 text-sm font-semibold text-white"
              >
                Reject
              </button>
              <button
                onClick={() => setActiveAction("approve")}
                disabled={actionLoading}
                className="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white"
              >
                Approve
              </button>
            </div>
          )}

          {canIssue && (
            <div className="mt-6">
              <button
                onClick={() => setActiveAction("issue")}
                disabled={actionLoading}
                className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white"
              >
                Issue Stock
              </button>
            </div>
          )}

          {activeAction ? (
            <div className="mt-6 rounded-2xl border border-border bg-white p-5">
              <p className="text-xs uppercase tracking-widest text-muted-foreground">Action Panel</p>
              <p className="mt-2 text-sm text-foreground">{actionSummary}</p>
              <div className="mt-4 grid gap-3">
                {(activeAction === "approve" || activeAction === "reject") ? (
                  <div>
                    <label className="text-xs uppercase text-muted-foreground">Remarks</label>
                    {activeAction === "reject" ? (
                      <p className="mt-1 text-xs text-muted-foreground">
                        Remarks are required for reject.
                      </p>
                    ) : null}
                    <textarea
                      value={remarks}
                      onChange={(e) => setRemarks(e.target.value)}
                      className="mt-1 min-h-[90px] w-full rounded-lg border border-border bg-white px-3 py-2 text-sm"
                    />
                  </div>
                ) : null}

                {activeAction === "issue" ? (
                  <label className="inline-flex items-center gap-2 text-sm text-muted-foreground">
                    <input
                      type="checkbox"
                      checked={confirmIssue}
                      onChange={(e) => setConfirmIssue(e.target.checked)}
                    />
                    I understand this will reduce inventory and close the request.
                  </label>
                ) : null}
              </div>

              <div className="mt-4 flex flex-wrap gap-2">
                <button
                  onClick={() => {
                    setActiveAction(null);
                    setRemarks("");
                    setConfirmIssue(false);
                  }}
                  className="rounded-lg border border-border px-4 py-2 text-sm"
                  disabled={actionLoading}
                >
                  Cancel
                </button>
                {activeAction === "approve" ? (
                  <button
                    onClick={() => handleManagerDecision("APPROVE")}
                    className="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white"
                    disabled={actionLoading}
                  >
                    {actionLoading ? (
                      <span className="inline-flex items-center gap-2">
                        <Spinner /> Approving
                      </span>
                    ) : (
                      "Confirm Approve"
                    )}
                  </button>
                ) : null}
                {activeAction === "reject" ? (
                  <button
                    onClick={() => handleManagerDecision("REJECT")}
                    className="rounded-lg bg-red-500 px-4 py-2 text-sm font-semibold text-white"
                    disabled={actionLoading || remarks.trim().length === 0}
                  >
                    {actionLoading ? (
                      <span className="inline-flex items-center gap-2">
                        <Spinner /> Rejecting
                      </span>
                    ) : (
                      "Confirm Reject"
                    )}
                  </button>
                ) : null}
                {activeAction === "issue" ? (
                  <button
                    onClick={handleIssue}
                    className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white"
                    disabled={actionLoading || !confirmIssue}
                  >
                    {actionLoading ? (
                      <span className="inline-flex items-center gap-2">
                        <Spinner /> Issuing
                      </span>
                    ) : (
                      "Confirm Issue"
                    )}
                  </button>
                ) : null}
              </div>
            </div>
          ) : null}

          {hasLoanLink ? (
            <div className="mt-4">
              <button
                onClick={() => nav(`/loans/${request?.loanId}`)}
                className="rounded-lg border border-border px-4 py-2 text-sm"
              >
                View Loan
              </button>
            </div>
          ) : null}

          <h2 className="mt-8 mb-2 text-lg font-semibold">History</h2>
          <DataTable>
            <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
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
              {(request.history ?? []).map((entry, idx) => (
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

          {(request.history ?? []).length === 0 ? (
            <EmptyState title="No history recorded." description="Approval and stock actions will appear here." />
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
