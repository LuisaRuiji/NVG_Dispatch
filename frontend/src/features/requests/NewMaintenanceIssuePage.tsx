import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import EmptyState from "@/components/EmptyState";

type Asset = {
  id: string;
  assetCode: string;
};

type InventoryItem = {
  id: string;
  name: string;
  unit: string;
};

type LineDraft = {
  inventoryId: string;
  inventoryName: string;
  quantity: number;
};

type CreateRequestDraftResponse = {
  requestId: string;
  status: string;
};

export default function NewMaintenanceIssuePage() {
  const nav = useNavigate();
  const me = getMe();

  const [assets, setAssets] = useState<Asset[]>([]);
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [assetId, setAssetId] = useState("");
  const [purpose, setPurpose] = useState("");
  const [search, setSearch] = useState("");
  const [selectedItemId, setSelectedItemId] = useState("");
  const [quantity, setQuantity] = useState(1);
  const [lines, setLines] = useState<LineDraft[]>([]);
  const [loading, setLoading] = useState(false);
  const { toasts, show } = useToast();

  useEffect(() => {
    if (!me) {
      nav("/login");
      return;
    }
    if (!me.roles?.includes("Driver")) {
      show("Access denied.", "error");
      return;
    }

    (async () => {
      try {
        const assetsResp = await api<Asset[]>("/api/assets", { method: "GET" });
        setAssets(assetsResp);
        setItems([]);
      } catch (e: any) {
        console.error(e);
        show(e?.message ?? "Failed to load assets.", "error");
      }
    })();
  }, []);

  useEffect(() => {
    if (!me?.roles?.includes("Driver")) {
      return;
    }
    const term = search.trim();
    if (term.length < 2) {
      setItems([]);
      return;
    }

    const handle = window.setTimeout(async () => {
      try {
        const resp = await api<InventoryItem[]>(
          `/api/inventory?itemType=CONSUMABLE&search=${encodeURIComponent(term)}`,
          { method: "GET" }
        );
        setItems(resp);
      } catch (e: any) {
        console.error(e);
        show(e?.message ?? "Failed to search inventory.", "error");
      }
    }, 250);

    return () => window.clearTimeout(handle);
  }, [search]);

  if (!me) {
    return null;
  }

  if (!me.roles?.includes("Driver")) {
    return (
      <div>
        <ToastHost toasts={toasts} />
        <PageHeader title="New Maintenance Issue" />
        <EmptyState title="Access denied." description="Driver role required." />
      </div>
    );
  }

  const filteredItems = useMemo(() => items, [items]);

  const addLine = () => {
    if (!selectedItemId) {
      show("Select an inventory item.", "error");
      return;
    }
    if (quantity <= 0) {
      show("Quantity must be greater than zero.", "error");
      return;
    }
    if (!Number.isInteger(quantity)) {
      show("Quantity must be a whole number.", "error");
      return;
    }
    if (lines.some((line) => line.inventoryId === selectedItemId)) {
      show("Duplicate inventory items are not allowed.", "error");
      return;
    }
    const item = items.find((i) => i.id === selectedItemId);
    if (!item) {
      show("Invalid inventory item.", "error");
      return;
    }
    setLines((prev) => [
      ...prev,
      { inventoryId: item.id, inventoryName: item.name, quantity }
    ]);
    setSelectedItemId("");
    setQuantity(1);
  };

  const removeLine = (inventoryId: string) => {
    setLines((prev) => prev.filter((line) => line.inventoryId !== inventoryId));
  };

  const submit = async () => {
    if (!assetId) {
      show("Asset is required.", "error");
      return;
    }
    if (lines.length === 0) {
      show("Add at least one line.", "error");
      return;
    }

    try {
      setLoading(true);
      const draft = await api<CreateRequestDraftResponse>("/api/requests", {
        method: "POST",
        body: JSON.stringify({
          requestType: "MAINTENANCE_ISSUE",
          assetId,
          purpose: purpose || null,
          lines: lines.map((line) => ({
            inventoryId: line.inventoryId,
            quantity: line.quantity,
            remarks: null
          }))
        })
      });

      await api(`/api/requests/${draft.requestId}/submit`, { method: "POST" });
      show("Request submitted.", "success");
      nav(`/requests/${draft.requestId}`);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to submit request.", "error");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <ToastHost toasts={toasts} />
      <PageHeader
        title="New Maintenance Issue"
        description="Consumables only. Asset required."
        actions={
          <button onClick={() => nav("/requests")} className="rounded-lg border border-border px-3 py-2 text-sm">
            Back
          </button>
        }
      />

      <div className="grid gap-6 lg:grid-cols-[1.2fr_1fr]">
        <div className="rounded-2xl border border-border bg-white p-5">
          <p className="text-xs uppercase text-muted-foreground">Request Details</p>
          <div className="mt-4 grid gap-4">
            <div>
              <label className="text-xs uppercase text-muted-foreground">Asset (required)</label>
              <select
                value={assetId}
                onChange={(e) => setAssetId(e.target.value)}
                className="mt-1 h-9 w-full rounded-lg border border-border bg-white px-3 text-sm"
              >
                <option value="">Select asset</option>
                {assets.map((asset) => (
                  <option key={asset.id} value={asset.id}>
                    {asset.assetCode}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="text-xs uppercase text-muted-foreground">Purpose (optional)</label>
              <input
                value={purpose}
                onChange={(e) => setPurpose(e.target.value)}
                className="mt-1 h-9 w-full rounded-lg border border-border bg-white px-3 text-sm"
              />
            </div>
          </div>
        </div>

        <div className="rounded-2xl border border-border bg-white p-5">
          <p className="text-xs uppercase text-muted-foreground">Add Line Item</p>
          <div className="mt-4 grid gap-3">
            <div>
              <label className="text-xs uppercase text-muted-foreground">Search Inventory</label>
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="mt-1 h-9 w-full rounded-lg border border-border bg-white px-3 text-sm"
              />
              {search.trim().length > 0 && search.trim().length < 2 ? (
                <p className="mt-1 text-xs text-muted-foreground">Type at least 2 characters.</p>
              ) : null}
            </div>
            <div>
              <label className="text-xs uppercase text-muted-foreground">Inventory Item</label>
              <select
                value={selectedItemId}
                onChange={(e) => setSelectedItemId(e.target.value)}
                className="mt-1 h-9 w-full rounded-lg border border-border bg-white px-3 text-sm"
              >
                <option value="">Select item</option>
                {filteredItems.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name} ({item.unit})
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-xs uppercase text-muted-foreground">Qty Requested</label>
              <input
                type="number"
                min={1}
                step={1}
                value={quantity}
                onChange={(e) => setQuantity(Number(e.target.value))}
                className="mt-1 h-9 w-full rounded-lg border border-border bg-white px-3 text-sm"
              />
            </div>
            <button
              onClick={addLine}
              disabled={loading}
              className="w-full rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white"
            >
              Add Line
            </button>
          </div>
        </div>
      </div>

      <div className="mt-6">
        <h2 className="mb-2 text-lg font-semibold">Lines</h2>
        <DataTable>
          <thead className="sticky top-0 bg-muted/40 text-xs uppercase text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left">Item</th>
              <th className="px-4 py-3 text-right">Qty Requested</th>
              <th className="px-4 py-3 text-right" />
            </tr>
          </thead>
          <tbody>
            {lines.map((line) => (
              <tr key={line.inventoryId} className="border-t border-border">
                <td className="px-4 py-3 text-sm">{line.inventoryName}</td>
                <td className="px-4 py-3 text-right text-sm">{line.quantity}</td>
                <td className="px-4 py-3 text-right text-sm">
                  <button
                    onClick={() => removeLine(line.inventoryId)}
                    disabled={loading}
                    className="rounded-lg border border-border px-2 py-1 text-xs"
                  >
                    Remove
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </DataTable>

        {lines.length === 0 ? (
          <EmptyState title="No lines yet." description="Add at least one consumable line." />
        ) : null}
      </div>

      <div className="mt-6">
        <button
          onClick={submit}
          disabled={loading}
          className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-white"
        >
          Create + Submit
        </button>
      </div>
    </div>
  );
}
