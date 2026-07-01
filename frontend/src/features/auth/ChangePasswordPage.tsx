import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import ToastHost from "@/components/ToastHost";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { PasswordInput } from "@/components/ui/password-input";
import { useToast } from "@/lib/useToast";
import { changePassword, logout } from "./authStore";

export default function ChangePasswordPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!newPassword.trim()) {
      show("New password is required.", "error");
      return;
    }
    if (newPassword !== confirmPassword) {
      show("Passwords do not match.", "error");
      return;
    }

    try {
      setSaving(true);
      await changePassword(newPassword);
      show("Password updated. Sign in with your new password.", "success");
      logout();
      nav("/login", { replace: true });
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to change password.", "error");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="min-h-screen bg-background">
      <ToastHost toasts={toasts} />
      <div className="mx-auto flex min-h-screen max-w-xl items-center px-6">
        <div className="w-full surface-card p-8">
          <div className="mb-6">
            <p className="text-xs uppercase tracking-[0.25em] text-muted-foreground">Account Security</p>
            <h1 className="mt-2 text-2xl font-semibold text-foreground">Change Password</h1>
          </div>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label>New Password</Label>
              <PasswordInput
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                autoComplete="new-password"
              />
            </div>
            <div className="space-y-2">
              <Label>Confirm Password</Label>
              <PasswordInput
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                autoComplete="new-password"
              />
            </div>
            <Button type="submit" className="w-full" disabled={saving}>
              {saving ? "Updating..." : "Update Password"}
            </Button>
          </form>
        </div>
      </div>
    </div>
  );
}
