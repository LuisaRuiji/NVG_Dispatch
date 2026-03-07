import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import type { PagedResult } from "@/lib/paging";
import type {
  DispatchTripDocument,
  DispatchTripListItem,
  TripDocumentState,
  TripDocumentType
} from "./types";

type DocQueueItem = {
  trip: DispatchTripListItem;
  doc: DispatchTripDocument;
};

type FilterState = {
  type: "ALL" | TripDocumentType;
  state: "ALL" | TripDocumentState;
  search: string;
};

type RejectModal = {
  tripId: string;
  docId: string;
  docType: TripDocumentType;
  remarks: string;
} | null;

const docStateClasses: Record<TripDocumentState, string> = {
  MISSING: "border-slate-200 text-slate-500",
  UPLOADED: "border-amber-200 text-amber-700",
  VERIFIED: "border-emerald-200 text-emerald-700",
  REJECTED: "border-rose-200 text-rose-700"
};

export default function DispatchDocumentsPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const me = getMe();
  const roles = me?.roles ?? [];
  const canVerify = roles.includes("Manager") || roles.includes("HeadOfFinance");

  const [loading, setLoading] = useState(false);
  const [docLoading, setDocLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);
  const [trips, setTrips] = useState<DispatchTripListItem[]>([]);
  const [docItems, setDocItems] = useState<DocQueueItem[]>([]);
  const [filters, setFilters] = useState<FilterState>({
    type: "ALL",
    state: "UPLOADED",
    search: ""
  });
  const [rejectModal, setRejectModal] = useState<RejectModal>(null);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const loadTrips = async (targetPage = page) => {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      params.set("page", targetPage.toString());
      params.set("pageSize", pageSize.toString());
      const result = await api<PagedResult<DispatchTripListItem>>(`/api/dispatch/trips?${params.toString()}`, {
        method: "GET"
      });
      setTrips(result.items ?? []);
      setTotalCount(result.totalCount ?? 0);
      setPage(result.page ?? targetPage);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load trips.", "error");
    } finally {
      setLoading(false);
    }
  };

  const loadDocuments = async (sourceTrips: DispatchTripListItem[]) => {
    setDocLoading(true);
    try {
      if (sourceTrips.length === 0) {
        setDocItems([]);
        return;
      }
      const results = await Promise.all(
        sourceTrips.map(async (trip) => {
          try {
            const docs = await api<DispatchTripDocument[]>(`/api/dispatch/trips/${trip.id}/documents`, {
              method: "GET"
            });
            return { trip, docs: docs ?? [] };
          } catch (e) {
            console.error(e);
            return { trip, docs: [] as DispatchTripDocument[] };
          }
        })
      );
      const flattened: DocQueueItem[] = [];
      results.forEach(({ trip, docs }) => {
        docs.forEach((doc) => flattened.push({ trip, doc }));
      });
      setDocItems(flattened);
    } finally {
      setDocLoading(false);
    }
  };

  useEffect(() => {
    loadTrips(1);
  }, []);

  useEffect(() => {
    loadTrips();
  }, [page]);

  useEffect(() => {
    loadDocuments(trips);
  }, [trips]);

  const filteredDocs = useMemo(() => {
    return docItems.filter(({ trip, doc }) => {
      if (filters.type !== "ALL" && doc.type !== filters.type) return false;
      if (filters.state !== "ALL" && doc.state !== filters.state) return false;
      if (filters.search) {
        const needle = filters.search.toLowerCase();
        const haystack = `${trip.id} ${trip.customer?.name ?? ""} ${trip.driverUsername ?? ""}`.toLowerCase();
        if (!haystack.includes(needle)) return false;
      }
      return true;
    });
  }, [docItems, filters]);

  const handleVerify = async (tripId: string, docId: string) => {
    try {
      await api(`/api/dispatch/trips/${tripId}/documents/${docId}/verify`, { method: "POST" });
      show("Document verified.", "success");
      loadDocuments(trips);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to verify document.", "error");
    }
  };

  const handleReject = async () => {
    if (!rejectModal) return;
    if (!rejectModal.remarks.trim()) {
      show("Remarks are required.", "error");
      return;
    }
    try {
      await api(`/api/dispatch/trips/${rejectModal.tripId}/documents/${rejectModal.docId}/reject`, {
        method: "POST",
        body: JSON.stringify({ remarks: rejectModal.remarks.trim() })
      });
      show("Document rejected.", "success");
      setRejectModal(null);
      loadDocuments(trips);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to reject document.", "error");
    }
  };

  const handleOpen = async (tripId: string, docId: string) => {
    try {
      const result = await api<{ storageKey: string }>(
        `/api/dispatch/trips/${tripId}/documents/${docId}/link`,
        { method: "GET" }
      );
      const key = result?.storageKey ?? "";
      if (!key) {
        show("No document link available.", "error");
        return;
      }
      if (key.startsWith("http://") || key.startsWith("https://")) {
        window.open(key, "_blank", "noopener,noreferrer");
        return;
      }
      try {
        await navigator.clipboard.writeText(key);
        show("Storage key copied.", "success");
      } catch {
        window.prompt("Storage key", key);
      }
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load document link.", "error");
    }
  };

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Dispatch Documents"
        description="Verify or reject uploaded dispatch documents."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/dispatch/board" className="text-muted-foreground hover:text-foreground">
              Dispatch
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">Documents</span>
          </nav>
        }
        actions={
          <Button variant="outline" size="sm" onClick={() => loadTrips()} disabled={loading}>
            Refresh
          </Button>
        }
      />

      <div className="surface-card p-6">
        <div className="grid gap-4 md:grid-cols-3">
          <div>
            <label className="text-xs uppercase text-muted-foreground">Document Type</label>
            <select
              value={filters.type}
              onChange={(e) => setFilters((prev) => ({ ...prev, type: e.target.value as FilterState["type"] }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            >
              <option value="ALL">All types</option>
              <option value="POD">POD</option>
              <option value="WAYBILL">WAYBILL</option>
              <option value="ATW">ATW</option>
            </select>
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Status</label>
            <select
              value={filters.state}
              onChange={(e) =>
                setFilters((prev) => ({ ...prev, state: e.target.value as FilterState["state"] }))
              }
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
            >
              <option value="ALL">All</option>
              <option value="UPLOADED">Uploaded</option>
              <option value="VERIFIED">Verified</option>
              <option value="REJECTED">Rejected</option>
            </select>
          </div>
          <div>
            <label className="text-xs uppercase text-muted-foreground">Search</label>
            <input
              value={filters.search}
              onChange={(e) => setFilters((prev) => ({ ...prev, search: e.target.value }))}
              className="mt-2 h-9 w-full rounded-lg border border-border bg-background px-3 text-sm"
              placeholder="Trip ID, customer, driver"
            />
          </div>
        </div>
      </div>

      {loading && trips.length === 0 ? (
        <LoadingSkeleton rows={6} />
      ) : filteredDocs.length === 0 ? (
        <EmptyState
          title={docLoading ? "Loading documents" : "No documents found"}
          description={
            docLoading ? "Fetching documents for this page." : "No documents match the current filters."
          }
        />
      ) : (
        <div className="surface-card p-6">
          <DataTable>
            <thead className="bg-muted/30 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
              <tr>
                <th className="px-6 py-4 text-left">Trip</th>
                <th className="px-6 py-4 text-left">Driver</th>
                <th className="px-6 py-4 text-left">Document</th>
                <th className="px-6 py-4 text-left">Uploaded</th>
                <th className="px-6 py-4 text-left">Status</th>
                <th className="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/50">
              {filteredDocs.map(({ trip, doc }) => (
                <tr
                  key={`${trip.id}-${doc.id}`}
                  className="cursor-pointer text-sm hover:bg-muted/30"
                  onClick={() => nav(`/dispatch/trips/${trip.id}`)}
                >
                  <td className="px-6 py-4 font-medium text-foreground">{trip.id.slice(0, 8)}</td>
                  <td className="px-6 py-4">{trip.driverUsername ?? "-"}</td>
                  <td className="px-6 py-4">
                    <div className="flex flex-col">
                      <span className="font-semibold text-foreground">{doc.type}</span>
                      <span className="text-xs text-muted-foreground">Active version</span>
                    </div>
                  </td>
                  <td className="px-6 py-4 text-xs text-muted-foreground">
                    {doc.uploadedAt ? new Date(doc.uploadedAt).toLocaleString() : "-"}
                  </td>
                  <td className="px-6 py-4">
                    <span
                      className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
                        docStateClasses[doc.state]
                      }`}
                    >
                      {doc.state}
                    </span>
                  </td>
                  <td className="px-6 py-4 text-right">
                    <div className="flex items-center justify-end gap-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={(e) => {
                          e.stopPropagation();
                          handleOpen(trip.id, doc.id);
                        }}
                      >
                        Open
                      </Button>
                      {canVerify && doc.state === "UPLOADED" ? (
                        <>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={(e) => {
                              e.stopPropagation();
                              handleVerify(trip.id, doc.id);
                            }}
                          >
                            Verify
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            className="border-rose-200 text-rose-700 hover:bg-rose-50"
                            onClick={(e) => {
                              e.stopPropagation();
                              setRejectModal({
                                tripId: trip.id,
                                docId: doc.id,
                                docType: doc.type,
                                remarks: ""
                              });
                            }}
                          >
                            Reject
                          </Button>
                        </>
                      ) : null}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </DataTable>

          <div className="mt-4 flex items-center justify-between text-xs text-muted-foreground">
            <span>
              Page {page} of {totalPages}
            </span>
            <div className="flex items-center gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                Previous
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        </div>
      )}

      {rejectModal ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          onClick={() => setRejectModal(null)}
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,520px)] rounded-2xl border border-slate-200 bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Reject Document</p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">{rejectModal.docType}</h2>
              </div>
              <button
                onClick={() => setRejectModal(null)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>

            <div className="mt-5 space-y-2 text-sm">
              <label className="text-xs uppercase text-slate-500">Remarks</label>
              <textarea
                value={rejectModal.remarks}
                onChange={(e) => setRejectModal((prev) => (prev ? { ...prev, remarks: e.target.value } : prev))}
                className="mt-2 min-h-[100px] w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm"
                placeholder="Reason for rejection"
              />
            </div>

            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setRejectModal(null)}>
                Cancel
              </Button>
              <Button className="bg-rose-600 hover:bg-rose-700" onClick={handleReject}>
                Reject
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
