import React, { useEffect, useState } from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, Routes, Route, Navigate, useLocation } from "react-router-dom";
import "leaflet/dist/leaflet.css";
import "./index.css";
import LandingPage from "@/features/landing/LandingPage";
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
import DispatchReportsPage from "@/features/reports/DispatchReportsPage";
import IntegrityPage from "@/features/admin/IntegrityPage";
import UsersPage from "@/features/admin/UsersPage";
import AdminCustomersPage from "@/features/admin/AdminCustomersPage";
import ModuleSettingsPage from "@/features/admin/ModuleSettingsPage";
import AuditLogsPage from "@/features/admin/AuditLogsPage";
import AuthEventsPage from "@/features/admin/AuthEventsPage";
import PurchaseOrdersPage from "@/features/purchase-orders/PurchaseOrdersPage";
import PurchaseOrderDetailPage from "@/features/purchase-orders/PurchaseOrderDetailPage";
import DispatchBoardPage from "@/features/dispatch/DispatchBoardPage";
import DispatchMapPage from "@/features/dispatch/DispatchMapPage";
import DispatchRequestsPage from "@/features/dispatch/DispatchRequestsPage";
import DispatchDocumentsPage from "@/features/dispatch/DispatchDocumentsPage";
import DispatchTripsPage from "@/features/dispatch/DispatchTripsPage";
import MyTripsPage from "@/features/dispatch/MyTripsPage";
import MyTripDetailPage from "@/features/dispatch/MyTripDetailPage";
import TripDetailPage from "@/features/dispatch/TripDetailPage";
import PortalDashboardPage from "@/features/portal/PortalDashboardPage";
import PortalRequestsPage from "@/features/portal/PortalRequestsPage";
import PortalRequestNewPage from "@/features/portal/PortalRequestNewPage";
import PortalRequestDetailPage from "@/features/portal/PortalRequestDetailPage";
import PortalShipmentsPage from "@/features/portal/PortalShipmentsPage";
import PortalShipmentDetailPage from "@/features/portal/PortalShipmentDetailPage";
import { setUnauthorizedHandler } from "@/lib/api";
import {
  clearInvalidSession,
  getDefaultRoute,
  getMe,
  loadMeIfTokenExists,
  refreshSession
} from "@/features/auth/authStore";
import ChangePasswordPage from "@/features/auth/ChangePasswordPage";
import RoleGate, { getSafeReturnRoute } from "@/components/RoleGate";
import { initTheme } from "@/lib/theme";

setUnauthorizedHandler(async () => {
  if (await refreshSession()) {
    return true;
  }

  clearInvalidSession();
  if (window.location.pathname !== "/login") {
    window.location.assign("/login");
  }
  return false;
});

initTheme();

function MustChangePasswordGuard({ children }: { children: React.ReactNode }) {
  const me = getMe();
  const location = useLocation();

  if (me?.mustChangePassword && location.pathname !== "/change-password") {
    return <Navigate to="/change-password" replace />;
  }

  if (!me?.mustChangePassword && location.pathname === "/change-password") {
    return <Navigate to={getDefaultRoute(me)} replace />;
  }

  return <>{children}</>;
}

function UnknownRouteFallback() {
  const me = getMe();
  const location = useLocation();

  return <Navigate to={me ? getSafeReturnRoute(location.pathname) : "/login"} replace />;
}

function App() {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    loadMeIfTokenExists().finally(() => setReady(true));
  }, []);

  if (!ready) {
    return <div className="min-h-screen bg-background" />;
  }

  return (
    <BrowserRouter>
      <MustChangePasswordGuard>
        <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/login" element={<LandingPage />} />
        <Route path="/change-password" element={<ChangePasswordPage />} />
        <Route element={<AppLayout />}>
          <Route
            path="/dispatch"
            element={
              <RoleGate roles={["Manager", "Dispatcher", "HeadOfFinance", "CEO"]}>
                <Navigate to="/dispatch/board" replace />
              </RoleGate>
            }
          />
            <Route
              path="/dashboard"
              element={
                <RoleGate roles={["SuperAdmin", "Admin", "Dispatcher", "Manager", "HeadOfFinance", "Driver", "Customer", "InventoryOfficer", "CEO"]}>
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
            path="/dispatch/board"
            element={
              <RoleGate roles={["Manager", "Dispatcher", "CEO"]}>
                <DispatchBoardPage />
              </RoleGate>
            }
          />
          <Route
            path="/dispatch/map"
            element={
              <RoleGate roles={["Manager", "Dispatcher"]}>
                <DispatchMapPage />
              </RoleGate>
            }
          />
          <Route
            path="/dispatch/requests"
            element={
              <RoleGate roles={["Manager", "Dispatcher"]}>
                <DispatchRequestsPage />
              </RoleGate>
            }
          />
          <Route
            path="/dispatch/trips"
            element={
              <RoleGate roles={["Manager", "Dispatcher", "CEO"]}>
                <DispatchTripsPage />
              </RoleGate>
            }
          />
          <Route
            path="/dispatch/documents"
            element={
              <RoleGate roles={["Manager", "Dispatcher", "Admin", "SuperAdmin", "HeadOfFinance"]}>
                <DispatchDocumentsPage />
              </RoleGate>
            }
          />
          <Route
            path="/dispatch/my-trips"
            element={
              <RoleGate roles={["Driver"]}>
                <MyTripsPage />
              </RoleGate>
            }
          />
          <Route
            path="/dispatch/my-trips/:id"
            element={
              <RoleGate roles={["Driver"]}>
                <MyTripDetailPage />
              </RoleGate>
            }
          />
          <Route
            path="/portal/dashboard"
            element={
              <RoleGate roles={["Customer"]}>
                <PortalDashboardPage />
              </RoleGate>
            }
          />
          <Route
            path="/portal/requests"
            element={
              <RoleGate roles={["Customer"]}>
                <PortalRequestsPage />
              </RoleGate>
            }
          />
          <Route
            path="/portal/requests/new"
            element={
              <RoleGate roles={["Customer"]}>
                <PortalRequestNewPage />
              </RoleGate>
            }
          />
          <Route
            path="/portal/requests/:id"
            element={
              <RoleGate roles={["Customer"]}>
                <PortalRequestDetailPage />
              </RoleGate>
            }
          />
          <Route
            path="/portal/shipments"
            element={
              <RoleGate roles={["Customer"]}>
                <PortalShipmentsPage />
              </RoleGate>
            }
          />
          <Route
            path="/portal/shipments/:id"
            element={
              <RoleGate roles={["Customer"]}>
                <PortalShipmentDetailPage />
              </RoleGate>
            }
          />
          <Route
            path="/dispatch/trips/:id"
            element={
              <RoleGate roles={["Manager", "Dispatcher", "HeadOfFinance", "CEO"]}>
                <TripDetailPage />
              </RoleGate>
            }
          />
          <Route
            path="/reports"
            element={
              <RoleGate roles={["Manager", "InventoryOfficer", "HeadOfFinance", "CEO", "Admin", "SuperAdmin"]}>
                <ReportsPage />
              </RoleGate>
            }
          />
          <Route
            path="/reports/dispatch"
            element={
              <RoleGate roles={["Dispatcher", "Manager", "HeadOfFinance", "CEO", "Admin", "SuperAdmin"]}>
                <DispatchReportsPage />
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
            path="/admin/customers"
            element={
              <RoleGate roles={["Manager", "Dispatcher", "Admin", "SuperAdmin"]}>
                <AdminCustomersPage />
              </RoleGate>
            }
          />
          <Route
            path="/admin/modules"
            element={
              <RoleGate roles={["SuperAdmin"]}>
                <ModuleSettingsPage />
              </RoleGate>
            }
          />
          <Route
            path="/admin/audit-logs"
            element={
              <RoleGate roles={["SuperAdmin", "Admin", "Manager", "Dispatcher", "HeadOfFinance", "InventoryOfficer", "Driver"]}>
                <AuditLogsPage />
              </RoleGate>
            }
          />
          <Route
            path="/admin/auth-events"
            element={
              <RoleGate roles={["Admin", "SuperAdmin"]}>
                <AuthEventsPage />
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
        </Route>
        <Route path="*" element={<UnknownRouteFallback />} />
        </Routes>
      </MustChangePasswordGuard>
    </BrowserRouter>
  );
}

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
