import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import StatusBadge from "@/components/StatusBadge";
import EmptyState from "@/components/EmptyState";

type InventoryItem = {
  id: string;
  name: string;
  itemType: string;
  quantity: number;
  reorderLevel?: number | null;
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
              <th className="px-4 py-3 text-left">Type</th>
              <th className="px-4 py-3 text-right">Current Qty</th>
              <th className="px-4 py-3 text-right">Reorder Level</th>
              <th className="px-4 py-3 text-left">Status</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => {
              const status = getStatus(item);
              return (
                <tr key={item.id} className="border-t border-slate-100">
                  <td className="px-4 py-3 text-sm">{item.name}</td>
                  <td className="px-4 py-3 text-sm">{item.itemType}</td>
                  <td className="px-4 py-3 text-right text-sm">{item.quantity}</td>
                  <td className="px-4 py-3 text-right text-sm">{item.reorderLevel ?? "-"}</td>
                  <td className="px-4 py-3 text-sm">
                    <StatusBadge status={status === "Low" ? "Pending" : "Approved"} />
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
    </div>
  );
}
