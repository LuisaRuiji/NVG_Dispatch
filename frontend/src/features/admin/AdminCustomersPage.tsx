import { useEffect, useMemo, useState } from "react";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import LoadingSkeleton from "@/components/LoadingSkeleton";
import EmptyState from "@/components/EmptyState";
import { Button } from "@/components/ui/button";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";

type CustomerListItem = {
  id: string;
  name: string;
  contactPerson?: string | null;
  contactEmail?: string | null;
  phone?: string | null;
  createdAt: string;
};

export default function AdminCustomersPage() {
  const { toasts, show } = useToast();
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [userOpen, setUserOpen] = useState(false);
  const [viewOpen, setViewOpen] = useState(false);
  const [selectedCustomer, setSelectedCustomer] = useState<CustomerListItem | null>(null);

  const [name, setName] = useState("");
  const [contactPerson, setContactPerson] = useState("");
  const [contactEmail, setContactEmail] = useState("");
  const [phone, setPhone] = useState("");

  const [userEmail, setUserEmail] = useState("");
  const [userPassword, setUserPassword] = useState("");

  const sortedCustomers = useMemo(
    () =>
      [...customers].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()),
    [customers]
  );

  const loadCustomers = async () => {
    try {
      setLoading(true);
      const data = await api<CustomerListItem[]>("/api/admin/customers", { method: "GET" });
      setCustomers(data ?? []);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load customers.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadCustomers();
  }, []);

  const resetCreateForm = () => {
    setName("");
    setContactPerson("");
    setContactEmail("");
    setPhone("");
  };

  const handleCreateCustomer = async () => {
    if (!name.trim()) {
      show("Company name is required.", "error");
      return;
    }
    try {
      setLoading(true);
      await api("/api/admin/customers", {
        method: "POST",
        body: JSON.stringify({
          name: name.trim(),
          contactPerson: contactPerson.trim() || null,
          contactEmail: contactEmail.trim() || null,
          phone: phone.trim() || null
        })
      });
      show("Customer created.", "success");
      setCreateOpen(false);
      resetCreateForm();
      await loadCustomers();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to create customer.", "error");
    } finally {
      setLoading(false);
    }
  };

  const handleCreateUser = async () => {
    if (!selectedCustomer) return;
    if (!userEmail.trim() || !userPassword.trim()) {
      show("Email and temporary password are required.", "error");
      return;
    }
    try {
      setLoading(true);
      await api(`/api/admin/customers/${selectedCustomer.id}/users`, {
        method: "POST",
        body: JSON.stringify({
          email: userEmail.trim(),
          password: userPassword.trim()
        })
      });
      show("Customer portal account created.", "success");
      setUserOpen(false);
      setUserEmail("");
      setUserPassword("");
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to create customer user.", "error");
    } finally {
      setLoading(false);
    }
  };

  const openCreateUser = (customer: CustomerListItem) => {
    setSelectedCustomer(customer);
    setUserOpen(true);
  };

  const openView = (customer: CustomerListItem) => {
    setSelectedCustomer(customer);
    setViewOpen(true);
  };

  return (
    <div className="space-y-6">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Customer Management"
        description="Create customers and provision portal accounts."
        actions={
          <Button onClick={() => setCreateOpen(true)} disabled={loading}>
            Create Customer
          </Button>
        }
      />

      {loading && customers.length === 0 ? (
        <LoadingSkeleton rows={6} />
      ) : sortedCustomers.length === 0 ? (
        <EmptyState title="No customers yet" description="Create a customer to begin provisioning portal accounts." />
      ) : (
        <div className="surface-card overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left">Customer Name</th>
                  <th className="px-4 py-3 text-left">Contact Person</th>
                  <th className="px-4 py-3 text-left">Email</th>
                  <th className="px-4 py-3 text-left">Phone</th>
                  <th className="px-4 py-3 text-left">Created At</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/60">
                {sortedCustomers.map((customer) => (
                  <tr key={customer.id} className="hover:bg-muted/10">
                    <td className="px-4 py-3 font-semibold">{customer.name}</td>
                    <td className="px-4 py-3">{customer.contactPerson ?? "-"}</td>
                    <td className="px-4 py-3">{customer.contactEmail ?? "-"}</td>
                    <td className="px-4 py-3">{customer.phone ?? "-"}</td>
                    <td className="px-4 py-3">
                      {customer.createdAt ? new Date(customer.createdAt).toLocaleDateString() : "-"}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex flex-wrap justify-end gap-2">
                        <Button size="sm" variant="outline" onClick={() => openView(customer)}>
                          View
                        </Button>
                        <Button size="sm" onClick={() => openCreateUser(customer)}>
                          Create Portal User
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {createOpen ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px]"
          onClick={() => setCreateOpen(false)}
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,520px)] rounded-2xl border border-border bg-white p-6 shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.25em] text-muted-foreground">Create Customer</p>
                <h2 className="mt-2 text-lg font-semibold text-foreground">Company details</h2>
              </div>
              <Button variant="outline" size="sm" onClick={() => setCreateOpen(false)}>
                Close
              </Button>
            </div>

            <div className="mt-5 space-y-4 text-sm">
              <div>
                <label className="text-xs uppercase text-muted-foreground">Company Name</label>
                <input
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="mt-1 h-10 w-full rounded-lg border border-border bg-white px-3"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-muted-foreground">Contact Person</label>
                <input
                  value={contactPerson}
                  onChange={(e) => setContactPerson(e.target.value)}
                  className="mt-1 h-10 w-full rounded-lg border border-border bg-white px-3"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-muted-foreground">Contact Email</label>
                <input
                  type="email"
                  value={contactEmail}
                  onChange={(e) => setContactEmail(e.target.value)}
                  className="mt-1 h-10 w-full rounded-lg border border-border bg-white px-3"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-muted-foreground">Phone</label>
                <input
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  className="mt-1 h-10 w-full rounded-lg border border-border bg-white px-3"
                />
              </div>
            </div>

            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setCreateOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreateCustomer} disabled={loading}>
                Create Customer
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {userOpen && selectedCustomer ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px]"
          onClick={() => setUserOpen(false)}
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,520px)] rounded-2xl border border-border bg-white p-6 shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.25em] text-muted-foreground">Create Portal User</p>
                <h2 className="mt-2 text-lg font-semibold text-foreground">
                  {selectedCustomer.name}
                </h2>
              </div>
              <Button variant="outline" size="sm" onClick={() => setUserOpen(false)}>
                Close
              </Button>
            </div>

            <div className="mt-5 space-y-4 text-sm">
              <div>
                <label className="text-xs uppercase text-muted-foreground">Email</label>
                <input
                  type="email"
                  value={userEmail}
                  onChange={(e) => setUserEmail(e.target.value)}
                  className="mt-1 h-10 w-full rounded-lg border border-border bg-white px-3"
                />
              </div>
              <div>
                <label className="text-xs uppercase text-muted-foreground">Temporary Password</label>
                <input
                  type="password"
                  value={userPassword}
                  onChange={(e) => setUserPassword(e.target.value)}
                  className="mt-1 h-10 w-full rounded-lg border border-border bg-white px-3"
                />
              </div>
            </div>

            <div className="mt-6 flex justify-end gap-3">
              <Button variant="outline" onClick={() => setUserOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreateUser} disabled={loading}>
                Create User
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {viewOpen && selectedCustomer ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px]"
          onClick={() => setViewOpen(false)}
          role="presentation"
        >
          <div
            role="dialog"
            aria-modal="true"
            className="w-[min(92vw,520px)] rounded-2xl border border-border bg-white p-6 shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs uppercase tracking-[0.25em] text-muted-foreground">Customer Details</p>
                <h2 className="mt-2 text-lg font-semibold text-foreground">{selectedCustomer.name}</h2>
              </div>
              <Button variant="outline" size="sm" onClick={() => setViewOpen(false)}>
                Close
              </Button>
            </div>
            <div className="mt-5 space-y-3 text-sm text-muted-foreground">
              <p>
                <span className="text-foreground font-semibold">Contact Person:</span>{" "}
                {selectedCustomer.contactPerson ?? "-"}
              </p>
              <p>
                <span className="text-foreground font-semibold">Email:</span>{" "}
                {selectedCustomer.contactEmail ?? "-"}
              </p>
              <p>
                <span className="text-foreground font-semibold">Phone:</span>{" "}
                {selectedCustomer.phone ?? "-"}
              </p>
              <p>
                <span className="text-foreground font-semibold">Created:</span>{" "}
                {selectedCustomer.createdAt
                  ? new Date(selectedCustomer.createdAt).toLocaleString()
                  : "-"}
              </p>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
