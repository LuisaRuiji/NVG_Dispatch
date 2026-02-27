import { useMemo, useState } from "react";
import { applyTheme, getStoredTheme, type Theme } from "@/lib/theme";

type Props = {
  open: boolean;
  onClose: () => void;
  username: string;
  userId: string;
  roles: string[];
  primaryRole: string;
};

export default function AccountSettingsModal({
  open,
  onClose,
  username,
  userId,
  roles,
  primaryRole
}: Props) {
  const [tab, setTab] = useState<"profile" | "appearance">("profile");
  const [theme, setTheme] = useState<Theme>(() => getStoredTheme() ?? "light");
  const roleList = useMemo(() => roles.join(", "), [roles]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
      onClick={onClose}
      role="presentation"
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Account settings"
        className="w-[min(92vw,540px)] rounded-2xl border border-slate-200 bg-white p-6 shadow-xl fade-up"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Settings</p>
            <h2 className="mt-2 text-lg font-semibold text-slate-900">Account Preferences</h2>
          </div>
          <button
            onClick={onClose}
            className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
          >
            Close
          </button>
        </div>

        <div className="mt-4 flex gap-2">
          <button
            onClick={() => setTab("profile")}
            className={`rounded-full px-3 py-1.5 text-xs font-semibold ${
              tab === "profile"
                ? "bg-[#175C99] text-white"
                : "border border-slate-200 text-slate-500 hover:text-slate-900"
            }`}
          >
            Profile
          </button>
          <button
            onClick={() => setTab("appearance")}
            className={`rounded-full px-3 py-1.5 text-xs font-semibold ${
              tab === "appearance"
                ? "bg-[#175C99] text-white"
                : "border border-slate-200 text-slate-500 hover:text-slate-900"
            }`}
          >
            Appearance
          </button>
        </div>

        {tab === "profile" ? (
          <div className="mt-6 space-y-4 text-sm text-slate-600">
            <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
              <p className="text-xs uppercase text-slate-400">Username</p>
              <p className="mt-1 font-semibold text-slate-900">{username}</p>
            </div>
            <div className="grid gap-3 md:grid-cols-2">
              <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
                <p className="text-xs uppercase text-slate-400">Primary Role</p>
                <p className="mt-1 font-semibold text-slate-900">{primaryRole}</p>
              </div>
              <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
                <p className="text-xs uppercase text-slate-400">User ID</p>
                <p className="mt-1 text-xs text-slate-500 break-all">{userId}</p>
              </div>
            </div>
            <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
              <p className="text-xs uppercase text-slate-400">Role Group</p>
              <p className="mt-1 text-slate-600">{roleList || "—"}</p>
            </div>
          </div>
        ) : (
          <div className="mt-6 space-y-4 text-sm text-slate-600">
            <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
              <p className="text-xs uppercase text-slate-400">Theme</p>
              <div className="mt-3 flex flex-wrap gap-2">
                {(["light", "dark"] as Theme[]).map((value) => (
                  <button
                    key={value}
                    onClick={() => {
                      setTheme(value);
                      applyTheme(value);
                    }}
                    className={`rounded-full px-3 py-1.5 text-xs font-semibold ${
                      theme === value
                        ? "bg-[#175C99] text-white"
                        : "border border-slate-200 text-slate-500 hover:text-slate-900"
                    }`}
                  >
                    {value === "light" ? "Light" : "Dark"}
                  </button>
                ))}
              </div>
            </div>
            <div className="rounded-xl border border-slate-200 bg-white px-4 py-3">
              <p className="text-xs uppercase text-slate-400">Layout Density</p>
              <p className="mt-1 text-slate-600">Comfortable</p>
            </div>
            <p className="text-xs text-slate-400">
              Theme selection is saved per browser.
            </p>
          </div>
        )}

        <div className="mt-6 flex justify-end">
          <button
            onClick={onClose}
            className="rounded-lg bg-[#175C99] px-4 py-2 text-sm font-semibold text-white hover:bg-[#144c7f]"
          >
            Done
          </button>
        </div>
      </div>
    </div>
  );
}
