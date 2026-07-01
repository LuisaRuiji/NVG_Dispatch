import { useEffect, useMemo, useState } from "react";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import { ApiRequestError, api } from "@/lib/api";
import { useToast } from "@/lib/useToast";
import { getMe } from "@/features/auth/authStore";
import { stepUp } from "@/features/auth/authStore";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/ui/password-input";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { Pencil, Trash2, UserPlus, X } from "lucide-react";

type UserSummary = {
  id: string;
  username: string;
  email?: string | null;
  isActive: boolean;
  createdAt: string;
  roles: string[];
};

type RoleSummary = {
  name: string;
};

export default function UsersPage() {
  const { toasts, show } = useToast();
  const me = getMe();
  const isSuperAdmin = me?.roles?.includes("SuperAdmin") ?? false;

  const [users, setUsers] = useState<UserSummary[]>([]);
  const [roles, setRoles] = useState<RoleSummary[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [loading, setLoading] = useState(false);

  const [createUsername, setCreateUsername] = useState("");
  const [createEmail, setCreateEmail] = useState("");
  const [createPassword, setCreatePassword] = useState("");
  const [createRole, setCreateRole] = useState("");

  const [manageRoles, setManageRoles] = useState<string[]>([]);
  const [manageActive, setManageActive] = useState(true);
  const [resetPassword, setResetPassword] = useState("");
  const [editUsername, setEditUsername] = useState("");
  const [editEmail, setEditEmail] = useState("");

  const selectedUser = useMemo(
    () => users.find((u) => u.id === selectedId) ?? null,
    [users, selectedId]
  );

  const sortedUsers = useMemo(
    () => [...users].sort((a, b) => a.username.localeCompare(b.username)),
    [users]
  );

  const roleOptions = useMemo(() => {
    if (isSuperAdmin) {
      return roles;
    }
    return roles.filter((role) => role.name !== "Admin" && role.name !== "SuperAdmin");
  }, [roles, isSuperAdmin]);

  const canAssignSuperAdmin = isSuperAdmin;

  useEffect(() => {
    (async () => {
      try {
        setLoading(true);
        const [userData, roleData] = await Promise.all([
          api<UserSummary[]>("/api/users", { method: "GET" }),
          api<RoleSummary[]>("/api/users/roles", { method: "GET" })
        ]);
        setUsers(userData);
        setRoles(roleData);
      } catch (e: any) {
        console.error(e);
        show("Failed to load users.", "error");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  useEffect(() => {
    if (!selectedUser) {
      setManageRoles([]);
      setManageActive(true);
      setResetPassword("");
      setEditUsername("");
      setEditEmail("");
      return;
    }
    setManageRoles(selectedUser.roles ?? []);
    setManageActive(selectedUser.isActive);
    setResetPassword("");
    setEditUsername(selectedUser.username);
    setEditEmail(selectedUser.email ?? "");
  }, [selectedUser]);

  const toggleRole = (value: string, next: string[], setNext: (roles: string[]) => void) => {
    if (!isSuperAdmin && (value === "Admin" || value === "SuperAdmin")) {
      return;
    }
    if (next.includes(value)) {
      setNext(next.filter((r) => r !== value));
    } else {
      setNext([...next, value]);
    }
  };

  async function refreshUsers() {
    const userData = await api<UserSummary[]>("/api/users", { method: "GET" });
    setUsers(userData);
  }

  function resetCreateForm() {
    setCreateUsername("");
    setCreateEmail("");
    setCreatePassword("");
    setCreateRole("");
  }

  function openEditUser(user: UserSummary) {
    setSelectedId(user.id);
    setEditOpen(true);
  }

  async function runWithStepUp<T>(operation: () => Promise<T>): Promise<T> {
    try {
      return await operation();
    } catch (error) {
      if (!(error instanceof ApiRequestError) || error.status !== 403) {
        throw error;
      }

      if (!me?.mfaEnabled) {
        throw new Error("Sensitive admin actions require MFA. Enable MFA from Account Settings, then try again.");
      }

      const code = window.prompt("Enter your MFA code to continue.");
      if (!code?.trim()) {
        throw new Error("MFA code is required for this action.");
      }

      await stepUp(code.trim());
      return await operation();
    }
  }

  async function handleCreate() {
    if (!createUsername.trim() || !createPassword.trim()) {
      show("Username and password are required.", "error");
      return;
    }
    if (!createRole) {
      show("Select a role.", "error");
      return;
    }
    try {
      setLoading(true);
      const created = await runWithStepUp(() => api<{ id: string; username: string }>("/api/users", {
        method: "POST",
        body: JSON.stringify({
          username: createUsername.trim(),
          password: createPassword,
          email: createEmail.trim() || null
        })
      }));
      await runWithStepUp(() => api(`/api/users/${created.id}/roles`, {
        method: "PUT",
        body: JSON.stringify({ roleNames: [createRole] })
      }));
      show("User created.", "success");
      setCreateOpen(false);
      resetCreateForm();
      await refreshUsers();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to create user.", "error");
    } finally {
      setLoading(false);
    }
  }

  async function handleUpdateRoles() {
    if (!selectedUser) return;
    try {
      setLoading(true);
      await runWithStepUp(() => api(`/api/users/${selectedUser.id}/roles`, {
        method: "PUT",
        body: JSON.stringify({ roleNames: manageRoles })
      }));
      show("Roles updated.", "success");
      await refreshUsers();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to update roles.", "error");
    } finally {
      setLoading(false);
    }
  }

  async function handleUpdateProfile() {
    if (!selectedUser) return;
    if (!editUsername.trim()) {
      show("Username is required.", "error");
      return;
    }
    try {
      setLoading(true);
      await api(`/api/users/${selectedUser.id}/profile`, {
        method: "PATCH",
        body: JSON.stringify({
          username: editUsername.trim(),
          email: editEmail.trim() || null
        })
      });
      show("User profile updated.", "success");
      await refreshUsers();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to update profile.", "error");
    } finally {
      setLoading(false);
    }
  }

  async function handleUpdateStatus() {
    if (!selectedUser) return;
    try {
      setLoading(true);
      await runWithStepUp(() => api(`/api/users/${selectedUser.id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ isActive: manageActive })
      }));
      show("User status updated.", "success");
      await refreshUsers();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to update status.", "error");
    } finally {
      setLoading(false);
    }
  }

  async function handleResetPassword() {
    if (!selectedUser) return;
    if (!resetPassword.trim()) {
      show("New password is required.", "error");
      return;
    }
    try {
      setLoading(true);
      await api(`/api/users/${selectedUser.id}/reset-password`, {
        method: "POST",
        body: JSON.stringify({ newPassword: resetPassword })
      });
      show("Password reset.", "success");
      setResetPassword("");
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to reset password.", "error");
    } finally {
      setLoading(false);
    }
  }

  async function handleDeleteUser() {
    if (!selectedUser) return;
    const confirmed = window.confirm(`Deactivate ${selectedUser.username}? This will remove account access.`);
    if (!confirmed) return;
    try {
      setLoading(true);
      await runWithStepUp(() => api(`/api/users/${selectedUser.id}`, { method: "DELETE" }));
      show("User deactivated.", "success");
      setEditOpen(false);
      setSelectedId(null);
      await refreshUsers();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to deactivate user.", "error");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="pb-10">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="User Management"
        description="Review users, assign roles, and manage account access."
        actions={
          <Button onClick={() => setCreateOpen(true)} disabled={loading} className="gap-2">
            <UserPlus className="h-4 w-4" />
            Add User
          </Button>
        }
      />

      <div className="surface-card p-6">
        <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <div className="flex items-center gap-2">
              <h3 className="text-sm font-semibold text-foreground">User Directory</h3>
              <Badge variant="secondary" className="font-mono">
                {users.length}
              </Badge>
            </div>
            <p className="mt-1 text-xs text-muted-foreground">Operational access control for dispatch and support modules.</p>
          </div>
        </div>

        <DataTable>
          <thead className="bg-muted/30 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
            <tr>
              <th className="px-6 py-4 text-left">Username</th>
              <th className="px-6 py-4 text-left">Email</th>
              <th className="px-6 py-4 text-left">Roles</th>
              <th className="px-6 py-4 text-left">Status</th>
              <th className="px-6 py-4 text-left">Created</th>
              <th className="px-6 py-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border/50">
            {sortedUsers.map((user) => (
              <tr key={user.id} className="transition-colors hover:bg-muted/40">
                <td className="px-6 py-4">
                  <div className="flex items-center gap-3">
                    <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">
                      {user.username[0]?.toUpperCase() ?? "U"}
                    </div>
                    <span className="text-sm font-semibold text-foreground">{user.username}</span>
                  </div>
                </td>
                <td className="px-6 py-4 text-sm text-muted-foreground">{user.email ?? "-"}</td>
                <td className="px-6 py-4">
                  <div className="flex flex-wrap gap-1">
                    {user.roles?.length ? (
                      user.roles.map((r) => (
                        <Badge key={r} variant="outline" className="py-0 text-[10px]">
                          {r}
                        </Badge>
                      ))
                    ) : (
                      <span className="text-sm text-muted-foreground">-</span>
                    )}
                  </div>
                </td>
                <td className="px-6 py-4">
                  <Badge
                    className={cn(
                      "text-[10px] font-bold",
                      user.isActive
                        ? "border-emerald-500/20 bg-emerald-500/10 text-emerald-600"
                        : "border-border bg-muted text-muted-foreground"
                    )}
                  >
                    {user.isActive ? "ACTIVE" : "INACTIVE"}
                  </Badge>
                </td>
                <td className="px-6 py-4 text-xs tabular-nums text-muted-foreground">
                  {new Date(user.createdAt).toLocaleDateString()}
                </td>
                <td className="px-6 py-4 text-right">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => openEditUser(user)}
                    disabled={loading}
                    className="gap-2"
                  >
                    <Pencil className="h-4 w-4" />
                    Edit
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </DataTable>

        {users.length === 0 && !loading ? (
          <EmptyState title="No users found" description="Create a user to begin managing platform access." />
        ) : null}
      </div>

      {createOpen ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4 backdrop-blur-[2px] fade-in"
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Add user"
            className="max-h-[92vh] w-[min(92vw,720px)] overflow-y-auto rounded-2xl border border-border bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.25em] text-muted-foreground">Add User</p>
                <h2 className="mt-2 text-lg font-semibold text-foreground">Create account</h2>
                <p className="mt-1 text-xs text-muted-foreground">
                  Admins can create non-admin users. SuperAdmin can create Admins.
                </p>
              </div>
              <Button variant="outline" size="icon" onClick={() => setCreateOpen(false)} aria-label="Close">
                <X className="h-4 w-4" />
              </Button>
            </div>

            <div className="mt-6 grid gap-5 md:grid-cols-2">
              <div className="space-y-2">
                <label className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">Username</label>
                <Input value={createUsername} onChange={(e) => setCreateUsername(e.target.value)} placeholder="jdoe" />
              </div>
              <div className="space-y-2">
                <label className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">Email Address</label>
                <Input
                  value={createEmail}
                  onChange={(e) => setCreateEmail(e.target.value)}
                  placeholder="john@example.com"
                  type="email"
                />
              </div>
              <div className="space-y-2">
                <label className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">Password</label>
                <PasswordInput
                  value={createPassword}
                  onChange={(e) => setCreatePassword(e.target.value)}
                  placeholder="Temporary password"
                />
              </div>
              <div className="space-y-2">
                <label className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">Initial Role</label>
                <select
                  value={createRole}
                  onChange={(e) => setCreateRole(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm transition-all duration-200 hover:border-ring/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  <option value="">Select role</option>
                  {roleOptions.map((role) => (
                    <option key={role.name} value={role.name}>
                      {role.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <Button variant="outline" onClick={() => setCreateOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreate} disabled={loading} className="gap-2">
                <UserPlus className="h-4 w-4" />
                Register User
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {editOpen && selectedUser ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4 backdrop-blur-[2px] fade-in"
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Edit user"
            className="max-h-[92vh] w-[min(92vw,760px)] overflow-y-auto rounded-2xl border border-border bg-white p-6 shadow-xl fade-up"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.25em] text-muted-foreground">Edit User</p>
                <h2 className="mt-2 text-lg font-semibold text-foreground">{selectedUser.username}</h2>
                <p className="mt-1 text-xs text-muted-foreground">{selectedUser.email ?? "No email address linked"}</p>
              </div>
              <Button variant="outline" size="icon" onClick={() => setEditOpen(false)} aria-label="Close">
                <X className="h-4 w-4" />
              </Button>
            </div>

            <div className="mt-6 flex items-center gap-3 rounded-xl border border-border/50 bg-muted/30 p-4">
              <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10 font-bold text-primary">
                {selectedUser.username[0]?.toUpperCase() ?? "U"}
              </div>
              <div>
                <p className="text-sm font-bold text-foreground">{selectedUser.username}</p>
                <p className="text-xs text-muted-foreground">
                  Created {new Date(selectedUser.createdAt).toLocaleDateString()}
                </p>
              </div>
              <div className="ml-auto">
                <Badge
                  className={selectedUser.isActive ? "border-emerald-500/20 bg-emerald-500/10 text-emerald-600" : "bg-muted text-muted-foreground"}
                >
                  {selectedUser.isActive ? "ACTIVE" : "INACTIVE"}
                </Badge>
              </div>
            </div>

            <div className="mt-6 space-y-6">
              <div className="space-y-3">
                <label className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">Profile</label>
                <div className="grid gap-4 md:grid-cols-2">
                  <div className="space-y-2">
                    <label className="text-xs text-muted-foreground">Username</label>
                    <Input value={editUsername} onChange={(e) => setEditUsername(e.target.value)} />
                  </div>
                  <div className="space-y-2">
                    <label className="text-xs text-muted-foreground">Email Address</label>
                    <Input
                      value={editEmail}
                      onChange={(e) => setEditEmail(e.target.value)}
                      type="email"
                      placeholder="No email address linked"
                    />
                  </div>
                </div>
                <Button variant="outline" size="sm" onClick={handleUpdateProfile} disabled={loading} className="mt-1">
                  Save Profile
                </Button>
              </div>

              <div className="space-y-3">
                <label className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">Assigned Roles</label>
                <div className="flex flex-wrap gap-2">
                  {roleOptions.map((role) => (
                    <label
                      key={role.name}
                      className="inline-flex cursor-pointer items-center gap-2 rounded-lg border border-border/50 bg-card px-3 py-1.5 text-xs font-medium transition-colors hover:bg-muted/30"
                    >
                      <input
                        type="checkbox"
                        className="h-4 w-4 rounded border-border text-primary focus:ring-primary/40"
                        checked={manageRoles.includes(role.name)}
                        onChange={() => toggleRole(role.name, manageRoles, setManageRoles)}
                        disabled={!isSuperAdmin && (role.name === "Admin" || (role.name === "SuperAdmin" && !canAssignSuperAdmin))}
                      />
                      {role.name}
                    </label>
                  ))}
                </div>
                <Button variant="outline" size="sm" onClick={handleUpdateRoles} disabled={loading} className="mt-1">
                  Apply Role Changes
                </Button>
              </div>

              <div className="grid gap-6 md:grid-cols-2">
                <div className="space-y-3">
                  <label className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">Connectivity</label>
                  <div className="flex items-center gap-2">
                    <select
                      value={manageActive ? "active" : "inactive"}
                      onChange={(e) => setManageActive(e.target.value === "active")}
                      className="h-9 w-full rounded-lg border border-input bg-background px-3 text-xs transition-all duration-200 hover:border-ring/50 focus-visible:outline-none focus:ring-2 focus:ring-ring"
                    >
                      <option value="active">Active</option>
                      <option value="inactive">Inactive</option>
                    </select>
                    <Button variant="outline" size="sm" onClick={handleUpdateStatus} disabled={loading}>
                      Update
                    </Button>
                  </div>
                </div>

                <div className="space-y-3">
                  <label className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">Password Reset</label>
                  <div className="flex items-center gap-2">
                    <PasswordInput
                      value={resetPassword}
                      onChange={(e) => setResetPassword(e.target.value)}
                      placeholder="New password"
                      className="h-9"
                    />
                    <Button variant="outline" size="sm" onClick={handleResetPassword} disabled={loading}>
                      Reset
                    </Button>
                  </div>
                </div>
              </div>

              <div className="flex flex-col gap-3 border-t border-border/50 pt-4 sm:flex-row sm:items-center sm:justify-between">
                <Button variant="destructive" size="sm" onClick={handleDeleteUser} disabled={loading} className="gap-2">
                  <Trash2 className="h-4 w-4" />
                  Deactivate Account
                </Button>
                <span className="text-[10px] italic text-muted-foreground">Deletion deactivates access.</span>
              </div>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
