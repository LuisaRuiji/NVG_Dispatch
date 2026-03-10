# Module Inventory (Step 1 Baseline)

Date: 2026-03-10
Scope: Backend (`NVGInventory`) current-state inventory before refactor.

## 1. Current Physical Structure

Current module folders:

- `Modules/Dispatching`
- `Modules/ShipmentRequests`
- `Modules/Customers`

Non-modular backend areas still carrying major domain logic:

- `Domain/Entities`
- `Domain/Services`
- `Controllers`
- `Data/InventoryDbContext.cs`

## 2. Current Context Ownership (As-Is)

### Identity (partially modularized, mostly shared)

Primary files:

- `Domain/Entities/User.cs`
- `Domain/Entities/Role.cs`
- `Domain/Entities/UserRole.cs`
- `Domain/Services/UserService.cs`
- `Controllers/AuthController.cs`
- `Controllers/UsersController.cs`

Notes:

- Identity is shared globally and used by all modules.
- `User` currently has `CustomerId` and navigation to dispatch `Customer`, creating cross-context coupling.

### Dispatch Operations + Document Management (currently combined)

Primary files:

- `Modules/Dispatching/Entities/*`
- `Modules/Dispatching/Enums/*`
- `Modules/Dispatching/Services/DispatchTripService.cs`
- `Modules/Dispatching/Services/DispatchTripQueryService.cs`
- `Modules/Dispatching/Controllers/DispatchTripsController.cs`
- `Modules/Dispatching/Controllers/DispatchMyTripsController.cs`
- `Modules/Dispatching/Controllers/DispatchCustomersController.cs`
- `Modules/Dispatching/Queries/*`

Notes:

- Trip lifecycle, conflict checks, status correction, document uploads/versioning/verification all live in one module.
- Document concerns are not yet separated into a distinct `DocumentManagement` context.

### Shipment Requests (module exists)

Primary files:

- `Modules/ShipmentRequests/Entities/*`
- `Modules/ShipmentRequests/Enums/*`
- `Modules/ShipmentRequests/Services/*`
- `Modules/ShipmentRequests/ShipmentRequestsController.cs`

Notes:

- Module is functional.
- Converts approved requests into dispatch trips through direct dependency on dispatch service.

### Audit (shared service)

Primary files:

- `Domain/Entities/AuditLog.cs`
- `Domain/Services/IAuditService.cs`
- `Domain/Services/AuditService.cs`
- `Controllers/ReportsController.cs` (audit/report access)

Notes:

- Audit behavior is shared across modules but not isolated in a dedicated `Modules/Audit` boundary yet.

### Inventory (large existing optional/supporting context)

Primary files:

- `Domain/Entities/Inventory*`, `Request*`, `Loan*`, `PurchaseOrder*`, `StockLog`, `Supplier`, `Asset`
- `Domain/Services/*` for inventory/request/loan/approval/report/purchase order flows
- `Controllers/InventoryController.cs`, `RequestsController.cs`, `LoansController.cs`, `ApprovalsController.cs`, etc.

Notes:

- Significant domain logic still sits in top-level `Domain` and `Controllers` namespaces.

### AI Assistance (not implemented yet)

No active backend module folder/entities/services for:

- `DocumentExtractionResult`
- `DispatchAnomaly`

## 3. Central Data Boundary Status

`Data/InventoryDbContext.cs` currently configures all contexts in one class:

- Identity
- Inventory/Requests/Loans/Approvals/Purchase Orders
- Audit/AuthEvents/ModuleSettings
- Dispatch
- Shipment Requests

This is functional but not yet modular by configuration ownership.

## 4. Cross-Context Couplings to Address

Current notable couplings:

- `Domain/Entities/User.cs` depends on `Modules.Dispatching.Entities.Customer`.
- `Modules/Dispatching/Services/DispatchTripService.cs` depends on:
  - `Domain.Services.UserService`
  - `Domain.Services.IAuditService`
  - `Domain.Entities` (User/Asset/Audit constants)
- `Modules/ShipmentRequests/Services/ShipmentRequestService.cs` depends on `DispatchTripService`.
- `Modules/Customers/Controllers/CustomersController.cs` writes dispatch customer and customer user assignment.

These are manageable, but must be peeled apart in controlled phases.

## 5. Gap Against Target Architecture (`instructions.md`)

Target contexts:

- Identity
- DispatchOperations
- ShipmentRequests
- DocumentManagement
- Audit
- Inventory (optional)
- AIAssistance (optional)

Current gap summary:

- Dispatch operations and document management are merged.
- Inventory and identity are not fully module-isolated.
- Audit is shared but not module-scoped.
- AI Assistance module is absent.
- Data configurations are centralized rather than module-owned.

## 6. Safety Baseline (Do Not Break)

Before structural moves, keep these stable:

- Dispatch lifecycle and role guards
- Conflict checks and manager override auditing
- Document versioning and close guards
- Shipment request conversion to trip
- Stock ledger invariants in inventory flows

Existing integration tests in `NVGInventory.Tests` are the regression safety net for each refactor phase.

