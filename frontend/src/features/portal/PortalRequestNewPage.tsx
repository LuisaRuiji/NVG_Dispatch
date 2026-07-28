import { useState } from "react";
import AddressAutocomplete from "@/components/AddressAutocomplete";
import { Link, useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/lib/useToast";
import { api, uploadFile } from "@/lib/api";
import {
  containerSizeLabels,
  tripTypeLabels,
  type ContainerSize,
  type ShipmentRequestStatusResponse,
  type TripType
} from "./types";

type FormState = {
  pickupLocation: string;
  dropoffLocation: string;
  requestedPickupDate: string;
  requestedPickupTime: string;
  containerSize: ContainerSize;
  tripType: TripType;
  containerNumber: string;
  shippingLine: string;
  bookingNumber: string;
  cargoDescription: string;
  cargoWeight: string;
  specialInstructions: string;
  pickupLatitude?: number;
  pickupLongitude?: number;
  dropoffLatitude?: number;
  dropoffLongitude?: number;
};

type AtwScanResult = {
  scanId: string;
  extractedContainerNumber?: string | null;
  extractedBookingNumber?: string | null;
  extractedShippingLine?: string | null;
  confidence: number;
  riskFlags: string[];
  analysisError?: string | null;
  suggestedPickupLocation?: string | null;
  suggestedDropoffLocation?: string | null;
  suggestedContainerSize?: ContainerSize | null;
  suggestedCargoDescription?: string | null;
  suggestedCargoWeight?: number | null;
  suggestedSpecialInstructions?: string | null;
  suggestedRequestedPickupTime?: string | null;
  atwIssueDate?: string | null;
  atwValidUntil?: string | null;
};

const containerSizeOptions = Object.entries(containerSizeLabels) as [ContainerSize, string][];
const tripTypeOptions = Object.entries(tripTypeLabels) as [TripType, string][];

const toScheduleDateTime = (date: string, time: string) => {
  if (!date) return null;
  return `${date}T${time || "00:00"}:00`;
};

const formatDocumentDate = (value?: string | null) => value
  ? new Intl.DateTimeFormat(undefined, { day: "numeric", month: "short", year: "numeric" }).format(new Date(`${value.slice(0, 10)}T00:00:00`))
  : null;

export default function PortalRequestNewPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const [saving, setSaving] = useState(false);
  const [scanning, setScanning] = useState(false);
  const [atwFile, setAtwFile] = useState<File | null>(null);
  const [atwScan, setAtwScan] = useState<AtwScanResult | null>(null);
  const [form, setForm] = useState<FormState>({
    pickupLocation: "",
    dropoffLocation: "",
    requestedPickupDate: "",
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

  const handleAtwFileChange = (file: File | null) => {
    setAtwFile(file);
    setAtwScan(null);
  };

  const handleScanAtw = async () => {
    if (!atwFile) {
      show("Choose the ATW file first.", "error");
      return;
    }

    try {
      setScanning(true);
      const result = await uploadFile<AtwScanResult>("/api/portal/requests/atw/scan", atwFile);
      if (result.analysisError) {
        setAtwScan(null);
        show(result.analysisError, "error");
        return;
      }

      setAtwScan(result);
      setForm((previous) => ({
        ...previous,
        pickupLocation: result.suggestedPickupLocation ?? previous.pickupLocation,
        dropoffLocation: result.suggestedDropoffLocation ?? previous.dropoffLocation,
        containerSize: result.suggestedContainerSize ?? previous.containerSize,
        containerNumber: result.extractedContainerNumber ?? previous.containerNumber,
        bookingNumber: result.extractedBookingNumber ?? previous.bookingNumber,
        shippingLine: result.extractedShippingLine ?? previous.shippingLine,
        requestedPickupDate: result.suggestedRequestedPickupTime?.slice(0, 10) ?? previous.requestedPickupDate,
        requestedPickupTime: result.suggestedRequestedPickupTime?.slice(11, 16) ?? previous.requestedPickupTime,
        cargoDescription: result.suggestedCargoDescription ?? previous.cargoDescription,
        cargoWeight: result.suggestedCargoWeight?.toString() ?? previous.cargoWeight,
        specialInstructions: result.suggestedSpecialInstructions ?? previous.specialInstructions
      }));
      show("ATW scanned. Review the suggested fields before creating the request.", "success");
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Could not scan the ATW.", "error");
    } finally {
      setScanning(false);
    }
  };

  const handleCreate = async () => {
    if (!atwFile || !atwScan) {
      show("Upload and scan the ATW before creating the request.", "error");
      return;
    }
    if (!form.pickupLocation.trim() || !form.dropoffLocation.trim()) {
      show("Pickup and dropoff locations are required.", "error");
      return;
    }
    try {
      setSaving(true);
      const payload = {
        pickupLocation: form.pickupLocation.trim(),
        pickupLatitude: form.pickupLatitude,
        pickupLongitude: form.pickupLongitude,
        dropoffLocation: form.dropoffLocation.trim(),
        dropoffLatitude: form.dropoffLatitude,
        dropoffLongitude: form.dropoffLongitude,
        requestedPickupTime: toScheduleDateTime(form.requestedPickupDate, form.requestedPickupTime),
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
        const analysis = await uploadFile<{
          analysisStatus: string;
          appliedFields: string[];
          riskFlags: string[];
          analysisError?: string | null;
        }>(`/api/portal/requests/${result.id}/documents/atw-upload`, atwFile, "file", { scanId: atwScan.scanId });
        if (analysis.analysisStatus === "NEEDS_REVIEW") {
          const details = analysis.appliedFields.length > 0
            ? ` Filled: ${analysis.appliedFields.join(", ")}.`
            : " No new fields were filled.";
          show(`ATW scanned and sent for dispatcher review.${details}`, "success");
        } else {
          show(analysis.analysisError ?? "ATW was uploaded, but scanning is not configured yet.", "error");
        }
      }
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
        description="Start with the ATW, then review the request details extracted from it."
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

      <section className="surface-card p-6 space-y-5" aria-labelledby="atw-intake-title">
        <div className="space-y-1">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">Step 1 of 2</p>
          <h2 id="atw-intake-title" className="text-lg font-semibold text-foreground">Upload and scan the ATW</h2>
          <p className="text-sm text-muted-foreground">
            VAIA reads the document once and suggests the container, booking, and shipping-line details. You stay in control and must review every suggestion.
          </p>
        </div>
        <div className="grid gap-4 md:grid-cols-[minmax(0,1fr)_auto] md:items-end">
          <div className="space-y-2">
            <Label htmlFor="atw-file">ATW (Authority to Withdraw)</Label>
            <Input
              id="atw-file"
              type="file"
              accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
              onChange={(e) => handleAtwFileChange(e.target.files?.[0] ?? null)}
            />
            <p className="text-xs text-muted-foreground">PDF, JPG, or PNG up to 10 MB.</p>
          </div>
          <Button type="button" onClick={handleScanAtw} disabled={!atwFile || scanning}>
            {scanning ? "Scanning ATW…" : "Scan ATW"}
          </Button>
        </div>

        {atwScan ? (
          <div className="rounded-md border border-border bg-muted/40 p-4 space-y-3" aria-live="polite">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="font-medium text-foreground">AI suggestions ready — review before submitting</p>
              <span className="text-xs text-muted-foreground">Confidence: {Math.round(atwScan.confidence * 100)}%</span>
            </div>
            {atwScan.riskFlags.length > 0 ? (
              <ul className="list-disc space-y-1 pl-5 text-sm text-muted-foreground">
                {atwScan.riskFlags.map((flag) => <li key={flag}>{flag}</li>)}
              </ul>
            ) : (
              <p className="text-sm text-muted-foreground">No missing fields were flagged. The dispatcher will still verify the ATW.</p>
            )}
            {(atwScan.atwIssueDate || atwScan.atwValidUntil) ? (
              <p className="text-sm text-muted-foreground">
                ATW dates: {atwScan.atwIssueDate ? `issued ${formatDocumentDate(atwScan.atwIssueDate)}` : "issue date not found"}
                {atwScan.atwValidUntil ? ` · valid until ${formatDocumentDate(atwScan.atwValidUntil)}` : ""}. These are document dates, not the pickup appointment.
              </p>
            ) : null}
          </div>
        ) : null}
      </section>

      <div className="surface-card p-6 space-y-5">
        <div className="space-y-1">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">Step 2 of 2</p>
          <h2 className="text-lg font-semibold text-foreground">Review shipment details</h2>
          <p className="text-sm text-muted-foreground">Locations and schedule are confirmed by you; document suggestions can be corrected below.</p>
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label>Pickup Location</Label>
            <AddressAutocomplete
              value={form.pickupLocation}
              onChange={(val, lat, lon) => update({ pickupLocation: val, pickupLatitude: lat, pickupLongitude: lon })}
              placeholder="Search pickup address..."
            />
            {atwScan?.suggestedPickupLocation ? <p className="text-xs text-muted-foreground">Suggested from the scanned ATW. Confirm the map location.</p> : null}
          </div>
          <div className="space-y-2">
            <Label>Dropoff Location</Label>
            <AddressAutocomplete
              value={form.dropoffLocation}
              onChange={(val, lat, lon) => update({ dropoffLocation: val, dropoffLatitude: lat, dropoffLongitude: lon })}
              placeholder="Search dropoff address..."
            />
            {atwScan?.suggestedDropoffLocation ? <p className="text-xs text-muted-foreground">Suggested from the scanned ATW. Confirm the map location.</p> : null}
          </div>
          <div className="space-y-2">
            <Label>Requested Pickup Date</Label>
            <Input
              type="date"
              value={form.requestedPickupDate}
              onChange={(e) => update({ requestedPickupDate: e.target.value })}
            />
            {atwScan?.suggestedRequestedPickupTime ? <p className="text-xs text-muted-foreground">Suggested from the scanned ATW.</p> : null}
          </div>
          <div className="space-y-2">
            <Label>Requested Pickup Time</Label>
            <Input
              type="time"
              value={form.requestedPickupTime}
              onChange={(e) => update({ requestedPickupTime: e.target.value })}
            />
            <p className="text-xs text-muted-foreground">Leave blank only if the customer has not confirmed a time.</p>
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
            {atwScan?.suggestedContainerSize ? <p className="text-xs text-muted-foreground">Suggested from the scanned ATW.</p> : null}
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
            {atwScan?.suggestedCargoWeight !== null && atwScan?.suggestedCargoWeight !== undefined ? <p className="text-xs text-muted-foreground">Suggested from the scanned ATW.</p> : null}
          </div>
          <div className="space-y-2">
            <Label>Container Number</Label>
            <Input
              value={form.containerNumber}
              onChange={(e) => update({ containerNumber: e.target.value })}
              placeholder="MSCU1234567"
            />
            {atwScan?.extractedContainerNumber ? <p className="text-xs text-muted-foreground">Suggested from the scanned ATW.</p> : null}
          </div>
          <div className="space-y-2">
            <Label>Shipping Line</Label>
            <Input
              value={form.shippingLine}
              onChange={(e) => update({ shippingLine: e.target.value })}
              placeholder="Shipping line"
            />
            {atwScan?.extractedShippingLine ? <p className="text-xs text-muted-foreground">Suggested from the scanned ATW.</p> : null}
          </div>
          <div className="space-y-2">
            <Label>Booking Number</Label>
            <Input
              value={form.bookingNumber}
              onChange={(e) => update({ bookingNumber: e.target.value })}
              placeholder="Booking reference"
            />
            {atwScan?.extractedBookingNumber ? <p className="text-xs text-muted-foreground">Suggested from the scanned ATW.</p> : null}
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
          {atwScan?.suggestedSpecialInstructions ? <p className="text-xs text-muted-foreground">Suggested from the scanned ATW.</p> : null}
          {atwScan?.suggestedCargoDescription ? <p className="text-xs text-muted-foreground">Suggested from the scanned ATW.</p> : null}
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
          <Button onClick={handleCreate} disabled={saving || scanning || !atwScan}>
            {saving ? "Creating Request…" : "Create Request"}
          </Button>
        </div>
      </div>
    </div>
  );
}
