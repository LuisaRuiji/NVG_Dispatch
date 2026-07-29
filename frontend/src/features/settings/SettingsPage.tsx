import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  Bell,
  Clipboard,
  Eye,
  KeyRound,
  LayoutPanelTop,
  Monitor,
  Palette,
  ShieldCheck,
  UserRound
} from "lucide-react";
import ToastHost from "@/components/ToastHost";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PasswordInput } from "@/components/ui/password-input";
import { getMe, changePassword, confirmMfa, disableMfa, getMfaStatus, logout, setupMfa, updateMyProfile } from "@/features/auth/authStore";
import type { MfaSetupResponse, MfaStatusResponse } from "@/features/auth/types";
import { resolvePrimaryRole } from "@/features/auth/roles";
import { getSettingsPreferences, saveSettingsPreferences, type DefaultLanding, type SettingsPreferences } from "./preferences";
import { useToast } from "@/lib/useToast";

type Section = "profile" | "security" | "appearance" | "notifications" | "workspace";

const sections: { id: Section; label: string; icon: typeof UserRound }[] = [
  { id: "profile", label: "Profile", icon: UserRound },
  { id: "security", label: "Security", icon: ShieldCheck },
  { id: "appearance", label: "Appearance", icon: Palette },
  { id: "notifications", label: "Notifications", icon: Bell },
  { id: "workspace", label: "Workspace", icon: LayoutPanelTop }
];

function SectionNav({ section, onSelect }: { section: Section; onSelect: (next: Section) => void }) {
  return (
    <>
      <nav className="hidden w-48 shrink-0 space-y-1 lg:block" aria-label="Settings sections">
        <p className="mb-2 px-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">Personal settings</p>
        {sections.map(({ id, label, icon: Icon }) => (
          <button key={id} type="button" onClick={() => onSelect(id)} className={`flex min-h-10 w-full items-center gap-3 rounded-lg px-3 text-left text-sm font-semibold transition-colors ${section === id ? "bg-accent text-foreground" : "text-muted-foreground hover:bg-muted hover:text-foreground"}`}>
            <Icon className={`h-4 w-4 ${section === id ? "text-primary" : ""}`} />{label}
          </button>
        ))}
      </nav>
      <nav className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1 lg:hidden" aria-label="Settings sections">
        {sections.map(({ id, label, icon: Icon }) => <button key={id} type="button" onClick={() => onSelect(id)} className={`inline-flex h-10 shrink-0 items-center gap-2 rounded-lg border px-3 text-sm font-semibold ${section === id ? "border-primary bg-accent text-foreground" : "border-border bg-card text-muted-foreground"}`}><Icon className="h-4 w-4" />{label}</button>)}
      </nav>
    </>
  );
}

export default function SettingsPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const me = getMe();
  const roles = me?.roles ?? [];
  const primaryRole = resolvePrimaryRole(roles) ?? "User";
  const [section, setSection] = useState<Section>("profile");
  const [profile, setProfile] = useState({ username: me?.username ?? "", email: me?.email ?? "" });
  const [profileSaving, setProfileSaving] = useState(false);
  const [mfaStatus, setMfaStatus] = useState<MfaStatusResponse | null>(null);
  const [mfaSetup, setMfaSetup] = useState<MfaSetupResponse | null>(null);
  const [mfaCode, setMfaCode] = useState("");
  const [mfaBusy, setMfaBusy] = useState(false);
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [passwordBusy, setPasswordBusy] = useState(false);
  const [preferences, setPreferences] = useState<SettingsPreferences>(() => getSettingsPreferences());
  const [savedPreferences, setSavedPreferences] = useState<SettingsPreferences>(() => getSettingsPreferences());

  const profileDirty = profile.username.trim() !== (me?.username ?? "") || profile.email.trim() !== (me?.email ?? "");
  const preferencesDirty = JSON.stringify(preferences) !== JSON.stringify(savedPreferences);
  const roleGroup = useMemo(() => roles.join(", ") || "No assigned role", [roles]);
  const canManageOrganization = roles.includes("Admin") || roles.includes("SuperAdmin");

  useEffect(() => {
    void getMfaStatus().then(setMfaStatus).catch(() => setMfaStatus(null));
  }, []);

  useEffect(() => {
    if (!profileDirty && !preferencesDirty) return;
    const warnBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warnBeforeUnload);
    return () => window.removeEventListener("beforeunload", warnBeforeUnload);
  }, [profileDirty, preferencesDirty]);

  function selectSection(next: Section) {
    if (next === section) return;
    if ((section === "profile" && profileDirty) || ((section === "appearance" || section === "workspace") && preferencesDirty)) {
      if (!window.confirm("You have unsaved changes. Discard them and switch sections?")) return;
      setProfile({ username: me?.username ?? "", email: me?.email ?? "" });
      setPreferences(savedPreferences);
    }
    setSection(next);
  }

  async function saveProfile() {
    if (!profile.username.trim()) {
      show("Username is required.", "error");
      return;
    }
    setProfileSaving(true);
    try {
      const updated = await updateMyProfile(profile.username.trim(), profile.email.trim() || null);
      setProfile({ username: updated.username, email: updated.email ?? "" });
      show("Profile changes saved.", "success");
    } catch (error: any) {
      show(error?.message ?? "Unable to save profile changes. Try again.", "error");
    } finally {
      setProfileSaving(false);
    }
  }

  async function savePreferences() {
    saveSettingsPreferences(preferences);
    setSavedPreferences(preferences);
    show("Settings changes saved.", "success");
  }

  async function beginMfaSetup() {
    setMfaBusy(true);
    try {
      setMfaSetup(await setupMfa());
      setMfaCode("");
    } catch (error: any) {
      show(error?.message ?? "Unable to start MFA setup.", "error");
    } finally {
      setMfaBusy(false);
    }
  }

  async function confirmMfaSetup() {
    if (!mfaCode.trim()) { show("Enter the authenticator code to continue.", "error"); return; }
    setMfaBusy(true);
    try {
      setMfaStatus(await confirmMfa(mfaCode.trim()));
      setMfaSetup(null);
      setMfaCode("");
      show("Multi-factor authentication enabled.", "success");
    } catch (error: any) {
      show(error?.message ?? "The authenticator code is invalid.", "error");
    } finally { setMfaBusy(false); }
  }

  async function turnOffMfa() {
    if (!mfaCode.trim()) { show("Enter an authenticator code to turn off MFA.", "error"); return; }
    setMfaBusy(true);
    try {
      setMfaStatus(await disableMfa(mfaCode.trim()));
      setMfaCode("");
      show("Multi-factor authentication disabled.", "success");
    } catch (error: any) {
      show(error?.message ?? "The authenticator code is invalid.", "error");
    } finally { setMfaBusy(false); }
  }

  async function savePassword() {
    if (password.length < 8) { show("Use at least 8 characters for your new password.", "error"); return; }
    if (password !== confirmPassword) { show("Passwords do not match.", "error"); return; }
    setPasswordBusy(true);
    try {
      await changePassword(password);
      show("Password changed. Sign in again to continue.", "success");
      logout();
      nav("/login", { replace: true });
    } catch (error: any) {
      show(error?.message ?? "Unable to change password. Try again.", "error");
    } finally { setPasswordBusy(false); }
  }

  const landingOptions: { value: DefaultLanding; label: string; roles: string[] }[] = [
    { value: "dashboard", label: "Dashboard", roles: [] },
    { value: "planning", label: "Planning", roles: ["Dispatcher", "Manager"] },
    { value: "my-trips", label: "My trips", roles: ["Driver"] },
    { value: "portal", label: "Customer portal", roles: ["Customer"] }
  ];

  return (
    <div className="space-y-5">
      <ToastHost toasts={toasts} />
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div><p className="text-xs font-semibold uppercase tracking-[0.08em] text-muted-foreground">Personal workspace</p><h1 className="mt-1 text-[26px] font-bold leading-tight text-foreground">Settings</h1><p className="mt-1 text-sm text-muted-foreground">Manage your account, security, and local workspace preferences.</p></div>
        {canManageOrganization ? <Link to="/admin/users"><Button variant="outline" size="sm">Organization administration</Button></Link> : null}
      </header>

      <div className="flex flex-col gap-5 lg:flex-row">
        <SectionNav section={section} onSelect={selectSection} />
        <main className="min-w-0 flex-1 surface-card p-4 sm:p-5" aria-live="polite">
          {section === "profile" ? <section aria-labelledby="profile-title" className="max-w-3xl"><div className="flex items-center gap-3 border-b border-border pb-4"><span className="grid h-12 w-12 place-items-center rounded-lg bg-muted text-lg font-bold text-foreground">{profile.username.slice(0, 1).toUpperCase() || "U"}</span><div><h2 id="profile-title" className="text-lg font-bold">Profile</h2><p className="mt-1 text-sm text-muted-foreground">Update the identity information tied to your account.</p></div></div><div className="mt-5 grid gap-4 sm:grid-cols-2"><div><Label htmlFor="settings-username">Username</Label><Input id="settings-username" value={profile.username} onChange={(event) => setProfile((current) => ({ ...current, username: event.target.value }))} className="mt-1.5" /></div><div><Label htmlFor="settings-email">Work email</Label><Input id="settings-email" type="email" value={profile.email} onChange={(event) => setProfile((current) => ({ ...current, email: event.target.value }))} className="mt-1.5" /></div></div><div className="mt-5 grid gap-3 border-t border-border pt-5 sm:grid-cols-2"><div className="rounded-lg border border-border bg-muted/50 p-3"><p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">Primary role</p><p className="mt-1 font-semibold text-foreground">{primaryRole}</p></div><div className="rounded-lg border border-border bg-muted/50 p-3"><p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">Role group</p><p className="mt-1 text-sm text-foreground">{roleGroup}</p></div><div className="rounded-lg border border-border bg-muted/50 p-3 sm:col-span-2"><div className="flex items-start justify-between gap-3"><div><p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">User ID</p><p className="mt-1 break-all font-mono text-xs text-foreground">{me?.userId}</p></div><Button variant="outline" size="sm" onClick={async () => { try { await navigator.clipboard.writeText(me?.userId ?? ""); show("User ID copied.", "success"); } catch { show("Unable to copy user ID.", "error"); } }}><Clipboard className="h-4 w-4" />Copy user ID</Button></div></div></div><div className="mt-5 flex justify-end gap-2 border-t border-border pt-4"><Button variant="outline" disabled={!profileDirty || profileSaving} onClick={() => setProfile({ username: me?.username ?? "", email: me?.email ?? "" })}>Cancel</Button><Button disabled={!profileDirty || profileSaving} onClick={() => void saveProfile()}>{profileSaving ? "Saving changes…" : "Save changes"}</Button></div></section> : null}

          {section === "security" ? <section aria-labelledby="security-title" className="max-w-3xl"><div className="border-b border-border pb-4"><h2 id="security-title" className="text-lg font-bold">Security</h2><p className="mt-1 text-sm text-muted-foreground">Use the account protections currently available in VAIA.</p></div><div className="mt-5 grid gap-5 lg:grid-cols-2"><div className="rounded-lg border border-border p-4"><div className="flex items-start justify-between gap-3"><div><p className="font-semibold text-foreground">Multi-factor authentication</p><p className="mt-1 text-sm text-muted-foreground">{mfaStatus?.enabled ? "Authenticator verification is enabled for this account." : "Add an authenticator app to protect sign-in."}</p></div><span className={`rounded-md px-2 py-1 text-[11px] font-bold uppercase tracking-[0.08em] ${mfaStatus?.enabled ? "bg-[#D1FAE5] text-[#065F46]" : "bg-muted text-muted-foreground"}`}>{mfaStatus?.enabled ? "Enabled" : "Not enabled"}</span></div>{mfaSetup ? <div className="mt-4 space-y-3 border-t border-border pt-4"><p className="text-xs text-muted-foreground">Add this secret to your authenticator app, then enter the generated code.</p><Input readOnly value={mfaSetup.secretKey} className="font-mono text-xs" aria-label="Authenticator secret" /><div><Label htmlFor="mfa-confirm-code">Authenticator code</Label><Input id="mfa-confirm-code" inputMode="numeric" autoComplete="one-time-code" value={mfaCode} onChange={(event) => setMfaCode(event.target.value)} className="mt-1.5" /></div><div className="flex gap-2"><Button variant="outline" onClick={() => { setMfaSetup(null); setMfaCode(""); }}>Cancel setup</Button><Button disabled={mfaBusy} onClick={() => void confirmMfaSetup()}>{mfaBusy ? "Confirming…" : "Confirm MFA"}</Button></div></div> : mfaStatus?.enabled ? <div className="mt-4 border-t border-border pt-4"><Label htmlFor="mfa-disable-code">Authenticator code to disable</Label><Input id="mfa-disable-code" inputMode="numeric" autoComplete="one-time-code" value={mfaCode} onChange={(event) => setMfaCode(event.target.value)} className="mt-1.5" /><Button variant="destructive" className="mt-3" disabled={mfaBusy || !mfaCode.trim()} onClick={() => void turnOffMfa()}>{mfaBusy ? "Disabling…" : "Disable MFA"}</Button></div> : <Button className="mt-4" disabled={mfaBusy} onClick={() => void beginMfaSetup()}>{mfaBusy ? "Preparing…" : "Set up MFA"}</Button>}</div><div className="rounded-lg border border-border p-4"><div className="flex items-start gap-3"><KeyRound className="mt-0.5 h-5 w-5 text-primary" /><div><p className="font-semibold text-foreground">Change password</p><p className="mt-1 text-sm text-muted-foreground">You will be signed out after changing your password.</p></div></div><div className="mt-4 space-y-3"><div><Label htmlFor="settings-password">New password</Label><PasswordInput id="settings-password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="new-password" className="mt-1.5" /></div><div><Label htmlFor="settings-confirm-password">Confirm new password</Label><PasswordInput id="settings-confirm-password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} autoComplete="new-password" className="mt-1.5" /></div><Button disabled={passwordBusy || !password || !confirmPassword} onClick={() => void savePassword()}>{passwordBusy ? "Saving password…" : "Save new password"}</Button></div></div></div><p className="mt-5 border-t border-border pt-4 text-xs text-muted-foreground"><Eye className="mr-1 inline h-3.5 w-3.5" />Active session lists and recovery codes are not available in the current authentication service.</p></section> : null}

          {section === "appearance" ? <section aria-labelledby="appearance-title" className="max-w-3xl"><div className="border-b border-border pb-4"><h2 id="appearance-title" className="text-lg font-bold">Appearance</h2><p className="mt-1 text-sm text-muted-foreground">Personal display preferences stored safely in this browser.</p></div><div className="mt-5 space-y-5"><div className="rounded-lg border border-border p-4"><p className="font-semibold text-foreground">Theme</p><p className="mt-1 text-sm text-muted-foreground">VAIA dispatch workspaces use the documented light theme. Dark and system themes are not exposed until app-wide semantic parity is complete.</p><span className="mt-3 inline-flex rounded-md bg-accent px-2 py-1 text-xs font-semibold text-foreground">Light mode</span></div><PreferenceChoice label="Layout density" description="Controls page spacing in the authenticated workspace." value={preferences.density} onChange={(density) => setPreferences((current) => ({ ...current, density: density as SettingsPreferences["density"] }))} options={[{ value: "comfortable", label: "Comfortable" }, { value: "compact", label: "Compact" }]} /><PreferenceChoice label="Font size" description="Choose the readable text scale for this workspace." value={preferences.fontSize} onChange={(fontSize) => setPreferences((current) => ({ ...current, fontSize: fontSize as SettingsPreferences["fontSize"] }))} options={[{ value: "small", label: "Small" }, { value: "medium", label: "Medium" }, { value: "large", label: "Large" }]} /><label className="flex items-start justify-between gap-4 rounded-lg border border-border p-4"><span><span className="block font-semibold text-foreground">Reduce motion</span><span className="mt-1 block text-sm text-muted-foreground">Minimize nonessential transitions and animations.</span></span><input type="checkbox" checked={preferences.reducedMotion} onChange={(event) => setPreferences((current) => ({ ...current, reducedMotion: event.target.checked }))} className="mt-1 h-4 w-4 rounded border-border text-primary focus:ring-primary" /></label></div><SaveFooter dirty={preferencesDirty} onCancel={() => setPreferences(savedPreferences)} onSave={() => void savePreferences()} /></section> : null}

          {section === "notifications" ? <section aria-labelledby="notifications-title" className="max-w-3xl"><div className="border-b border-border pb-4"><h2 id="notifications-title" className="text-lg font-bold">Notifications</h2><p className="mt-1 text-sm text-muted-foreground">Review how live operational activity reaches this workspace.</p></div><div className="mt-5 rounded-lg border border-border p-4"><div className="flex items-start gap-3"><Bell className="mt-0.5 h-5 w-5 text-primary" /><div><p className="font-semibold text-foreground">In-app operational alerts</p><p className="mt-1 text-sm text-muted-foreground">VAIA currently delivers role-relevant updates in the notification panel, including bookings, document issues, trip status changes, and dispatch exceptions.</p></div></div><p className="mt-4 border-t border-border pt-4 text-xs text-muted-foreground">Per-event notification preferences and email delivery controls are not supported by the current notification service, so no inactive controls are shown here.</p></div></section> : null}

          {section === "workspace" ? <section aria-labelledby="workspace-title" className="max-w-3xl"><div className="border-b border-border pb-4"><h2 id="workspace-title" className="text-lg font-bold">Workspace</h2><p className="mt-1 text-sm text-muted-foreground">Set the real workspace default currently supported by VAIA.</p></div><div className="mt-5 rounded-lg border border-border p-4"><div className="flex items-start gap-3"><Monitor className="mt-0.5 h-5 w-5 text-primary" /><div><p className="font-semibold text-foreground">Default landing page</p><p className="mt-1 text-sm text-muted-foreground">Applied after your next sign-in. Planning is the recommended starting point for Dispatchers.</p></div></div><div className="mt-4 grid gap-2 sm:grid-cols-2">{landingOptions.filter((option) => option.roles.length === 0 || option.roles.some((role) => roles.includes(role as typeof roles[number]))).map((option) => <label key={option.value} className={`flex cursor-pointer items-center gap-3 rounded-lg border p-3 text-sm ${preferences.defaultLanding === option.value ? "border-primary bg-accent" : "border-border"}`}><input type="radio" name="default-landing" checked={preferences.defaultLanding === option.value} onChange={() => setPreferences((current) => ({ ...current, defaultLanding: option.value }))} className="h-4 w-4 text-primary focus:ring-primary" /><span className="font-semibold text-foreground">{option.label}</span>{option.value === "planning" ? <span className="ml-auto text-[11px] text-primary">Recommended</span> : null}</label>)}</div></div><p className="mt-5 text-xs text-muted-foreground">Timezone, date-format, saved table-layout, and default-filter preferences are not yet backed by the application, so they are intentionally unavailable.</p><SaveFooter dirty={preferencesDirty} onCancel={() => setPreferences(savedPreferences)} onSave={() => void savePreferences()} /></section> : null}
        </main>
      </div>
    </div>
  );
}

function PreferenceChoice({ label, description, value, options, onChange }: { label: string; description: string; value: string; options: { value: string; label: string }[]; onChange: (value: string) => void }) {
  return <div className="rounded-lg border border-border p-4"><p className="font-semibold text-foreground">{label}</p><p className="mt-1 text-sm text-muted-foreground">{description}</p><div className="mt-3 flex flex-wrap gap-2">{options.map((option) => <button key={option.value} type="button" onClick={() => onChange(option.value)} className={`h-10 rounded-lg border px-3 text-sm font-semibold ${value === option.value ? "border-primary bg-accent text-foreground" : "border-border bg-card text-muted-foreground hover:bg-muted"}`}>{option.label}</button>)}</div></div>;
}

function SaveFooter({ dirty, onCancel, onSave }: { dirty: boolean; onCancel: () => void; onSave: () => void }) {
  return <div className="mt-5 flex justify-end gap-2 border-t border-border pt-4"><Button variant="outline" disabled={!dirty} onClick={onCancel}>Cancel</Button><Button disabled={!dirty} onClick={onSave}>Save changes</Button></div>;
}
