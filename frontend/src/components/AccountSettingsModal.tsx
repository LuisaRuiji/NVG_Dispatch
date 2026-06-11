import { useEffect, useMemo, useState } from "react";
import { applyTheme, getStoredTheme, type Theme } from "@/lib/theme";
import {
  confirmMfa,
  disableMfa,
  getMfaStatus,
  setupMfa
} from "@/features/auth/authStore";
import type { MfaSetupResponse, MfaStatusResponse } from "@/features/auth/types";

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
  const [tab, setTab] = useState<"profile" | "security" | "appearance">("profile");
  const [theme, setTheme] = useState<Theme>(() => getStoredTheme() ?? "light");
  const [mfaStatus, setMfaStatus] = useState<MfaStatusResponse | null>(null);
  const [mfaSetup, setMfaSetup] = useState<MfaSetupResponse | null>(null);
  const [mfaCode, setMfaCode] = useState("");
  const [mfaBusy, setMfaBusy] = useState(false);
  const [mfaMessage, setMfaMessage] = useState<string | null>(null);
  const roleList = useMemo(() => roles.join(", "), [roles]);

  useEffect(() => {
    if (!open) return;

    void getMfaStatus()
      .then(setMfaStatus)
      .catch(() => setMfaStatus(null));
  }, [open]);

  async function beginSetup() {
    try {
      setMfaBusy(true);
      setMfaMessage(null);
      setMfaSetup(await setupMfa());
      setMfaCode("");
    } catch (error) {
      console.error(error);
      setMfaMessage("Unable to start MFA setup.");
    } finally {
      setMfaBusy(false);
    }
  }

  async function confirmSetup() {
    try {
      setMfaBusy(true);
      setMfaMessage(null);
      const status = await confirmMfa(mfaCode);
      setMfaStatus(status);
      setMfaSetup(null);
      setMfaCode("");
      setMfaMessage("MFA enabled.");
    } catch (error) {
      console.error(error);
      setMfaMessage("Invalid MFA code.");
    } finally {
      setMfaBusy(false);
    }
  }

  async function disableCurrentMfa() {
    try {
      setMfaBusy(true);
      setMfaMessage(null);
      const status = await disableMfa(mfaCode);
      setMfaStatus(status);
      setMfaCode("");
      setMfaMessage("MFA disabled.");
    } catch (error) {
      console.error(error);
      setMfaMessage("Invalid MFA code.");
    } finally {
      setMfaBusy(false);
    }
  }

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
            onClick={() => setTab("security")}
            className={`rounded-full px-3 py-1.5 text-xs font-semibold ${
              tab === "security"
                ? "bg-[#175C99] text-white"
                : "border border-slate-200 text-slate-500 hover:text-slate-900"
            }`}
          >
            Security
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
        ) : tab === "security" ? (
          <div className="mt-6 space-y-4 text-sm text-slate-600">
            <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
              <p className="text-xs uppercase text-slate-400">Multi-factor authentication</p>
              <p className="mt-1 font-semibold text-slate-900">
                {mfaStatus?.enabled ? "Enabled" : "Not enabled"}
              </p>
            </div>

            {mfaSetup ? (
              <div className="space-y-3 rounded-xl border border-slate-200 bg-white px-4 py-3">
                <div>
                  <p className="text-xs uppercase text-slate-400">Secret</p>
                  <input
                    readOnly
                    value={mfaSetup.secretKey}
                    className="mt-1 h-10 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 text-xs text-slate-700"
                  />
                </div>
                <div>
                  <p className="text-xs uppercase text-slate-400">Authenticator URI</p>
                  <input
                    readOnly
                    value={mfaSetup.otpAuthUri}
                    className="mt-1 h-10 w-full rounded-lg border border-slate-200 bg-slate-50 px-3 text-xs text-slate-700"
                  />
                </div>
                <div>
                  <p className="text-xs uppercase text-slate-400">Code</p>
                  <input
                    value={mfaCode}
                    onChange={(event) => setMfaCode(event.target.value)}
                    inputMode="numeric"
                    autoComplete="one-time-code"
                    className="mt-1 h-10 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                  />
                </div>
                <button
                  onClick={confirmSetup}
                  disabled={mfaBusy}
                  className="rounded-lg bg-[#175C99] px-4 py-2 text-sm font-semibold text-white disabled:opacity-60"
                >
                  Confirm
                </button>
              </div>
            ) : mfaStatus?.enabled ? (
              <div className="space-y-3 rounded-xl border border-slate-200 bg-white px-4 py-3">
                <p className="text-xs uppercase text-slate-400">Disable MFA</p>
                <input
                  value={mfaCode}
                  onChange={(event) => setMfaCode(event.target.value)}
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  className="h-10 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
                />
                <button
                  onClick={disableCurrentMfa}
                  disabled={mfaBusy}
                  className="rounded-lg border border-rose-200 px-4 py-2 text-sm font-semibold text-rose-600 disabled:opacity-60"
                >
                  Disable
                </button>
              </div>
            ) : (
              <button
                onClick={beginSetup}
                disabled={mfaBusy}
                className="rounded-lg bg-[#175C99] px-4 py-2 text-sm font-semibold text-white disabled:opacity-60"
              >
                Set up MFA
              </button>
            )}

            {mfaMessage ? <p className="text-xs text-slate-500">{mfaMessage}</p> : null}
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
