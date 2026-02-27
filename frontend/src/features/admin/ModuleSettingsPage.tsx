import { useEffect, useState } from "react";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import type { ModuleSettingResponse } from "@/lib/api/types";

type ModuleRow = ModuleSettingResponse;

export default function ModuleSettingsPage() {
  const { toasts, show } = useToast();
  const [modules, setModules] = useState<ModuleRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [savingKey, setSavingKey] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        setLoading(true);
        const data = await api<ModuleRow[]>("/api/modules", { method: "GET" });
        setModules(data);
      } catch (e: any) {
        console.error(e);
        show("Failed to load module settings.", "error");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  async function toggleModule(module: ModuleRow) {
    try {
      setSavingKey(module.moduleKey);
      const next = await api<ModuleRow>(`/api/modules/${module.moduleKey}`, {
        method: "PATCH",
        body: JSON.stringify({ isEnabled: !module.isEnabled, notes: module.notes ?? null })
      });
      setModules((prev) =>
        prev.map((item) => (item.moduleKey === module.moduleKey ? next : item))
      );
      show(`${next.displayName} ${next.isEnabled ? "enabled" : "disabled"}.`, "success");
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to update module.", "error");
    } finally {
      setSavingKey(null);
    }
  }

  return (
    <div className="pb-10">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Module Maintenance"
        description="SuperAdmin can enable or disable modules. Disabled modules return maintenance responses."
      />

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 p-6">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-slate-900">Module Toggles</h3>
          <p className="text-xs text-slate-500">{modules.length} modules</p>
        </div>

        <div className="mt-4">
          <DataTable>
            <thead className="bg-slate-50/50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 text-left">Module</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-left">Updated By</th>
                <th className="px-4 py-3 text-left">Updated At</th>
                <th className="px-4 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              {modules.map((module) => (
                <tr key={module.moduleKey} className="border-t border-slate-100">
                  <td className="px-4 py-3 text-sm">
                    <div className="font-medium text-slate-900">{module.displayName}</div>
                    <div className="text-xs text-slate-500">{module.moduleKey}</div>
                  </td>
                  <td className="px-4 py-3 text-sm">
                    <span
                      className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${
                        module.isEnabled
                          ? "bg-emerald-50 text-emerald-700 border border-emerald-200"
                          : "bg-amber-50 text-amber-700 border border-amber-200"
                      }`}
                    >
                      {module.isEnabled ? "ENABLED" : "MAINTENANCE"}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-sm text-slate-500">
                    {module.updatedByUsername ?? "-"}
                  </td>
                  <td className="px-4 py-3 text-sm text-slate-500">
                    {module.updatedAt ? new Date(module.updatedAt).toLocaleString() : "-"}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button
                      onClick={() => toggleModule(module)}
                      disabled={savingKey === module.moduleKey || loading}
                      className={`rounded-lg px-3 py-2 text-xs font-semibold transition disabled:opacity-60 ${
                        module.isEnabled
                          ? "border border-amber-200 text-amber-700 hover:text-amber-800"
                          : "bg-[#175C99] text-white hover:bg-[#144c7f]"
                      }`}
                    >
                      {savingKey === module.moduleKey
                        ? "Updating..."
                        : module.isEnabled
                          ? "Disable"
                          : "Enable"}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </DataTable>
          {modules.length === 0 && !loading ? (
            <div className="mt-6">
              <EmptyState title="No modules found." description="Module settings are missing." />
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
