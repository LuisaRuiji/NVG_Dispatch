import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import TripMap from "@/components/dispatch/TripMap";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type {
  CustomerShipmentDetail,
  CustomerShipmentDocument,
  CustomerShipmentTimelineEntry
} from "./types";
import { statusLabels, type TripStatus } from "@/features/dispatch/types";
import { CheckCircle2, Circle, FileText } from "lucide-react";

const trackingSteps: TripStatus[] = [
  "DISPATCHED",
  "ENROUTE_PICKUP",
  "AT_PICKUP",
  "LOADED",
  "ENROUTE_DROPOFF",
  "AT_DROPOFF",
  "DELIVERED"
];

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
  const atwDoc = documents.find((doc) => doc.type === "ATW");
  const pickupStop = detail.stops.find((stop) => stop.stopType === "PICKUP") ?? null;
  const dropoffStop = detail.stops.find((stop) => stop.stopType === "DROPOFF") ?? null;
  const latestLocation = detail.latestDriverLocation;

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

      <div className="surface-card space-y-5 p-4 md:p-6">
        <div>
          <p className="text-xs uppercase tracking-[0.18em] text-muted-foreground">Container</p>
          <div className="mt-1 flex flex-wrap items-center gap-3">
            <h2 className="font-mono text-2xl font-semibold text-foreground">
              {detail.containerNumber ?? "Container pending"}
            </h2>
            <StatusBadge status={statusLabels[detail.status] ?? detail.status} />
          </div>
          <p className="mt-1 text-sm text-muted-foreground">Trip {detail.tripId.slice(0, 8)}</p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <StatusChip label="ATW" value={detail.atwState === "MISSING" ? "Missing" : "Received"} />
          <StatusChip label="Waybill" value={detail.waybillGenerated ? "Generated" : "Pending"} />
          <StatusChip label="POD" value={detail.podState === "VERIFIED" ? "Verified" : "Pending"} />
        </div>

        <div className="grid gap-4 text-sm md:grid-cols-2">
          <ShipmentLocation label="Pickup" location={detail.pickupLocation} time={detail.pickupTime} />
          <ShipmentLocation label="Dropoff" location={detail.dropoffLocation} time={detail.dropoffTime} />
        </div>

        <ShipmentProgressStepper status={detail.status} />
      </div>

      <div className="surface-card p-4 md:p-6">
        <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-primary">Shipment Map</p>
            <h3 className="mt-2 text-base font-semibold text-foreground">Pickup, Dropoff, and Latest Update</h3>
          </div>
          <span className="text-xs text-muted-foreground">
            {latestLocation?.recordedAt
              ? `Last update ${new Date(latestLocation.recordedAt).toLocaleString()}`
              : "No active location update"}
          </span>
        </div>
        <div className="mt-4">
          <TripMap
            pickup={{
              latitude: pickupStop?.latitude,
              longitude: pickupStop?.longitude,
              label: pickupStop?.locationText ?? detail.pickupLocation,
              detail: pickupStop?.scheduledAt
                ? new Date(pickupStop.scheduledAt).toLocaleString()
                : detail.pickupTime
                ? new Date(detail.pickupTime).toLocaleString()
                : null
            }}
            dropoff={{
              latitude: dropoffStop?.latitude,
              longitude: dropoffStop?.longitude,
              label: dropoffStop?.locationText ?? detail.dropoffLocation,
              detail: dropoffStop?.scheduledAt
                ? new Date(dropoffStop.scheduledAt).toLocaleString()
                : detail.dropoffTime
                ? new Date(detail.dropoffTime).toLocaleString()
                : null
            }}
            driver={
              latestLocation
                ? {
                    latitude: latestLocation.latitude,
                    longitude: latestLocation.longitude,
                    label: "Latest shipment location"
                  }
                : null
            }
            driverRecordedAt={latestLocation?.recordedAt}
            driverAccuracyMeters={latestLocation?.accuracyMeters}
            emptyTitle="No shipment coordinates yet"
            heightClassName="h-[20rem] md:h-[24rem]"
          />
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
              <div className="mt-4 space-y-0 border-l border-border/70 pl-4 text-sm md:space-y-3 md:border-l-0 md:pl-0">
                {timeline.map((entry, index) => (
                  <div key={`${entry.eventAt}-${index}`} className="relative mb-4 rounded-lg border border-border/50 bg-muted/10 px-4 py-3 md:mb-0">
                    <span className="absolute -left-[23px] top-4 h-3 w-3 rounded-full border-2 border-primary bg-background md:hidden" />
                    <p className="text-xs text-muted-foreground">{new Date(entry.eventAt).toLocaleString()}</p>
                    <p className="mt-2 text-foreground">
                      {statusLabels[entry.fromStatus]} to {statusLabels[entry.toStatus]}
                    </p>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="surface-card p-6">
            <h3 className="text-sm font-semibold">Documents</h3>
            <p className="text-xs text-muted-foreground">Document status is view only after submission.</p>
            <div className="mt-4 space-y-3">
              <CustomerDocumentRow
                label="ATW"
                status={detail.atwState === "MISSING" ? "Missing" : "Received"}
                doc={atwDoc}
              />
              <CustomerDocumentRow
                label="Waybill"
                status={detail.waybillGenerated ? "Generated" : "Pending"}
              />
              <CustomerDocumentRow
                label="POD"
                status={detail.podState === "VERIFIED" ? "Verified" : "Pending"}
                doc={podDoc}
              />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function StatusChip({ label, value }: { label: string; value: string }) {
  return (
    <span className="rounded-full border border-border bg-muted px-2 py-1 text-xs font-medium text-muted-foreground">
      {label}: {value}
    </span>
  );
}

function ShipmentProgressStepper({ status }: { status: TripStatus }) {
  const currentIndex = status === "CLOSED" ? trackingSteps.length - 1 : trackingSteps.indexOf(status);

  return (
    <div className="rounded-2xl border border-border bg-muted/20 p-4">
      <h3 className="text-sm font-semibold text-foreground">Shipment Progress</h3>
      <div className="mt-4">
        {trackingSteps.map((step, index) => {
          const isComplete = currentIndex >= 0 && index < currentIndex;
          const isCurrent = index === currentIndex;
          const isPastOrCurrent = isComplete || isCurrent || status === "CLOSED";

          return (
            <div key={step} className="grid grid-cols-[2rem_1fr] gap-3 pb-4 last:pb-0">
              <div className="flex flex-col items-center">
                <span
                  className={`grid h-7 w-7 place-items-center rounded-full border ${
                    isPastOrCurrent
                      ? "border-primary bg-primary text-primary-foreground"
                      : "border-border bg-background text-muted-foreground"
                  }`}
                >
                  {isPastOrCurrent ? <CheckCircle2 className="h-4 w-4" /> : <Circle className="h-3 w-3" />}
                </span>
                {index < trackingSteps.length - 1 ? (
                  <span className={`mt-1 h-full min-h-5 w-px ${isComplete ? "bg-primary" : "bg-border"}`} />
                ) : null}
              </div>
              <div>
                <p className={`text-sm font-semibold ${isCurrent ? "text-foreground" : "text-muted-foreground"}`}>
                  {statusLabels[step]}
                </p>
                {isCurrent ? <p className="mt-0.5 text-xs text-muted-foreground">Current shipment status</p> : null}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function ShipmentLocation({ label, location, time }: { label: string; location: string; time?: string | null }) {
  return (
    <div className="rounded-xl border border-border/50 bg-muted/20 px-4 py-3">
      <p className="text-xs uppercase text-muted-foreground">{label}</p>
      <p className="mt-1 text-foreground">{location}</p>
      <p className="mt-1 text-xs text-muted-foreground">
        {time ? new Date(time).toLocaleString() : "Unscheduled"}
      </p>
    </div>
  );
}

function CustomerDocumentRow({
  label,
  status,
  doc
}: {
  label: string;
  status: string;
  doc?: CustomerShipmentDocument;
}) {
  return (
    <div className="rounded-2xl border border-border/50 bg-muted/10 px-4 py-3">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex min-w-0 items-center gap-3">
          <FileText className="h-4 w-4 shrink-0 text-primary" />
          <div>
            <p className="text-sm font-semibold text-foreground">{label}</p>
            <p className="text-xs text-muted-foreground">
              Status: {status}
              {doc?.uploadedAt ? ` - Uploaded ${new Date(doc.uploadedAt).toLocaleString()}` : ""}
            </p>
          </div>
        </div>
        {doc?.storageKey ? (
          <Button
            variant="outline"
            size="sm"
            className="h-11 w-full sm:h-9 sm:w-auto"
            onClick={() => window.open(doc.storageKey, "_blank", "noopener,noreferrer")}
          >
            View
          </Button>
        ) : null}
      </div>
    </div>
  );
}
