import React, { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { getDefaultRoute, isMfaRequiredResponse, login, verifyMfaLogin } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";
import { PasswordInput } from "@/components/ui/password-input";

export default function LoginModal() {
    const nav = useNavigate();
    const location = useLocation();
    const initialLoginOpen = location.pathname === "/login";
    const [loginOpen, setLoginOpen] = useState(initialLoginOpen);
    const [username, setUsername] = useState("");
    const [password, setPassword] = useState("");
    const [rememberMe, setRememberMe] = useState(false);
    const [mfaCode, setMfaCode] = useState("");
    const [mfaChallengeId, setMfaChallengeId] = useState<string | null>(null);
    const [submitting, setSubmitting] = useState(false);
    const { toasts, show } = useToast();

    useEffect(() => {
        setLoginOpen(location.pathname === "/login");
    }, [location.pathname]);

    const closeLogin = () => {
        setLoginOpen(false);
        setMfaChallengeId(null);
            setMfaCode("");
        if (location.pathname === "/login") {
            nav("/", { replace: true });
        }
    };

    const handleLogin = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            setSubmitting(true);
            if (mfaChallengeId) {
                const me = await verifyMfaLogin({ challengeId: mfaChallengeId, code: mfaCode, rememberMe });
                nav(getDefaultRoute(me), { replace: true });
                return;
            }

            const me = await login({ username, password, rememberMe });
            if (isMfaRequiredResponse(me)) {
                setMfaChallengeId(me.challengeId);
                setMfaCode("");
                return;
            }

            nav(getDefaultRoute(me), { replace: true });
        } catch (e: any) {
            console.error(e);
            show(e?.message ?? "Login failed", "error");
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <>
            <ToastHost toasts={toasts} />
            {loginOpen ? (
                <div
                    className="fixed inset-0 z-[60] flex items-center justify-center bg-[#122442]/35"
                    role="presentation"
                >
                    <div
                        role="dialog"
                        aria-modal="true"
                        className="w-[min(92vw,520px)] rounded-xl border border-border bg-white p-6 sm:p-8"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="flex items-start justify-between gap-4">
                            <div>
                                <p className="text-xs uppercase tracking-[0.08em] text-muted-foreground">VAIA secure access</p>
                                <h2 className="mt-2 text-2xl font-bold text-foreground">Sign in to operations</h2>
                            </div>
                            <button
                                onClick={closeLogin}
                                className="rounded-lg border border-border px-3 py-2 text-xs text-muted-foreground hover:bg-muted hover:text-foreground"
                            >
                                Close
                            </button>
                        </div>

                        <form onSubmit={handleLogin} className="mt-6 space-y-4">
                            {!mfaChallengeId ? (
                                <>
                                    <div>
                                        <label className="text-xs uppercase tracking-[0.08em] text-muted-foreground">Username</label>
                                        <input
                                            value={username}
                                            onChange={(e) => setUsername(e.target.value)}
                                            className="mt-1 h-10 w-full rounded-lg border border-border bg-white px-3 text-sm"
                                        />
                                    </div>
                                    <div>
                                        <label className="text-xs uppercase tracking-[0.08em] text-muted-foreground">Password</label>
                                        <PasswordInput
                                            value={password}
                                            onChange={(e) => setPassword(e.target.value)}
                                            className="mt-1 h-10 rounded-lg border-border bg-white"
                                        />
                                    </div>
                                    <label className="flex items-start gap-3 rounded-lg border border-border bg-muted px-3 py-3 text-sm text-muted-foreground">
                                        <input
                                            type="checkbox"
                                            checked={rememberMe}
                                            onChange={(e) => setRememberMe(e.target.checked)}
                                            className="mt-0.5 h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary"
                                        />
                                        <span>
                                            <span className="block font-medium text-foreground">Remember this device</span>
                                            <span className="block text-xs text-muted-foreground">Stay signed in here for 7 days.</span>
                                        </span>
                                    </label>
                                </>
                            ) : (
                                <div>
                                    <label className="text-xs uppercase tracking-[0.08em] text-muted-foreground">Authenticator code</label>
                                    <input
                                        value={mfaCode}
                                        onChange={(e) => setMfaCode(e.target.value)}
                                        inputMode="numeric"
                                        autoComplete="one-time-code"
                                        className="mt-1 h-10 w-full rounded-lg border border-border bg-white px-3 text-sm"
                                    />
                                </div>
                            )}
                            <button
                                type="submit"
                                disabled={submitting}
                                className="w-full rounded-lg bg-primary px-4 py-3 text-sm font-semibold text-white hover:bg-[#E65300] disabled:opacity-70"
                            >
                                {submitting ? "Signing in..." : mfaChallengeId ? "Verify" : "Sign in"}
                            </button>
                        </form>
                    </div>
                </div>
            ) : null}
        </>
    );
}
