import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import { getDefaultRoute, isMfaRequiredResponse, login, verifyMfaLogin } from "./authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";

export default function LoginPage() {
  const nav = useNavigate();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [mfaCode, setMfaCode] = useState("");
  const [mfaChallengeId, setMfaChallengeId] = useState<string | null>(null);
  const { toasts, show } = useToast();

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    try {
      if (mfaChallengeId) {
        const me = await verifyMfaLogin({ challengeId: mfaChallengeId, code: mfaCode });
        nav(getDefaultRoute(me));
        return;
      }

      const me = await login({ username, password });
      if (isMfaRequiredResponse(me)) {
        setMfaChallengeId(me.challengeId);
        setMfaCode("");
        return;
      }

      nav(getDefaultRoute(me));
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Login failed", "error");
    }
  }

  return (
    <div className="min-h-screen bg-background">
      <ToastHost toasts={toasts} />
      <div className="mx-auto flex min-h-screen max-w-6xl flex-col items-center justify-center px-6">
        <div className="grid w-full items-center gap-8 lg:grid-cols-[1.2fr_1fr]">
          <div className="hidden rounded-3xl bg-nvg-gradient p-10 text-white lg:block">
            <p className="text-xs uppercase tracking-[0.3em] text-white/70">NVG Logistics</p>
            <h1 className="mt-4 text-3xl font-semibold leading-tight">
              ERP Portal for operations, inventory, and asset lifecycle control.
            </h1>
            <p className="mt-4 text-sm text-white/80">
              Secure access for Inventory Officers, Managers, Finance, and Drivers.
            </p>
          </div>

          <div className="rounded-2xl border border-border bg-white p-8 shadow-sm">
            <div className="mb-6">
              <p className="text-xs uppercase tracking-[0.25em] text-muted-foreground">Sign In</p>
              <h2 className="mt-2 text-2xl font-semibold text-foreground">Welcome back</h2>
            </div>
            <form onSubmit={onSubmit} className="space-y-4">
              {!mfaChallengeId ? (
                <>
                  <div>
                    <label className="text-xs uppercase text-muted-foreground">Username</label>
                    <input
                      value={username}
                      onChange={(e) => setUsername(e.target.value)}
                      className="mt-1 h-10 w-full rounded-lg border border-border bg-white px-3 text-sm"
                    />
                  </div>
                  <div>
                    <label className="text-xs uppercase text-muted-foreground">Password</label>
                    <input
                      type="password"
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      className="mt-1 h-10 w-full rounded-lg border border-border bg-white px-3 text-sm"
                    />
                  </div>
                </>
              ) : (
                <div>
                  <label className="text-xs uppercase text-muted-foreground">Authenticator code</label>
                  <input
                    value={mfaCode}
                    onChange={(e) => setMfaCode(e.target.value)}
                    inputMode="numeric"
                    autoComplete="one-time-code"
                    className="mt-1 h-10 w-full rounded-lg border border-border bg-white px-3 text-sm"
                  />
                </div>
              )}
              <button type="submit" className="w-full rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white">
                {mfaChallengeId ? "Verify" : "Sign in"}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  );
}
