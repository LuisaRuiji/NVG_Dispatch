import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import StatusBadge from "@/components/StatusBadge";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type {
  ShipmentRequestDetail,
  ShipmentRequestDocumentType,
  ShipmentRequestStatusResponse
} from "./types";

type FormState = {
  pickupLocation: string;
  dropoffLocation: string;
  requestedPickupTime: string;
  cargoDescription: string;
  cargoWeight: string;
  specialInstructions: string;
};

type UploadModal = {
  docType: ShipmentRequestDocumentType;
  storageKey: string;
} | null;

const docTypes: ShipmentRequestDocumentType[] = [
  "INVOICE",
  "CARGO_MANIFEST",
  "DELIVERY_INSTRUCTIONS",
  "OTHER"
];

const toLocalInput = (iso?: string | null) => {
  if (!iso) return "";
  const date = new Date(iso);
  const offset = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
};

const fromLocalInput = (value: string) => {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
};

export default function PortalRequestDetailPage() {
  const { id } = useParams();
  const nav = useNavigate();
  const { toasts, show } = useToast();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [request, setRequest] = useState<ShipmentRequestDetail | null>(null);
  const [form, setForm] = useState<FormState>({
    pickupLocation: "",
    dropoffLocation: "",
    requestedPickupTime: "",
    cargoDescription: "",
    cargoWeight: "",
    specialInstructions: ""
  });
  const [uploadModal, setUploadModal] = useState<UploadModal>(null);

  const isDraft = request?.status === "DRAFT";
  const canUpload = request?.status === "DRAFT" || request?.status === "SUBMITTED";

  const updateForm = (patch: Partial<FormState>) => setForm((prev) => ({ ...prev, ...patch }));

  const loadRequest = async () => {
    if (!id) return;
    try {
      setLoading(true);
      const detail = await api<ShipmentRequestDetail>(`/api/portal/requests/${id}`, { method: "GET" });
      setRequest(detail);
      setForm({
        pickupLocation: detail.pickupLocation ?? "",
        dropoffLocation: detail.dropoffLocation ?? "",
        requestedPickupTime: toLocalInput(detail.requestedPickupTime),
        cargoDescription: detail.cargoDescription ?? "",
        cargoWeight: detail.cargoWeight?.toString() ?? "",
        specialInstructions: detail.specialInstructions ?? ""
      });
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load request.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRequest();
  }, [id]);

  const handleSave = async () => {
    if (!id) return;
    if (!form.pickupLocation.trim() || !form.dropoffLocation.trim()) {
      show("Pickup and dropoff locations are required.", "error");
      return;
    }
    try {
      setSaving(true);
      await api<ShipmentRequestStatusResponse>(`/api/portal/requests/${id}`, {
        method: "PUT",
        body: JSON.stringify({
          pickupLocation: form.pickupLocation.trim(),
          dropoffLocation: form.dropoffLocation.trim(),
          requestedPickupTime: fromLocalInput(form.requestedPickupTime),
          cargoDescription: form.cargoDescription.trim() || null,
          cargoWeight: form.cargoWeight ? Number(form.cargoWeight) : null,
          specialInstructions: form.specialInstructions.trim() || null
        })
      });
      show("Request updated.", "success");
      await loadRequest();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to update request.", "error");
    } finally {
      setSaving(false);
    }
  };

  const handleSubmit = async () => {
    if (!id) return;
    try {
      setSaving(true);
      await api<ShipmentRequestStatusResponse>(`/api/portal/requests/${id}/submit`, { method: "POST" });
      show("Request submitted.", "success");
      await loadRequest();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to submit request.", "error");
    } finally {
      setSaving(false);
    }
  };

  const handleUpload = async () => {
    if (!id || !uploadModal) return;
    if (!uploadModal.storageKey.trim()) {
      show("Storage key is required.", "error");
      return;
    }
    try {
      setSaving(true);
      await api(`/api/portal/requests/${id}/documents`, {
        method: "POST",
        body: JSON.stringify({
          documentType: uploadModal.docType,
          storageKey: uploadModal.storageKey.trim()
        })
      });
      show("Document uploaded.", "success");
      setUploadModal(null);
      await loadRequest();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to upload document.", "error");
    } finally {
      setSaving(false);
    }
  };

  const documents = useMemo(() => request?.documents ?? [], [request]);

  if (loading) {
    return (
      <div className="space-y-6">
        <PageHeader title="Shipment Request" description="Loading request..." />
        <LoadingSkeleton rows={6} />
      </div>
    );
  }

  if (!request) {
    return <EmptyState title="Request not found" description="The request could not be loaded." />;
  }

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title={`Request ${request.id.slice(0, 8)}`}
        description="Shipment request details."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/portal/dashboard" className="text-muted-foreground hover:text-foreground">
              Portal
            </Link>
            <span className="text-muted-foreground">/</span>
            <Link to="/portal/requests" className="text-muted-foreground hover:text-foreground">
              Requests
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">Request {request.id.slice(0, 8)}</span>
          </nav>
        }
        actions={
          <Button variant="outline" onClick={() => nav("/portal/requests")}>
            Back
          </Button>
        }
      />

      <div className="surface-card p-6 space-y-6">
        <div className="flex flex-wrap items-center gap-3">
          <StatusBadge status={request.status} />
          {request.convertedTripId ? (
            <span className="text-xs rounded-full border border-emerald-200 bg-emerald-50 px-2 py-1 text-emerald-700">
              Converted to trip
            </span>
          ) : null}
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label>Pickup Location</Label>
            <Input
              value={form.pickupLocation}
              onChange={(e) => updateForm({ pickupLocation: e.target.value })}
              disabled={!isDraft}
            />
          </div>
          <div className="space-y-2">
            <Label>Dropoff Location</Label>
            <Input
              value={form.dropoffLocation}
              onChange={(e) => updateForm({ dropoffLocation: e.target.value })}
              disabled={!isDraft}
            />
          </div>
          <div className="space-y-2">
            <Label>Requested Pickup Time</Label>
            <Input
              type="datetime-local"
              value={form.requestedPickupTime}
              onChange={(e) => updateForm({ requestedPickupTime: e.target.value })}
              disabled={!isDraft}
            />
          </div>
          <div className="space-y-2">
            <Label>Cargo Weight (kg)</Label>
            <Input
              type="number"
              min="0"
              step="0.01"
              value={form.cargoWeight}
              onChange={(e) => updateForm({ cargoWeight: e.target.value })}
              disabled={!isDraft}
            />
          </div>
        </div>

        <div className="space-y-2">
          <Label>Cargo Description</Label>
          <Textarea
            value={form.cargoDescription}
            onChange={(e) => updateForm({ cargoDescription: e.target.value })}
            disabled={!isDraft}
            className="min-h-[110px]"
          />
        </div>

        <div className="space-y-2">
          <Label>Special Instructions</Label>
          <Textarea
            value={form.specialInstructions}
            onChange={(e) => updateForm({ specialInstructions: e.target.value })}
            disabled={!isDraft}
            className="min-h-[110px]"
          />
        </div>

        <div className="flex flex-wrap justify-end gap-3">
          {isDraft ? (
            <Button variant="outline" onClick={handleSave} disabled={saving}>
              Save Draft
            </Button>
          ) : null}
          {isDraft ? (
            <Button onClick={handleSubmit} disabled={saving}>
              Submit Request
            </Button>
          ) : null}
        </div>
      </div>

      <div className="surface-card p-6">
        <div className="flex items-center justify-between gap-4">
          <div>
            <h3 className="text-sm font-semibold">Request Documents</h3>
            <p className="text-xs text-muted-foreground">Attach supporting documents for dispatch review.</p>
          </div>
          <Button
            size="sm"
            variant="outline"
            onClick={() => setUploadModal({ docType: "INVOICE", storageKey: "" })}
            disabled={!canUpload}
          >
            Upload Document
          </Button>
        </div>

        {documents.length === 0 ? (
          <div className="mt-4">
            <EmptyState title="No documents yet" description="Upload invoices or delivery instructions." />
          </div>
        ) : (
          <div className="mt-4 space-y-3 text-sm">
            {documents.map((doc) => (
              <div key={doc.id} className="rounded-lg border border-border/50 bg-muted/10 px-4 py-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <p className="font-semibold text-foreground">{doc.documentType}</p>
                    <p className="text-xs text-muted-foreground">
                      Uploaded {new Date(doc.uploadedAt).toLocaleString()}
                    </p>
                  </div>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => window.open(doc.storageKey, "_blank", "noopener,noreferrer")}
                  >
                    View
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {uploadModal ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          onClick={() => setUploadModal(null)}
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
                <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Upload</p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">Request Document</h2>
              </div>
              <button
                onClick={() => setUploadModal(null)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>

            <div className="mt-5 space-y-3 text-sm">
              <div>
                <Label>Document Type</Label>
                <select
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                  value={uploadModal.docType}
                  onChange={(e) =>
                    setUploadModal({ ...uploadModal, docType: e.target.value as ShipmentRequestDocumentType })
                  }
                >
                  {docTypes.map((type) => (
                    <option key={type} value={type}>
                      {type}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <Label>Storage Key / URL</Label>
                <Input
                  value={uploadModal.storageKey}
                  onChange={(e) => setUploadModal({ ...uploadModal, storageKey: e.target.value })}
                  placeholder="docs/invoice.pdf"
                />
              </div>
            </div>

            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setUploadModal(null)}>
                Cancel
              </Button>
              <Button onClick={handleUpload} disabled={saving || !canUpload}>
                Upload
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
