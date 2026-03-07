import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type {
  CustomerShipmentDetail,
  CustomerShipmentDocument,
  CustomerShipmentTimelineEntry
} from "./types";
import { statusLabels } from "@/features/dispatch/types";

export default function PortalShipmentDetailPage() {
  const { id } = useParams();
  const nav = useNavigate();
  const { toasts, show } = useToast();

  const [loading, setLoading] = useState(true);
  const [detail, setDetail] = useState<CustomerShipmentDetail | null>(null);
  const [timeline, setTimeline] = useState<CustomerShipmentTimelineEntry[]>([]);
  const [documents, setDocuments] = useState<CustomerShipmentDocument[]>([]);

  const loadShipment = async () => {
    if (!id) return;
    try {
      setLoading(true);
      const [summary, events, docs] = await Promise.all([
        api<CustomerShipmentDetail>(`/api/portal/shipments/${id}`, { method: "GET" }),
        api<CustomerShipmentTimelineEntry[]>(`/api/portal/shipments/${id}/timeline`, { method: "GET" }),
        api<CustomerShipmentDocument[]>(`/api/portal/shipments/${id}/documents`, { method: "GET" })
      ]);
      setDetail(summary);
      setTimeline(events ?? []);
      setDocuments(docs ?? []);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load shipment.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadShipment();
  }, [id]);

  if (loading) {
    return (
      <div className="space-y-6">
        <PageHeader title="Shipment" description="Loading shipment detail..." />
        <LoadingSkeleton rows={8} />
      </div>
    );
  }

  if (!detail) {
    return <EmptyState title="Shipment not found" description="The shipment could not be loaded." />;
  }

  const podDoc = documents.find((doc) => doc.type === "POD");

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title={`Shipment ${detail.tripId.slice(0, 8)}`}
        description="Shipment tracking and documents."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/portal/dashboard" className="text-muted-foreground hover:text-foreground">
              Portal
            </Link>
            <span className="text-muted-foreground">/</span>
            <Link to="/portal/shipments" className="text-muted-foreground hover:text-foreground">
              Shipments
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">Shipment {detail.tripId.slice(0, 8)}</span>
          </nav>
        }
        actions={
          <Button variant="outline" onClick={() => nav("/portal/shipments")}>
            Back
          </Button>
        }
      />

      <div className="surface-card p-6 space-y-5">
        <div className="flex flex-wrap items-center gap-3">
          <StatusBadge status={statusLabels[detail.status] ?? detail.status} />
          <span className="text-xs rounded-full border border-slate-200 bg-slate-50 px-2 py-1 text-slate-600">
            POD: {detail.podState}
          </span>
        </div>

        <div className="grid gap-4 md:grid-cols-2 text-sm">
          <div className="rounded-xl border border-border/50 bg-muted/20 px-4 py-3">
            <p className="text-xs uppercase text-muted-foreground">Pickup</p>
            <p className="mt-1 text-foreground">{detail.pickupLocation}</p>
            <p className="mt-1 text-xs text-muted-foreground">
              {detail.pickupTime ? new Date(detail.pickupTime).toLocaleString() : "Unscheduled"}
            </p>
          </div>
          <div className="rounded-xl border border-border/50 bg-muted/20 px-4 py-3">
            <p className="text-xs uppercase text-muted-foreground">Dropoff</p>
            <p className="mt-1 text-foreground">{detail.dropoffLocation}</p>
            <p className="mt-1 text-xs text-muted-foreground">
              {detail.dropoffTime ? new Date(detail.dropoffTime).toLocaleString() : "Unscheduled"}
            </p>
          </div>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1.1fr_1fr]">
        <div className="surface-card p-6">
          <h3 className="text-sm font-semibold">Stops</h3>
          <div className="mt-4 space-y-3 text-sm">
            {detail.stops.map((stop) => (
              <div key={`${stop.stopType}-${stop.locationText}`} className="rounded-lg border border-border/50 bg-muted/10 px-4 py-3">
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-foreground">{stop.stopType}</span>
                  <span className="text-xs text-muted-foreground">
                    {stop.scheduledAt ? new Date(stop.scheduledAt).toLocaleString() : "Unscheduled"}
                  </span>
                </div>
                <p className="mt-2 text-muted-foreground">{stop.locationText}</p>
                {stop.actualAt ? (
                  <p className="mt-1 text-xs text-muted-foreground">
                    Actual: {new Date(stop.actualAt).toLocaleString()}
                  </p>
                ) : null}
              </div>
            ))}
          </div>
        </div>

        <div className="space-y-6">
          <div className="surface-card p-6">
            <h3 className="text-sm font-semibold">Timeline</h3>
            {timeline.length === 0 ? (
              <div className="mt-4">
                <EmptyState title="No events yet" description="Tracking updates will appear here." />
              </div>
            ) : (
              <div className="mt-4 space-y-3 text-sm">
                {timeline.map((entry, index) => (
                  <div key={`${entry.eventAt}-${index}`} className="rounded-lg border border-border/50 bg-muted/10 px-4 py-3">
                    <p className="text-xs text-muted-foreground">{new Date(entry.eventAt).toLocaleString()}</p>
                    <p className="mt-2 text-foreground">
                      {statusLabels[entry.fromStatus]} → {statusLabels[entry.toStatus]}
                    </p>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="surface-card p-6">
            <h3 className="text-sm font-semibold">POD Document</h3>
            <p className="text-xs text-muted-foreground">Download proof of delivery once available.</p>
            {podDoc ? (
              <div className="mt-4 rounded-lg border border-border/50 bg-muted/10 px-4 py-3">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <p className="text-sm font-semibold text-foreground">POD</p>
                    <p className="text-xs text-muted-foreground">
                      Status: {podDoc.state} • Uploaded {new Date(podDoc.uploadedAt).toLocaleString()}
                    </p>
                  </div>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => window.open(podDoc.storageKey, "_blank", "noopener,noreferrer")}
                  >
                    Download
                  </Button>
                </div>
              </div>
            ) : (
              <div className="mt-4">
                <EmptyState title="POD not available" description="The POD will appear after delivery." />
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
