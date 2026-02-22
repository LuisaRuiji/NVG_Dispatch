import React, { useEffect, useState } from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import "./index.css";
import LoginPage from "@/features/auth/LoginPage";
import AppLayout from "@/components/layout/AppLayout";
import DashboardPage from "@/features/dashboard/DashboardPage";
import RequestsPage from "@/features/requests/RequestsPage";
import RequestDetailPage from "@/features/requests/RequestDetailPage";
import RequestIoReviewPage from "@/features/requests/RequestIoReviewPage";
import NewMaintenanceIssuePage from "@/features/requests/NewMaintenanceIssuePage";
import NewBorrowRequestPage from "@/features/requests/NewBorrowRequestPage";
import LoanListPage from "@/features/loans/LoanListPage";
import LoanDetailPage from "@/features/loans/LoanDetailPage";
import {
  IoQueuePage,
  IssueQueuePage,
  ManagerQueuePage,
  MyRequestsPage
} from "@/features/requests/RequestQueuePage";
import InventoryPage from "@/features/inventory/InventoryPage";
import ReportsPage from "@/features/reports/ReportsPage";
import IntegrityPage from "@/features/admin/IntegrityPage";
import UsersPage from "@/features/admin/UsersPage";
import PurchaseOrdersPage from "@/features/purchase-orders/PurchaseOrdersPage";
import PurchaseOrderDetailPage from "@/features/purchase-orders/PurchaseOrderDetailPage";
import { setUnauthorizedHandler } from "@/lib/api";
import { getDefaultRoute, getMe, loadMeIfTokenExists, logout } from "@/features/auth/authStore";
import RoleGate from "@/components/RoleGate";

setUnauthorizedHandler(() => {
  logout();
  window.location.assign("/login");
});

function App() {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    loadMeIfTokenExists().finally(() => setReady(true));
  }, []);

  if (!ready) {
    return <div className="min-h-screen bg-background" />;
  }

  const defaultRoute = getDefaultRoute(getMe());

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<AppLayout />}>
          <Route
            path="/dashboard"
            element={
              <RoleGate roles={["InventoryOfficer", "Manager", "HeadOfFinance", "CEO", "Driver"]}>
                <DashboardPage />
              </RoleGate>
            }
          />
          <Route path="/requests" element={<RequestsPage />} />
          <Route
            path="/queue/io"
            element={
              <RoleGate roles={["InventoryOfficer"]}>
                <IoQueuePage />
              </RoleGate>
            }
          />
          <Route
            path="/queue/issue"
            element={
              <RoleGate roles={["InventoryOfficer"]}>
                <IssueQueuePage />
              </RoleGate>
            }
          />
          <Route
            path="/queue/manager"
            element={
              <RoleGate roles={["Manager"]}>
                <ManagerQueuePage />
              </RoleGate>
            }
          />
          <Route
            path="/my/requests"
            element={
              <RoleGate roles={["Driver"]}>
                <MyRequestsPage />
              </RoleGate>
            }
          />
          <Route
            path="/inventory"
            element={
              <RoleGate roles={["InventoryOfficer", "Manager"]}>
                <InventoryPage />
              </RoleGate>
            }
          />
          <Route
            path="/purchase-orders"
            element={
              <RoleGate roles={["InventoryOfficer", "Manager", "HeadOfFinance", "CEO"]}>
                <PurchaseOrdersPage />
              </RoleGate>
            }
          />
          <Route
            path="/purchase-orders/:id"
            element={
              <RoleGate roles={["InventoryOfficer", "Manager", "HeadOfFinance", "CEO"]}>
                <PurchaseOrderDetailPage />
              </RoleGate>
            }
          />
          <Route
            path="/reports"
            element={
              <RoleGate roles={["InventoryOfficer", "Manager", "HeadOfFinance", "CEO", "Admin", "SuperAdmin"]}>
                <ReportsPage />
              </RoleGate>
            }
          />
          <Route
            path="/admin/integrity"
            element={
              <RoleGate roles={["Manager", "HeadOfFinance", "CEO", "Admin", "SuperAdmin"]}>
                <IntegrityPage />
              </RoleGate>
            }
          />
          <Route
            path="/admin/users"
            element={
              <RoleGate roles={["Admin", "SuperAdmin"]}>
                <UsersPage />
              </RoleGate>
            }
          />
          <Route
            path="/requests/new/maintenance-issue"
            element={
              <RoleGate roles={["Driver"]}>
                <NewMaintenanceIssuePage />
              </RoleGate>
            }
          />
          <Route
            path="/requests/new/borrow"
            element={
              <RoleGate roles={["Driver"]}>
                <NewBorrowRequestPage />
              </RoleGate>
            }
          />
          <Route path="/requests/:id" element={<RequestDetailPage />} />
          <Route
            path="/requests/:id/io-review"
            element={
              <RoleGate roles={["InventoryOfficer"]}>
                <RequestIoReviewPage />
              </RoleGate>
            }
          />
          <Route
            path="/loans"
            element={
              <RoleGate roles={["InventoryOfficer", "Manager"]}>
                <LoanListPage />
              </RoleGate>
            }
          />
          <Route
            path="/loans/:id"
            element={
              <RoleGate roles={["InventoryOfficer", "Manager"]}>
                <LoanDetailPage />
              </RoleGate>
            }
          />
          <Route path="/" element={<Navigate to={defaultRoute} replace />} />
        </Route>
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
