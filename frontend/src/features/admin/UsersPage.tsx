import { useEffect, useMemo, useState } from "react";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import { api } from "@/lib/api";
import { useToast } from "@/lib/useToast";
import { getMe } from "@/features/auth/authStore";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { Trash2, UserPlus, Settings2 } from "lucide-react";

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
  const [loading, setLoading] = useState(false);

  const [createUsername, setCreateUsername] = useState("");
  const [createEmail, setCreateEmail] = useState("");
  const [createPassword, setCreatePassword] = useState("");
  const [createRole, setCreateRole] = useState("");

  const [manageRoles, setManageRoles] = useState<string[]>([]);
  const [manageActive, setManageActive] = useState(true);
  const [resetPassword, setResetPassword] = useState("");

  const selectedUser = useMemo(
    () => users.find((u) => u.id === selectedId) ?? null,
    [users, selectedId]
  );

  const roleOptions = useMemo(() => {
    if (isSuperAdmin) {
      const filtered = roles.filter((role) => role.name === "Admin" || role.name === "SuperAdmin");
      return filtered.length > 0 ? filtered : [{ name: "Admin" }, { name: "SuperAdmin" }];
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
      return;
    }
    setManageRoles(selectedUser.roles ?? []);
    setManageActive(selectedUser.isActive);
    setResetPassword("");
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

  async function handleCreate() {
    if (!createUsername || !createPassword) {
      show("Username and password are required.", "error");
      return;
    }
    if (!createRole) {
      show("Select a role.", "error");
      return;
    }
    try {
      setLoading(true);
      const created = await api<{ id: string; username: string }>("/api/users", {
        method: "POST",
        body: JSON.stringify({
          username: createUsername,
          password: createPassword,
          email: createEmail || null
        })
      });
      await api(`/api/users/${created.id}/roles`, {
        method: "PUT",
        body: JSON.stringify({ roleNames: [createRole] })
      });
      show("User created.", "success");
      setCreateUsername("");
      setCreateEmail("");
      setCreatePassword("");
      setCreateRole("");
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
      await api(`/api/users/${selectedUser.id}/roles`, {
        method: "PUT",
        body: JSON.stringify({ roleNames: manageRoles })
      });
      show("Roles updated.", "success");
      await refreshUsers();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to update roles.", "error");
    } finally {
      setLoading(false);
    }
  }

  async function handleUpdateStatus() {
    if (!selectedUser) return;
    try {
      setLoading(true);
      await api(`/api/users/${selectedUser.id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ isActive: manageActive })
      });
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
    if (!resetPassword) {
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
    const confirmed = window.confirm(`Delete ${selectedUser.username}? This will deactivate the account.`);
    if (!confirmed) return;
    try {
      setLoading(true);
      await api(`/api/users/${selectedUser.id}`, { method: "DELETE" });
      show("User deleted.", "success");
      setSelectedId(null);
      await refreshUsers();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to delete user.", "error");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="pb-10">
      <ToastHost toasts={toasts} />
      <PageHeader title="User Management" description="Create accounts, assign roles, and manage access." />

      <div className="grid gap-6 lg:grid-cols-[1.1fr_1fr]">
        <div className="surface-card p-6">
          <div className="flex items-center gap-2 mb-1">
            <UserPlus className="h-4 w-4 text-primary" />
            <h3 className="text-sm font-semibold text-foreground">Create User</h3>
          </div>
          <p className="text-xs text-muted-foreground/80">Admins can create non-admin users. SuperAdmin can create Admins.</p>

          <div className="mt-6 grid gap-5 md:grid-cols-2">
            <div className="space-y-2">
              <label className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">Username</label>
              <Input
                value={createUsername}
                onChange={(e) => setCreateUsername(e.target.value)}
                placeholder="jdoe"
              />
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
              <Input
                value={createPassword}
                onChange={(e) => setCreatePassword(e.target.value)}
                type="password"
                placeholder="••••••••"
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

          <Button
            onClick={handleCreate}
            disabled={loading}
            className="mt-6 w-full md:w-auto"
          >
            Register User
          </Button>
        </div>

        <div className="surface-card p-6">
          <div className="flex items-center gap-2 mb-1">
            <Settings2 className="h-4 w-4 text-primary" />
            <h3 className="text-sm font-semibold text-foreground">Account Controls</h3>
          </div>
          <p className="text-xs text-muted-foreground/80">Select a user from the directory to manage roles or reset passwords.</p>

          {selectedUser ? (
            <div className="mt-6 space-y-6 fade-in">
              <div className="flex items-center gap-3 p-4 rounded-xl bg-muted/30 border border-border/50">
                <div className="h-10 w-10 rounded-full bg-primary/10 flex items-center justify-center text-primary font-bold">
                  {selectedUser.username[0].toUpperCase()}
                </div>
                <div>
                  <p className="text-sm font-bold text-foreground">{selectedUser.username}</p>
                  <p className="text-xs text-muted-foreground">{selectedUser.email ?? "No email address linked"}</p>
                </div>
                <div className="ml-auto">
                  <Badge className={selectedUser.isActive ? "bg-emerald-500/10 text-emerald-600 border-emerald-500/20" : "bg-muted text-muted-foreground"}>
                    {selectedUser.isActive ? "ACTIVE" : "INACTIVE"}
                  </Badge>
                </div>
              </div>

              <div className="space-y-3">
                <label className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">Assigned Roles</label>
                <div className="flex flex-wrap gap-2">
                  {roleOptions.map((role) => (
                    <label key={role.name} className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg border border-border/50 bg-card text-xs font-medium cursor-pointer hover:bg-muted/30 transition-colors">
                      <input
                        type="checkbox"
                        className="rounded border-border text-primary focus:ring-primary/40 h-4 w-4"
                        checked={manageRoles.includes(role.name)}
                        onChange={() =>
                          toggleRole(role.name, manageRoles, setManageRoles)
                        }
                        disabled={!isSuperAdmin && (role.name === "Admin" || (role.name === "SuperAdmin" && !canAssignSuperAdmin))}
                      />
                      {role.name}
                    </label>
                  ))}
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleUpdateRoles}
                  disabled={loading}
                  className="mt-1"
                >
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
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={handleUpdateStatus}
                      disabled={loading}
                    >
                      Update
                    </Button>
                  </div>
                </div>

                <div className="space-y-3">
                  <label className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">Quick Reset</label>
                  <div className="flex items-center gap-2">
                    <Input
                      value={resetPassword}
                      onChange={(e) => setResetPassword(e.target.value)}
                      type="password"
                      placeholder="••••••••"
                      className="h-9"
                    />
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={handleResetPassword}
                      disabled={loading}
                    >
                      Reset
                    </Button>
                  </div>
                </div>
              </div>

              <div className="pt-4 border-t border-border/50 flex items-center justify-between">
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={handleDeleteUser}
                  disabled={loading}
                  className="gap-2"
                >
                  <Trash2 className="h-4 w-4" />
                  Deactivate Account
                </Button>
                <span className="text-[10px] text-muted-foreground italic">Note: Deletion deactivates access.</span>
              </div>
            </div>
          ) : (
            <div className="mt-8">
              <EmptyState
                title="Account Settings"
                description="Select a team member from the directory below to manage their access, roles, and security settings."
              />
            </div>
          )}
        </div>
      </div>

      <div className="surface-card p-6 mt-6">
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center gap-2">
            <h3 className="text-sm font-semibold text-foreground">User Directory</h3>
            <Badge variant="secondary" className="font-mono">{users.length}</Badge>
          </div>
          <p className="text-xs text-muted-foreground">Operational access control</p>
        </div>

        <DataTable>
          <thead className="bg-muted/30 text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground/70">
            <tr>
              <th className="px-6 py-4 text-left">Username</th>
              <th className="px-6 py-4 text-left">Email</th>
              <th className="px-6 py-4 text-left">Roles</th>
              <th className="px-6 py-4 text-left">Status</th>
              <th className="px-6 py-4 text-right">Created</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border/50">
            {users.map((user) => (
              <tr
                key={user.id}
                className={cn(
                  "cursor-pointer transition-colors hover:bg-muted/40",
                  selectedId === user.id ? "bg-primary/5" : ""
                )}
                onClick={() => setSelectedId(user.id)}
              >
                <td className="px-6 py-4">
                  <span className="text-sm font-semibold text-foreground">{user.username}</span>
                </td>
                <td className="px-6 py-4 text-sm text-muted-foreground">{user.email ?? "-"}</td>
                <td className="px-6 py-4 flex flex-wrap gap-1">
                  {user.roles?.length ? (
                    user.roles.map((r) => (
                      <Badge key={r} variant="outline" className="text-[10px] py-0">{r}</Badge>
                    ))
                  ) : "-"}
                </td>
                <td className="px-6 py-4">
                  <Badge
                    className={cn(
                      "font-bold text-[10px]",
                      user.isActive
                        ? "bg-emerald-500/10 text-emerald-600 border-emerald-500/20"
                        : "bg-muted text-muted-foreground border-border"
                    )}
                  >
                    {user.isActive ? "ACTIVE" : "INACTIVE"}
                  </Badge>
                </td>
                <td className="px-6 py-4 text-right text-xs text-muted-foreground tabular-nums">
                  {new Date(user.createdAt).toLocaleDateString()}
                </td>
              </tr>
            ))}
          </tbody>
        </DataTable>

        {users.length === 0 && !loading && (
          <EmptyState title="No users found" description="The system hasn't registered any users yet." />
        )}
      </div>
    </div>
  );
}
