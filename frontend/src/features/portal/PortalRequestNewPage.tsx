import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type { ShipmentRequestStatusResponse } from "./types";

type FormState = {
  pickupLocation: string;
  dropoffLocation: string;
  requestedPickupTime: string;
  cargoDescription: string;
  cargoWeight: string;
  specialInstructions: string;
};

const fromLocalInput = (value: string) => {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
};

export default function PortalRequestNewPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<FormState>({
    pickupLocation: "",
    dropoffLocation: "",
    requestedPickupTime: "",
    cargoDescription: "",
    cargoWeight: "",
    specialInstructions: ""
  });

  const update = (patch: Partial<FormState>) => setForm((prev) => ({ ...prev, ...patch }));

  const handleCreate = async () => {
    if (!form.pickupLocation.trim() || !form.dropoffLocation.trim()) {
      show("Pickup and dropoff locations are required.", "error");
      return;
    }
    try {
      setSaving(true);
      const payload = {
        pickupLocation: form.pickupLocation.trim(),
        dropoffLocation: form.dropoffLocation.trim(),
        requestedPickupTime: fromLocalInput(form.requestedPickupTime),
        cargoDescription: form.cargoDescription.trim() || null,
        cargoWeight: form.cargoWeight ? Number(form.cargoWeight) : null,
        specialInstructions: form.specialInstructions.trim() || null
      };
      const result = await api<ShipmentRequestStatusResponse>("/api/portal/requests", {
        method: "POST",
        body: JSON.stringify(payload)
      });
      show("Request created.", "success");
      nav(`/portal/requests/${result.id}`);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to create request.", "error");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="New Shipment Request"
        description="Provide pickup and dropoff details for dispatch."
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
            <span className="text-foreground">New</span>
          </nav>
        }
      />

      <div className="surface-card p-6 space-y-5">
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label>Pickup Location</Label>
            <Input
              value={form.pickupLocation}
              onChange={(e) => update({ pickupLocation: e.target.value })}
              placeholder="Warehouse A"
            />
          </div>
          <div className="space-y-2">
            <Label>Dropoff Location</Label>
            <Input
              value={form.dropoffLocation}
              onChange={(e) => update({ dropoffLocation: e.target.value })}
              placeholder="Client DC"
            />
          </div>
          <div className="space-y-2">
            <Label>Requested Pickup Time</Label>
            <Input
              type="datetime-local"
              value={form.requestedPickupTime}
              onChange={(e) => update({ requestedPickupTime: e.target.value })}
            />
          </div>
          <div className="space-y-2">
            <Label>Cargo Weight (kg)</Label>
            <Input
              type="number"
              min="0"
              step="0.01"
              value={form.cargoWeight}
              onChange={(e) => update({ cargoWeight: e.target.value })}
              placeholder="1200"
            />
          </div>
        </div>

        <div className="space-y-2">
          <Label>Cargo Description</Label>
          <Textarea
            value={form.cargoDescription}
            onChange={(e) => update({ cargoDescription: e.target.value })}
            placeholder="Palletized electronics"
            className="min-h-[110px]"
          />
        </div>

        <div className="space-y-2">
          <Label>Special Instructions</Label>
          <Textarea
            value={form.specialInstructions}
            onChange={(e) => update({ specialInstructions: e.target.value })}
            placeholder="Dock requires appointment"
            className="min-h-[110px]"
          />
        </div>

        <div className="flex justify-end gap-3">
          <Button variant="outline" onClick={() => nav("/portal/requests")}>
            Cancel
          </Button>
          <Button onClick={handleCreate} disabled={saving}>
            Create Request
          </Button>
        </div>
      </div>
    </div>
  );
}
