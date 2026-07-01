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
                    className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 backdrop-blur-[2px]"
                    role="presentation"
                >
                    <div
                        role="dialog"
                        aria-modal="true"
                        className="w-[min(92vw,520px)] rounded-2xl border border-gray-200 bg-white p-8 shadow-2xl"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="flex items-start justify-between gap-4">
                            <div>
                                <p className="text-xs uppercase tracking-[0.25em] text-gray-400">Sign In</p>
                                <h2 className="mt-2 text-2xl font-semibold text-primary">Welcome back</h2>
                            </div>
                            <button
                                onClick={closeLogin}
                                className="rounded-lg border border-gray-200 px-3 py-1 text-xs text-gray-500 hover:text-gray-900"
                            >
                                Close
                            </button>
                        </div>

                        <form onSubmit={handleLogin} className="mt-6 space-y-4">
                            {!mfaChallengeId ? (
                                <>
                                    <div>
                                        <label className="text-xs uppercase text-gray-500">Username</label>
                                        <input
                                            value={username}
                                            onChange={(e) => setUsername(e.target.value)}
                                            className="mt-1 h-11 w-full rounded-lg border border-gray-200 bg-white px-3 text-sm"
                                        />
                                    </div>
                                    <div>
                                        <label className="text-xs uppercase text-gray-500">Password</label>
                                        <PasswordInput
                                            value={password}
                                            onChange={(e) => setPassword(e.target.value)}
                                            className="mt-1 h-11 rounded-lg border-gray-200 bg-white"
                                        />
                                    </div>
                                    <label className="flex items-start gap-3 rounded-lg border border-gray-200 bg-gray-50 px-3 py-3 text-sm text-gray-600">
                                        <input
                                            type="checkbox"
                                            checked={rememberMe}
                                            onChange={(e) => setRememberMe(e.target.checked)}
                                            className="mt-0.5 h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary"
                                        />
                                        <span>
                                            <span className="block font-medium text-gray-800">Remember this device</span>
                                            <span className="block text-xs text-gray-500">Stay signed in here for 7 days.</span>
                                        </span>
                                    </label>
                                </>
                            ) : (
                                <div>
                                    <label className="text-xs uppercase text-gray-500">Authenticator code</label>
                                    <input
                                        value={mfaCode}
                                        onChange={(e) => setMfaCode(e.target.value)}
                                        inputMode="numeric"
                                        autoComplete="one-time-code"
                                        className="mt-1 h-11 w-full rounded-lg border border-gray-200 bg-white px-3 text-sm"
                                    />
                                </div>
                            )}
                            <button
                                type="submit"
                                disabled={submitting}
                                className="w-full rounded-lg bg-primary px-4 py-3 text-sm font-semibold text-white hover:bg-primary/90 disabled:opacity-70"
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
