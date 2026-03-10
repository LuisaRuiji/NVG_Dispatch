# Safe Refactor Plan (Phased Execution)

Date: 2026-03-10
Purpose: Move toward modular boundaries from `instructions.md` with low operational risk.

## Refactor Rules

1. No behavior changes mixed with namespace/folder moves in one commit.
2. Keep API routes and DTO contracts stable while internals are reorganized.
3. Preserve dispatch invariants: status guards, conflict checks, document close rules, audit writes.
4. Preserve inventory invariants: StockLedger-only quantity mutations.
5. Run targeted integration tests after every phase.
6. Use adapter wrappers before hard moves when cross-context callers still exist.
7. No new direct identity-to-dispatch entity navigations are allowed.
8. No new cross-context EF navigation may be added unless explicitly approved.
9. Module callers should depend on seam interfaces, not concrete cross-module services.

## Phase 0 - Baseline Freeze (No Structural Changes)

Actions:

- Keep current runtime behavior as reference baseline.
- Use these tests as mandatory guards:
  - `NVGInventory.Tests/DispatchTripStatusTests.cs`
  - `NVGInventory.Tests/DispatchTripSimulationTests.cs`
  - `NVGInventory.Tests/DispatchTripDocumentVersionsTests.cs`
  - `NVGInventory.Tests/DispatchTripDocumentLinkTests.cs`
  - `NVGInventory.Tests/DispatchMyTripsQueryTests.cs`
  - `NVGInventory.Tests/ShipmentRequestTests.cs`
  - `NVGInventory.Tests/StockLedgerServiceTests.cs`

Validation:

- `dotnet test NVGInventory.Tests/NVGInventory.Tests.csproj`

## Phase 1 - Boundary Preparation (No File Moves Yet)

Goal: create clean seams so later moves are mechanical.

### Phase 1 Interface List (Concrete)

Introduce only these seams first:

1. `ITripLifecycleService`
2. `IDispatchDocumentWorkflowService`
3. `IShipmentRequestTripCreationService`
4. `IAuditService` (existing seam; treat as official audit write boundary)

Method scope by seam:

- `ITripLifecycleService`
  - `CreateDraftAsync`
  - `UpdateTripAsync`
  - `DispatchAsync`
  - `ChangeStatusAsync`
  - `CorrectStatusAsync`
- `IDispatchDocumentWorkflowService`
  - `UploadDocumentAsync`
  - `VerifyDocumentAsync`
  - `RejectDocumentAsync`
- `IShipmentRequestTripCreationService`
  - `CreateDraftFromShipmentRequestAsync` (single-purpose trip creation for approved requests)
- `IAuditService`
  - `AddEntry` (already exists; no concrete `AuditService` dependencies in callers)

### Caller Switches Required In Phase 1

Switch these callers in the same phase:

1. `Modules/Dispatching/Controllers/DispatchTripsController.cs`
   - from `DispatchTripService` to:
   - `ITripLifecycleService`
   - `IDispatchDocumentWorkflowService`
2. `Modules/ShipmentRequests/Services/ShipmentRequestService.cs`
   - from `DispatchTripService` to:
   - `IShipmentRequestTripCreationService`
3. `Program.cs`
   - register interfaces to current implementations/adapters without behavior change.

### Forbidden Dependencies After Phase 1

1. `ShipmentRequests` must not reference concrete `DispatchTripService`.
2. Document upload/verify/reject logic must not expand further inside `DispatchTripService`.
3. Controllers must not depend on lower-level concrete services across module boundaries.
4. No new identity-to-dispatch navigation properties may be introduced.
5. No new cross-context EF navigation properties without explicit approval.

Expected outcome:

- Existing code still compiles and runs unchanged.
- Callers depend on abstractions, reducing move risk.
- We remove the current highest-risk concrete coupling path before structural moves.

## Phase 2 - DispatchOperations vs DocumentManagement Split

Goal: separate trip lifecycle concerns from document workflow concerns.

Safe order:

1. Extract document-focused logic from `DispatchTripService` into dedicated services:
   - upload version
   - verify/reject
   - active-version lookup
2. Keep `DispatchTripService` delegating to new document services.
3. Keep existing controllers and routes unchanged (`/api/dispatch/trips/...`).

Target folders:

- `Modules/DispatchOperations/*` (trip lifecycle, assignments, conflicts)
- `Modules/DocumentManagement/*` (trip documents, verification, versioning)

Validation:

- Dispatch status and document test suites pass.
- No API contract changes in controllers.

## Phase 3 - ShipmentRequests Hardening

Goal: keep ShipmentRequests independent except intentional integration seam.

Actions:

- Replace direct concrete dependency on `DispatchTripService` with a narrow trip-creation interface.
- Keep convert-to-trip behavior identical.
- Keep request ownership and portal restrictions unchanged.

Validation:

- `ShipmentRequestTests` pass.
- Dispatch create-draft flow still passes simulation tests.

## Phase 4 - EF Configuration Ownership (One DbContext, Modular Config Classes)

Goal: reinforce context ownership early without changing database topology.

Actions:

- Keep one `InventoryDbContext`.
- Move per-context EF configurations into module-owned configuration classes/files.
- Keep table names, column mappings, and relationships unchanged.

Validation:

- Migrations remain compatible.
- No schema drift.
- Existing integration tests pass unchanged.

## Phase 5 - Audit Module Isolation

Goal: isolate audit persistence and audit action constants as their own context.

Actions:

- Move `AuditLog`, `IAuditService`, and `AuditService` into `Modules/Audit`.
- Keep same DI service contract so existing callers do not change behavior.
- Keep `audit_logs` table schema untouched in this phase.

Validation:

- All existing actions still write audit rows.
- Dispatch and shipment approval/rejection flows remain unchanged.

## Phase 6 - Identity Isolation

Goal: isolate identity models/services from dispatch model dependencies.

Key risk:

- `Domain/Entities/User.cs` currently references dispatch `Customer`.

Safe approach:

1. Introduce a neutral customer reference model owned by identity (e.g. `CustomerRefId` only), preserving DB column.
2. Remove direct navigation dependency from identity entity to dispatch entity after adapters exist.
3. Keep customer portal access checks behavior unchanged.

Validation:

- Auth/login/me flows pass.
- Customer-scoped portal flows still enforce ownership.

## Phase 7 - Inventory as Optional Context + AI Scaffolding

Actions:

- Group inventory/request/loan/purchase-order services/controllers under an inventory context boundary.
- Keep dispatch logic free of inventory writes (already enforced).
- Add `Modules/AIAssistance` scaffolding only (no lifecycle writes), behind feature flags.

Validation:

- Stock ledger tests pass.
- Dispatch tests remain green.

## Known Gaps to Track Explicitly

1. Dispatch document types currently implemented: `WAYBILL`, `POD`, `ATW`.
   - New AGENTS target also mentions `EIR` and `Gate Pass`.
   - Add as controlled schema/API extension in a separate change.
2. `ModuleRegistry` does not currently include a distinct `portal` module key.
   - Frontend nav already uses `moduleKey: "portal"`.
   - Add registry support in a dedicated maintenance-controls change.
3. AIAssistance entities/services are not yet present.

## Immediate Next Execution Step

Implement Phase 1 seams exactly as listed, then switch callers in the same PR:

1. Add `ITripLifecycleService` and `IDispatchDocumentWorkflowService`.
2. Add `IShipmentRequestTripCreationService`.
3. Wire controller/service callers to interfaces.
4. Keep behavior and routes unchanged.
5. Run targeted dispatch/shipment tests before any folder or namespace moves.
