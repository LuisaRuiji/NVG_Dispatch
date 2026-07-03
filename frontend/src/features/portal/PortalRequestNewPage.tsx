import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import LocationPinPicker from "@/components/dispatch/LocationPinPicker";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import {
  containerSizeLabels,
  tripTypeLabels,
  type ContainerSize,
  type ShipmentRequestStatusResponse,
  type TripType
} from "./types";

type FormState = {
  pickupLocation: string;
  pickupLatitude?: number | null;
  pickupLongitude?: number | null;
  dropoffLocation: string;
  dropoffLatitude?: number | null;
  dropoffLongitude?: number | null;
  requestedPickupTime: string;
  containerSize: ContainerSize;
  tripType: TripType;
  containerNumber: string;
  shippingLine: string;
  bookingNumber: string;
  cargoDescription: string;
  cargoWeight: string;
  specialInstructions: string;
};

const containerSizeOptions = Object.entries(containerSizeLabels) as [ContainerSize, string][];
const tripTypeOptions = Object.entries(tripTypeLabels) as [TripType, string][];

const fromLocalInput = (value: string) => {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
};

export default function PortalRequestNewPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const [saving, setSaving] = useState(false);
  const [atwFile, setAtwFile] = useState<File | null>(null);
  const [form, setForm] = useState<FormState>({
    pickupLocation: "",
    pickupLatitude: null,
    pickupLongitude: null,
    dropoffLocation: "",
    dropoffLatitude: null,
    dropoffLongitude: null,
    requestedPickupTime: "",
    containerSize: "TWENTY_FT",
    tripType: "PORT_PICKUP",
    containerNumber: "",
    shippingLine: "",
    bookingNumber: "",
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
        pickupLatitude: form.pickupLatitude ?? null,
        pickupLongitude: form.pickupLongitude ?? null,
        dropoffLocation: form.dropoffLocation.trim(),
        dropoffLatitude: form.dropoffLatitude ?? null,
        dropoffLongitude: form.dropoffLongitude ?? null,
        requestedPickupTime: fromLocalInput(form.requestedPickupTime),
        containerSize: form.containerSize,
        tripType: form.tripType,
        containerNumber: form.containerNumber.trim() || null,
        shippingLine: form.shippingLine.trim() || null,
        bookingNumber: form.bookingNumber.trim() || null,
        cargoDescription: form.cargoDescription.trim() || null,
        cargoWeight: form.cargoWeight ? Number(form.cargoWeight) : null,
        specialInstructions: form.specialInstructions.trim() || null
      };
      const result = await api<ShipmentRequestStatusResponse>("/api/portal/requests", {
        method: "POST",
        body: JSON.stringify(payload)
      });
      if (atwFile) {
        await api(`/api/portal/requests/${result.id}/documents`, {
          method: "POST",
          body: JSON.stringify({
            documentType: "ATW",
            storageKey: atwFile.name
          })
        });
      }
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
            <Label>Container Size</Label>
            <select
              value={form.containerSize}
              onChange={(e) => update({ containerSize: e.target.value as ContainerSize })}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
            >
              {containerSizeOptions.map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </div>
          <div className="space-y-2">
            <Label>Trip Type</Label>
            <select
              value={form.tripType}
              onChange={(e) => update({ tripType: e.target.value as TripType })}
              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
            >
              {tripTypeOptions.map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
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
          <div className="space-y-2">
            <Label>Container Number</Label>
            <Input
              value={form.containerNumber}
              onChange={(e) => update({ containerNumber: e.target.value })}
              placeholder="MSCU1234567"
            />
          </div>
          <div className="space-y-2">
            <Label>Shipping Line</Label>
            <Input
              value={form.shippingLine}
              onChange={(e) => update({ shippingLine: e.target.value })}
              placeholder="Shipping line"
            />
          </div>
          <div className="space-y-2">
            <Label>Booking Number</Label>
            <Input
              value={form.bookingNumber}
              onChange={(e) => update({ bookingNumber: e.target.value })}
              placeholder="Booking reference"
            />
          </div>
        </div>

        <div className="space-y-2">
          <Label>Map Pins</Label>
          <LocationPinPicker
            value={{
              pickupLatitude: form.pickupLatitude,
              pickupLongitude: form.pickupLongitude,
              dropoffLatitude: form.dropoffLatitude,
              dropoffLongitude: form.dropoffLongitude
            }}
            onChange={(pins) => update(pins)}
          />
          <p className="text-xs text-muted-foreground">
            Pins are optional but help dispatchers see the shipment on the operations map.
          </p>
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
          <Label>ATW (Authority to Withdraw)</Label>
          <Input
            type="file"
            accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
            onChange={(e) => setAtwFile(e.target.files?.[0] ?? null)}
          />
          <p className="text-xs text-muted-foreground">
            Upload your ATW if available. The dispatcher will also accept it via email or messenger.
          </p>
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
