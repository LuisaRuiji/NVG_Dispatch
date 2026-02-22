import { useEffect, useMemo, useState } from "react";
import PageHeader from "@/components/PageHeader";
import DataTable from "@/components/DataTable";
import EmptyState from "@/components/EmptyState";
import ToastHost from "@/components/ToastHost";
import { api } from "@/lib/api";
import { useToast } from "@/lib/useToast";
import { getMe } from "@/features/auth/authStore";

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
  const [createRoles, setCreateRoles] = useState<string[]>([]);

  const [manageRoles, setManageRoles] = useState<string[]>([]);
  const [manageActive, setManageActive] = useState(true);
  const [resetPassword, setResetPassword] = useState("");

  const selectedUser = useMemo(
    () => users.find((u) => u.id === selectedId) ?? null,
    [users, selectedId]
  );

  const roleOptions = useMemo(() => {
    const filtered = roles.filter((role) => role.name === "Admin" || role.name === "SuperAdmin");
    if (filtered.length > 0) {
      return filtered;
    }
    return [{ name: "Admin" }, { name: "SuperAdmin" }];
  }, [roles]);

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
      if (createRoles.length > 0) {
        await api(`/api/users/${created.id}/roles`, {
          method: "PUT",
          body: JSON.stringify({ roleNames: createRoles })
        });
      }
      show("User created.", "success");
      setCreateUsername("");
      setCreateEmail("");
      setCreatePassword("");
      setCreateRoles([]);
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

  return (
    <div className="pb-10">
      <ToastHost toasts={toasts} />
      <PageHeader title="User Management" description="Create accounts, assign roles, and manage access." />

      <div className="grid gap-6 lg:grid-cols-[1.1fr_1fr]">
        <div className="bg-white rounded-2xl shadow-sm border border-slate-100 p-6">
          <h3 className="text-sm font-semibold text-slate-900">Create User</h3>
          <p className="mt-1 text-sm text-slate-500">Admins can create non-admin users. SuperAdmin can create Admins.</p>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <div>
              <label className="text-xs uppercase text-slate-500">Username</label>
              <input
                value={createUsername}
                onChange={(e) => setCreateUsername(e.target.value)}
                className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
              />
            </div>
            <div>
              <label className="text-xs uppercase text-slate-500">Email</label>
              <input
                value={createEmail}
                onChange={(e) => setCreateEmail(e.target.value)}
                className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
              />
            </div>
            <div>
              <label className="text-xs uppercase text-slate-500">Password</label>
              <input
                value={createPassword}
                onChange={(e) => setCreatePassword(e.target.value)}
                type="password"
                className="mt-2 h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm"
              />
            </div>
            <div>
              <label className="text-xs uppercase text-slate-500">Roles</label>
              <div className="mt-2 flex flex-wrap gap-2">
                {roleOptions.map((role) => (
                  <label key={role.name} className="inline-flex items-center gap-2 text-sm text-slate-600">
                    <input
                      type="checkbox"
                      checked={createRoles.includes(role.name)}
                      onChange={() =>
                        toggleRole(role.name, createRoles, setCreateRoles)
                      }
                      disabled={!isSuperAdmin && (role.name === "Admin" || (role.name === "SuperAdmin" && !canAssignSuperAdmin))}
                    />
                    {role.name}
                  </label>
                ))}
              </div>
            </div>
          </div>
          <button
            onClick={handleCreate}
            disabled={loading}
            className="mt-4 rounded-lg bg-[#175C99] px-4 py-2 text-sm font-semibold text-white disabled:opacity-60"
          >
            Create User
          </button>
        </div>

        <div className="bg-white rounded-2xl shadow-sm border border-slate-100 p-6">
          <h3 className="text-sm font-semibold text-slate-900">Manage User</h3>
          <p className="mt-1 text-sm text-slate-500">Select a user from the table to manage roles or reset passwords.</p>
          {selectedUser ? (
            <div className="mt-4 space-y-4">
              <div>
                <p className="text-sm font-semibold text-slate-900">{selectedUser.username}</p>
                <p className="text-xs text-slate-500">{selectedUser.email ?? "No email"}</p>
              </div>

              <div>
                <label className="text-xs uppercase text-slate-500">Roles</label>
                <div className="mt-2 flex flex-wrap gap-2">
                  {roleOptions.map((role) => (
                    <label key={role.name} className="inline-flex items-center gap-2 text-sm text-slate-600">
                      <input
                        type="checkbox"
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
                <button
                  onClick={handleUpdateRoles}
                  disabled={loading}
                  className="mt-3 rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-600 hover:text-slate-900 disabled:opacity-60"
                >
                  Update Roles
                </button>
              </div>

              <div>
                <label className="text-xs uppercase text-slate-500">Status</label>
                <div className="mt-2 flex items-center gap-3">
                  <select
                    value={manageActive ? "active" : "inactive"}
                    onChange={(e) => setManageActive(e.target.value === "active")}
                    className="h-9 rounded-lg border border-slate-200 bg-white px-3 text-sm"
                  >
                    <option value="active">Active</option>
                    <option value="inactive">Inactive</option>
                  </select>
                  <button
                    onClick={handleUpdateStatus}
                    disabled={loading}
                    className="rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-600 hover:text-slate-900 disabled:opacity-60"
                  >
                    Save Status
                  </button>
                </div>
              </div>

              <div>
                <label className="text-xs uppercase text-slate-500">Reset Password</label>
                <div className="mt-2 flex flex-wrap items-center gap-3">
                  <input
                    value={resetPassword}
                    onChange={(e) => setResetPassword(e.target.value)}
                    type="password"
                    className="h-9 w-60 rounded-lg border border-slate-200 bg-white px-3 text-sm"
                  />
                  <button
                    onClick={handleResetPassword}
                    disabled={loading}
                    className="rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-600 hover:text-slate-900 disabled:opacity-60"
                  >
                    Reset Password
                  </button>
                </div>
              </div>
            </div>
          ) : (
            <div className="mt-6 text-sm text-slate-500">Select a user from the table.</div>
          )}
        </div>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 p-6 mt-6">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-slate-900">Users</h3>
          <p className="text-xs text-slate-500">{users.length} total</p>
        </div>
        <div className="mt-4">
          <DataTable>
            <thead className="bg-slate-50/50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 text-left">Username</th>
                <th className="px-4 py-3 text-left">Email</th>
                <th className="px-4 py-3 text-left">Roles</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-right">Created</th>
              </tr>
            </thead>
            <tbody>
              {users.map((user) => (
                <tr
                  key={user.id}
                  className={`border-t border-slate-100 cursor-pointer ${
                    selectedId === user.id ? "bg-slate-50" : "hover:bg-slate-50/60"
                  }`}
                  onClick={() => setSelectedId(user.id)}
                >
                  <td className="px-4 py-3 text-sm">{user.username}</td>
                  <td className="px-4 py-3 text-sm text-slate-500">{user.email ?? "-"}</td>
                  <td className="px-4 py-3 text-sm text-slate-500">
                    {user.roles?.length ? user.roles.join(", ") : "-"}
                  </td>
                  <td className="px-4 py-3 text-sm">
                    <span
                      className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${
                        user.isActive
                          ? "bg-emerald-50 text-emerald-700 border border-emerald-200"
                          : "bg-slate-100 text-slate-600 border border-slate-200"
                      }`}
                    >
                      {user.isActive ? "ACTIVE" : "INACTIVE"}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right text-xs text-slate-500">
                    {new Date(user.createdAt).toLocaleDateString()}
                  </td>
                </tr>
              ))}
            </tbody>
          </DataTable>
          {users.length === 0 && !loading ? (
            <div className="mt-6">
              <EmptyState title="No users found." description="Create a user to get started." />
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
