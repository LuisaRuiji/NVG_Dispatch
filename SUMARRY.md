# VAIA system summary

> Snapshot of the repository as inspected on 29 July 2026. This describes the
> code currently present, rather than only the intended design. The filename is
> intentionally `SUMARRY.md`, as requested.

## What this system is

VAIA is a single-company trucking operations system centred on a **Trip**: one
container movement from pickup to drop-off. It combines dispatch operations,
customer shipment requests, driver execution, document control, fleet tracking,
operational reporting, and a supporting inventory module.

The application is a modular monolith: one ASP.NET Core API and SQL Server
database, with a React/Vite frontend. SignalR supplies live operational updates.
The same responsive frontend is configured for an Android Capacitor wrapper.

## Technology and project layout

| Area | Current implementation |
| --- | --- |
| Backend | ASP.NET Core on .NET 8, Entity Framework Core, SQL Server, Swagger |
| Frontend | React 18, TypeScript, Vite, Tailwind CSS, React Router, Leaflet, Recharts |
| Real time | SignalR hubs at `/hubs/dispatch` and `/hubs/dispatch-location` |
| Security | JWT access tokens, refresh tokens, CSRF marker, role/policy authorization, MFA/TOTP, audit/auth-event logging, encrypted sensitive fields |
| Mobile | Capacitor Android project with Camera, App, and Push Notifications plugins |
| Deployment | Docker Compose for API, SQL Server, frontend, and a separate landing site |

Important source areas:

- `Modules/Dispatching` — trips, planning, documents, recommendations, tracking.
- `Modules/ShipmentRequests` — customer portal request workflow and ATW handling.
- `Controllers` and `Domain` — inventory, users, reporting, procurement, auth, and shared rules.
- `frontend/src/features` — role-aware UI screens.
- `Data` — EF Core context, migrations, seeders, and protection migrations.
- `NVGInventory.Tests` — unit and SQL Server integration tests.

Some folders, namespaces, container names, and the solution name still use the
legacy **NVGInventory** name. `InventoryERP.Api` is a separate legacy project
excluded from the main `NVGInventory.csproj`; the running application is the
root project.

## Implemented operational features

| Module | What is implemented now |
| --- | --- |
| Dispatch lifecycle | Create, edit, list, filter, dispatch, and monitor trips. Controlled statuses include Draft, Ready for Dispatch, Dispatched, pickup/drop-off milestones, Delivered, Closed, On Hold, Failed Attempt, and Cancelled. Status changes create history and broadcast live events. |
| Dispatch planning | Planning board, resource availability, decision support, readiness validation, manual assignment of drivers/trucks/trailers, and overlap/conflict checks. Trips can be grouped and viewed by assignment day. |
| Driver work | “My Trips” and trip-detail screens allow assigned drivers to progress a trip, view routes, record locations, start/stop tracking sessions, and upload allowed documents. |
| Fleet and maps | Trucks, trailers, drivers, customers, trip stops, coordinates, live location updates, dispatcher live map, owner read-only map, and driver route map. Address geocoding uses OpenStreetMap Nominatim. |
| Documents | ATW, EIR, Gate Pass, DR, POD, document versions, verification/rejection, secure document links, completeness checks, and system-generated waybills. Shipment-request documents are stored locally under `App_Data/dispatch-documents`. |
| Shipment requests / customer portal | Customer-specific dashboard, create/edit/submit request, optional ATW upload, request review/approval/rejection/revision, conversion of an approved request to a draft trip, shipment tracking, timeline, and document viewing. Admin/dispatch can manage customers and provision customer users. |
| Dispatch recommendations | CSP feasibility checks and TOPSIS-ranked trip-chaining suggestions; pending, accept/confirm, ignore/dismiss, and history endpoints; recommendation notifications through SignalR; optimization-weight settings; audit records for decisions. Suggestions are generated asynchronously after a Delivered **or Cancelled** trip, so they cannot roll back the lifecycle update. |
| Dashboards and reports | Role-specific dashboards for dispatch, manager, driver, customer, finance, CEO, and inventory. Dispatch reports cover trip summaries, driver performance, delivery-time routes, document processing, financial summaries, and recommendations, with CSV exports. Inventory reports include stock, valuation, loans, supplier spend, asset maintenance/consumption, adjustments, audit, and integrity checks. |
| Inventory support module | Inventory items and kits/components, assets, suppliers, stock ledger, borrow/return workflow, maintenance issues, approvals, purchase orders/receipts, inventory adjustments, queues, reports, and integrity checks. |
| Administration | User/role management, customer administration, module maintenance switches, audit-log views, auth-event views, data-integrity view, demo/performance seeders, and guarded database-reset utilities. |

## Security and data controls already present

- Roles include SuperAdmin, Admin, Manager, Dispatcher, HeadOfFinance, CEO,
  Driver, Customer, InventoryOfficer, and Owner. The UI and API use role gates
  and authorization policies.
- Login, MFA setup/verification/disable, step-up authentication, password
  change, access-token refresh, and logout endpoints are implemented.
- Passwords use BCrypt and policy checks. Sensitive dispatch financial data and
  personally identifiable fields have encryption migrations/services.
- Audit logs and trip-status history provide operational traceability. A
  correlation ID is added to requests.
- In-memory caching is registered for selected dashboard and dispatch reads.

## Present but only partially production-ready

| Area | Current boundary |
| --- | --- |
| Push notifications | Native clients can request permission and register a device token, and the API saves tokens. The backend `PushNotificationService` currently only logs notification messages; it has no FCM/APNs delivery provider. |
| Mobile release | Android Capacitor sources and camera/upload integration are present. There is no iOS project in this repository, and a packaged/released mobile app is not evidenced here. |
| Optimization inputs | CSP/TOPSIS workflow is implemented, but several scoring inputs are provisional: Haversine distance with a fixed 50 km/h speed, 15-minute cleaning, and default cargo-compatibility/asset-utilization scores. It is decision support, never automatic dispatch. |
| Geocoding | Live-map and planning geocoding call Nominatim and keep a process-local cache. It depends on network availability and the public service’s use policy. |
| ATW extraction | Azure Document Intelligence integration exists and can scan uploaded ATWs when endpoint/key configuration is supplied. It starts with the generic `prebuilt-layout` model and uses extracted values for review; it is not a trained, production-specific ATW model. |
| Document storage | Shipment-request uploads are validated (PDF/JPG/PNG, maximum 10 MB) and held on the API host’s local disk. They are not yet object-storage backed. |
| Deployment evolution | Docker Compose is available and the API applies EF migrations at startup. The system is still single-tenant: entities and queries do not carry a tenant scope. |

## Not implemented as a current system capability

- Billing/invoice generation or payment processing.
- Multi-tenant SaaS administration, tenant-scoped data, or database-per-tenant routing.
- A delivery-time prediction or anomaly-detection ML model.
- A real push-delivery provider (FCM/APNs) or verified background notification delivery.
- A native iOS application.

## Main user journeys

1. A customer creates and submits a shipment request, optionally attaching an
   ATW.
2. Dispatch/manager reviews it, requests changes/rejects/approves it, then
   converts it into a draft trip.
3. Dispatcher plans and assigns the driver, truck, and optional trailer;
   readiness validation checks stops, schedule, container and document rules.
4. The driver executes controlled trip transitions, sends location updates, and
   uploads site documents. Dispatch verifies documents and can generate the
   waybill.
5. Delivery updates the portal, dashboards, history, reporting, and live
   clients. Advisory trip-chaining suggestions are scored in the background for
   the dispatcher to accept or ignore.
6. Completed work remains available through trip history, document records,
   audit logs, dashboards, and CSV reports.

## Useful entry points

- API composition and dependency registration: `Program.cs`
- API documentation at runtime: Swagger is configured by the root API project.
- Frontend routes: `frontend/src/main.tsx`
- Dispatch API: `Modules/Dispatching/Controllers`
- Shipment/customer API: `Modules/ShipmentRequests/ShipmentRequestsController.cs`
- Database schema history: `Data/Migrations`
- Container setup: `docker-compose.yml` and `.env.example`

## Local validation available

The repository includes SQL Server-backed integration tests. Set
`TEST_SQLSERVER_CONNECTION_STRING` to a database whose name ends in `_Tests`,
then run `dotnet test`. The test fixture refuses unsafe test-database names and
recreates the test database for a test run.
