import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import StatusBadge from "@/components/StatusBadge";
import EmptyState from "@/components/EmptyState";
import { Archive, Eye, PencilLine } from "lucide-react";

type InventoryItem = {
  id: string;
  name: string;
  itemType: string;
  unit: string;
  isKit: boolean;
  quantity: number;
  reorderLevel?: number | null;
  location?: string | null;
  unitValue?: number | null;
};

type EditFormState = {
  name: string;
  unit: string;
  itemType: "CONSUMABLE" | "NON_CONSUMABLE";
  reorderLevel: string;
  location: string;
  unitValue: string;
  isKit: boolean;
};

export default function InventoryPage() {
  const nav = useNavigate();
  const me = getMe();
  const { toasts, show } = useToast();
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [search, setSearch] = useState("");
  const [itemTypeFilter, setItemTypeFilter] = useState<"ALL" | "CONSUMABLE" | "NON_CONSUMABLE">("ALL");
  const [onlyLowStock, setOnlyLowStock] = useState(false);
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [viewItem, setViewItem] = useState<InventoryItem | null>(null);
  const [editItem, setEditItem] = useState<InventoryItem | null>(null);
  const [archiveItem, setArchiveItem] = useState<InventoryItem | null>(null);
  const [editForm, setEditForm] = useState<EditFormState | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!me) {
      nav("/login");
      return;
    }
    if (!me.roles?.includes("InventoryOfficer") && !me.roles?.includes("Manager")) {
      show("Access denied.", "error");
      return;
    }

  }, []);

  useEffect(() => {
    const handle = window.setTimeout(() => {
      setDebouncedSearch(search.trim());
    }, 250);

    return () => window.clearTimeout(handle);
  }, [search]);

  useEffect(() => {
    if (!me) {
      return;
    }
    if (!me.roles?.includes("InventoryOfficer") && !me.roles?.includes("Manager")) {
      return;
    }

    (async () => {
      try {
        const params = new URLSearchParams();
        if (itemTypeFilter !== "ALL") {
          params.set("itemType", itemTypeFilter);
        }
        if (onlyLowStock) {
          params.set("lowStock", "true");
        }
        if (debouncedSearch.length > 0) {
          params.set("search", debouncedSearch);
        }
        const url = params.toString()
          ? `/api/inventory?${params.toString()}`
          : "/api/inventory";
        const result = await api<InventoryItem[]>(url, { method: "GET" });
        setItems(result);
      } catch (e: any) {
        console.error(e);
        show(e?.message ?? "Failed to load inventory.", "error");
      }
    })();
  }, [itemTypeFilter, onlyLowStock, debouncedSearch]);

  const getStatus = (item: InventoryItem) => {
    if (item.reorderLevel === null || item.reorderLevel === undefined) {
      return "OK";
    }
    return item.quantity <= item.reorderLevel ? "Low" : "OK";
  };

  const editFormValid = useMemo(() => {
    if (!editForm) return false;
    return editForm.name.trim().length > 0 && editForm.unit.trim().length > 0;
  }, [editForm]);

  return (
    <div className="pb-10">
      <ToastHost toasts={toasts} />
      <PageHeader title="Inventory Overview" description="Current quantities and reorder signals." />

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 p-6 mt-6">
        <div className="mb-6 grid grid-cols-1 gap-4 md:grid-cols-[minmax(220px,280px)_minmax(180px,220px)_auto] md:items-end md:gap-6">
          <div>
            <label className="text-xs uppercase text-slate-500">Search</label>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Item name"
              className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
            />
          </div>
          <div>
            <label className="text-xs uppercase text-slate-500">Item Type</label>
            <select
              value={itemTypeFilter}
              onChange={(e) => setItemTypeFilter(e.target.value as "ALL" | "CONSUMABLE" | "NON_CONSUMABLE")}
              className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
            >
              <option value="ALL">All</option>
              <option value="CONSUMABLE">Consumable</option>
              <option value="NON_CONSUMABLE">Non-Consumable</option>
            </select>
          </div>
          <label className="inline-flex items-center gap-2 text-sm text-slate-500 md:pt-2">
            <input
              type="checkbox"
              checked={onlyLowStock}
              onChange={(e) => setOnlyLowStock(e.target.checked)}
            />
            Only Low Stock
          </label>
        </div>

        <DataTable>
          <thead className="bg-slate-50/50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3 text-left">Item</th>
              <th className="px-4 py-3 text-left">Unit</th>
              <th className="px-4 py-3 text-left">Type</th>
              <th className="px-4 py-3 text-right">Current Qty</th>
              <th className="px-4 py-3 text-right">Reorder Level</th>
              <th className="px-4 py-3 text-left">Status</th>
              <th className="px-4 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => {
              const status = getStatus(item);
              return (
                <tr key={item.id} className="border-t border-slate-100">
                  <td className="px-4 py-3 text-sm">{item.name}</td>
                  <td className="px-4 py-3 text-sm">{item.unit || "-"}</td>
                  <td className="px-4 py-3 text-sm">{item.itemType}</td>
                  <td className="px-4 py-3 text-right text-sm">{item.quantity}</td>
                  <td className="px-4 py-3 text-right text-sm">{item.reorderLevel ?? "-"}</td>
                  <td className="px-4 py-3 text-sm">
                    <StatusBadge status={status === "Low" ? "Pending" : "Approved"} />
                  </td>
                  <td className="px-4 py-3 text-right text-sm">
                    <div className="flex items-center justify-end gap-2">
                      <button
                        className="inline-flex h-8 w-8 items-center justify-center rounded-full border border-slate-200 text-slate-600 hover:text-slate-900 hover:border-slate-300"
                        title="View"
                        onClick={() => setViewItem(item)}
                      >
                        <Eye className="h-4 w-4" />
                      </button>
                      <button
                        className="inline-flex h-8 w-8 items-center justify-center rounded-full border border-slate-200 text-slate-600 hover:text-slate-900 hover:border-slate-300"
                        title="Edit"
                        onClick={() => {
                          setEditItem(item);
                          setEditForm({
                            name: item.name,
                            unit: item.unit ?? "",
                            itemType: item.itemType === "CONSUMABLE" ? "CONSUMABLE" : "NON_CONSUMABLE",
                            reorderLevel: item.reorderLevel === null || item.reorderLevel === undefined ? "" : String(item.reorderLevel),
                            location: item.location ?? "",
                            unitValue: item.unitValue === null || item.unitValue === undefined ? "" : String(item.unitValue),
                            isKit: item.isKit
                          });
                        }}
                      >
                        <PencilLine className="h-4 w-4" />
                      </button>
                      <button
                        className="inline-flex h-8 w-8 items-center justify-center rounded-full border border-slate-200 text-slate-600 hover:text-slate-900 hover:border-slate-300"
                        title="Archive"
                        onClick={() => setArchiveItem(item)}
                      >
                        <Archive className="h-4 w-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </DataTable>

        {items.length === 0 ? (
          <div className="mt-6">
            <EmptyState title="No inventory items found." description="Try another search term." />
          </div>
        ) : null}
      </div>

      {viewItem ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          onClick={() => setViewItem(null)}
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Inventory item details"
            className="w-[min(92vw,520px)] rounded-2xl border border-slate-200 bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Inventory</p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">{viewItem.name}</h2>
              </div>
              <button
                onClick={() => setViewItem(null)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>

            <div className="mt-5 grid gap-3 text-sm text-slate-600 md:grid-cols-2">
              <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
                <p className="text-xs uppercase text-slate-400">Type</p>
                <p className="mt-1 font-semibold text-slate-900">{viewItem.itemType}</p>
              </div>
              <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
                <p className="text-xs uppercase text-slate-400">Unit</p>
                <p className="mt-1 font-semibold text-slate-900">{viewItem.unit || "-"}</p>
              </div>
              <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
                <p className="text-xs uppercase text-slate-400">Quantity</p>
                <p className="mt-1 font-semibold text-slate-900">{viewItem.quantity}</p>
              </div>
              <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
                <p className="text-xs uppercase text-slate-400">Reorder Level</p>
                <p className="mt-1 font-semibold text-slate-900">{viewItem.reorderLevel ?? "-"}</p>
              </div>
              <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
                <p className="text-xs uppercase text-slate-400">Location</p>
                <p className="mt-1 font-semibold text-slate-900">{viewItem.location ?? "-"}</p>
              </div>
              <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
                <p className="text-xs uppercase text-slate-400">Unit Value</p>
                <p className="mt-1 font-semibold text-slate-900">
                  {viewItem.unitValue === null || viewItem.unitValue === undefined ? "-" : viewItem.unitValue}
                </p>
              </div>
              <div className="rounded-xl border border-slate-200 bg-white px-4 py-3 md:col-span-2">
                <p className="text-xs uppercase text-slate-400">Kit</p>
                <p className="mt-1 font-semibold text-slate-900">{viewItem.isKit ? "Yes" : "No"}</p>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {editItem && editForm ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          onClick={() => {
            if (!saving) {
              setEditItem(null);
              setEditForm(null);
            }
          }}
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Edit inventory item"
            className="w-[min(92vw,560px)] rounded-2xl border border-slate-200 bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Edit Item</p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">{editItem.name}</h2>
              </div>
              <button
                onClick={() => {
                  if (!saving) {
                    setEditItem(null);
                    setEditForm(null);
                  }
                }}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>

            <div className="mt-5 grid gap-4 text-sm md:grid-cols-2">
              <div>
                <label className="text-xs uppercase text-slate-500">Name</label>
                <input
                  value={editForm.name}
                  onChange={(e) => setEditForm({ ...editForm, name: e.target.value })}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-slate-500">Unit</label>
                <input
                  value={editForm.unit}
                  onChange={(e) => setEditForm({ ...editForm, unit: e.target.value })}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-slate-500">Item Type</label>
                <select
                  value={editForm.itemType}
                  onChange={(e) =>
                    setEditForm((current) => {
                      if (!current) return current;
                      const nextType = e.target.value as "CONSUMABLE" | "NON_CONSUMABLE";
                      return {
                        ...current,
                        itemType: nextType,
                        isKit: nextType === "CONSUMABLE" ? false : current.isKit
                      };
                    })
                  }
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                >
                  <option value="CONSUMABLE">Consumable</option>
                  <option value="NON_CONSUMABLE">Non-Consumable</option>
                </select>
              </div>
              <div>
                <label className="text-xs uppercase text-slate-500">Reorder Level</label>
                <input
                  type="number"
                  value={editForm.reorderLevel}
                  onChange={(e) => setEditForm({ ...editForm, reorderLevel: e.target.value })}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-slate-500">Location</label>
                <input
                  value={editForm.location}
                  onChange={(e) => setEditForm({ ...editForm, location: e.target.value })}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-slate-500">Unit Value</label>
                <input
                  type="number"
                  value={editForm.unitValue}
                  onChange={(e) => setEditForm({ ...editForm, unitValue: e.target.value })}
                  className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
              </div>
              <label className="inline-flex items-center gap-2 text-sm text-slate-600 md:col-span-2">
                <input
                  type="checkbox"
                  checked={editForm.isKit}
                  onChange={(e) => setEditForm({ ...editForm, isKit: e.target.checked })}
                  disabled={editForm.itemType === "CONSUMABLE"}
                />
                Mark as kit {editForm.itemType === "CONSUMABLE" ? "(non-consumables only)" : ""}
              </label>
            </div>

            <div className="mt-6 flex justify-end gap-3">
              <button
                onClick={() => {
                  setEditItem(null);
                  setEditForm(null);
                }}
                className="rounded-lg border border-slate-200 px-4 py-2 text-sm text-slate-600 hover:text-slate-900"
                disabled={saving}
              >
                Cancel
              </button>
              <button
                onClick={async () => {
                  if (!editFormValid || !editItem) return;
                  try {
                    setSaving(true);
                    const payload = {
                      name: editForm.name.trim(),
                      unit: editForm.unit.trim(),
                      itemType: editForm.itemType,
                      reorderLevel: editForm.reorderLevel === "" ? null : Number(editForm.reorderLevel),
                      location: editForm.location.trim() === "" ? null : editForm.location.trim(),
                      unitValue: editForm.unitValue === "" ? null : Number(editForm.unitValue),
                      isKit: editForm.isKit
                    };
                    const updated = await api<InventoryItem>(`/api/inventory/${editItem.id}`, {
                      method: "PUT",
                      body: JSON.stringify(payload)
                    });
                    setItems((prev) => prev.map((entry) => (entry.id === updated.id ? updated : entry)));
                    setEditItem(null);
                    setEditForm(null);
                    show("Inventory item updated.", "success");
                  } catch (e: any) {
                    console.error(e);
                    show(e?.message ?? "Failed to update item.", "error");
                  } finally {
                    setSaving(false);
                  }
                }}
                className="rounded-lg bg-[#175C99] px-4 py-2 text-sm font-semibold text-white hover:bg-[#144c7f] disabled:opacity-70"
                disabled={!editFormValid || saving}
              >
                {saving ? "Saving..." : "Save Changes"}
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {archiveItem ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
          onClick={() => setArchiveItem(null)}
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Archive inventory item"
            className="w-[min(92vw,480px)] rounded-2xl border border-slate-200 bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Archive Item</p>
                <h2 className="mt-2 text-lg font-semibold text-slate-900">{archiveItem.name}</h2>
              </div>
              <button
                onClick={() => setArchiveItem(null)}
                className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
              >
                Close
              </button>
            </div>

            <p className="mt-4 text-sm text-slate-600">
              Archiving removes the item from active lists. This action can be reversed only by an admin.
            </p>

            <div className="mt-6 flex justify-end gap-3">
              <button
                onClick={() => setArchiveItem(null)}
                className="rounded-lg border border-slate-200 px-4 py-2 text-sm text-slate-600 hover:text-slate-900"
                disabled={saving}
              >
                Cancel
              </button>
              <button
                onClick={async () => {
                  try {
                    setSaving(true);
                    await api<void>(`/api/inventory/${archiveItem.id}/archive`, { method: "PATCH" });
                    setItems((prev) => prev.filter((entry) => entry.id !== archiveItem.id));
                    setArchiveItem(null);
                    show("Inventory item archived.", "success");
                  } catch (e: any) {
                    console.error(e);
                    show(e?.message ?? "Failed to archive item.", "error");
                  } finally {
                    setSaving(false);
                  }
                }}
                className="rounded-lg bg-rose-600 px-4 py-2 text-sm font-semibold text-white hover:bg-rose-700 disabled:opacity-70"
                disabled={saving}
              >
                {saving ? "Archiving..." : "Archive"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
