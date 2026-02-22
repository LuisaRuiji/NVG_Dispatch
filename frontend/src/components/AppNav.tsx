import { Link, useNavigate } from "react-router-dom";
import { getMe, logout } from "@/features/auth/authStore";

export default function AppNav() {
  const nav = useNavigate();
  const me = getMe();

  if (!me) {
    return null;
  }

  const roles = me.roles ?? [];
  const canAccessReports =
    roles.includes("InventoryOfficer") ||
    roles.includes("Manager") ||
    roles.includes("HeadOfFinance") ||
    roles.includes("CEO");
  const canAccessIntegrity =
    roles.includes("Manager") || roles.includes("HeadOfFinance") || roles.includes("CEO");

  return (
    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
      <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
        {roles.includes("InventoryOfficer") ? <Link to="/queue/io">IO Queue</Link> : null}
        {roles.includes("InventoryOfficer") ? <Link to="/queue/issue">Issue Queue</Link> : null}
        {roles.includes("Manager") ? <Link to="/queue/manager">Manager Queue</Link> : null}
        {roles.includes("Driver") ? <Link to="/my/requests">My Requests</Link> : null}
        {roles.includes("Driver") ? <Link to="/requests/new/maintenance-issue">New Maintenance Issue</Link> : null}
        {roles.includes("Driver") ? <Link to="/requests/new/borrow">New Borrow Request</Link> : null}
        {roles.includes("InventoryOfficer") || roles.includes("Manager") ? (
          <Link to="/inventory">Inventory</Link>
        ) : null}
        {canAccessReports ? <Link to="/reports">Reports</Link> : null}
        {canAccessIntegrity ? <Link to="/admin/integrity">Integrity</Link> : null}
        {roles.includes("InventoryOfficer") ? <Link to="/loans">Loans</Link> : null}
      </div>
      <div>
        <span style={{ marginRight: 12 }}>
          {me.username} ({roles.join(", ")})
        </span>
        <button
          onClick={() => {
            logout();
            nav("/login");
          }}
        >
          Logout
        </button>
      </div>
    </div>
  );
}
