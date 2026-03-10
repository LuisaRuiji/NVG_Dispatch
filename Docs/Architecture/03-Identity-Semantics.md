# Identity Semantics (Current + Locked Intent)

Date: 2026-03-11  
Scope: clarify current identity meaning before deeper model changes.

## 1. What is a `User` in this system?

`User` is an account identity used for:

- Authentication (username/password, JWT subject)
- Authorization (role membership via `UserRole`)
- Optional portal-customer scope linkage (`User.CustomerId`)

`User` is **not** a business shipment/dispatch aggregate.

## 2. What is a `Customer` in this system?

`Customer` is a business entity (company/account owner for shipments and trips).

Current business ownership usage:

- `ShipmentRequest.CustomerId`
- `Trip.CustomerId`

## 3. What does `User.CustomerId` mean?

`User.CustomerId` currently means **portal account scope linkage**:

- If set, the user is scoped to one customer in portal endpoints.
- If missing, portal customer endpoints are denied.

This field should be treated as an identity-access linkage, not generic domain ownership logic.

## 4. Internal vs external account roles

Internal operator roles (operations/administration):  
`InventoryOfficer`, `Dispatcher`, `Manager`, `HeadOfFinance`, `CEO`, `Driver`

External portal role:  
`Customer`

Current behavior allows role assignment on a single user account model; no separate employee/customer account table exists yet.

## 5. Boundary rules going forward

1. Controllers must not query `Users` directly for portal customer scope checks.
2. Portal customer scope resolution must go through `IPortalCustomerAccessService`.
3. Dispatch and ShipmentRequests continue to use `CustomerId` as business ownership key.
4. `User.CustomerId` remains an implementation detail of portal access until a dedicated mapping model is introduced.
5. Any schema split (for example `UserCustomerScope`) is deferred and must be behavior-preserving.

## Current status

Portal scope lookup is centralized in:

- `IPortalCustomerAccessService.GetRequiredPortalCustomerIdAsync(...)`

So controller-level identity-to-customer lookup duplication is removed.
