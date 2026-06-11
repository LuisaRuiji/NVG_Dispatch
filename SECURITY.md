# Security Policy

Last reviewed: 2026-06-12

## Scope

This policy covers the NVG Dispatch / NVGInventory codebase in this repository:

- ASP.NET Core API in `NVGInventory.csproj`
- React/Vite application in `frontend/`
- React/Vite landing app in `landing/`
- SQL Server and Docker Compose deployment files
- Integration tests and local seed scripts

`InventoryERP.Api/` is a small sample API outside `NVGInventory.sln`. Do not treat it as a
production surface unless it is explicitly added to the deployed system and given the same
authentication, authorization, logging, and dependency checks as the main API.

---

## Reporting Vulnerabilities

Do not open public issues for exploitable vulnerabilities, leaked secrets, customer data
exposure, authentication bypasses, or dispatch lifecycle tampering.

Report privately to the project maintainer or repository owner with:

- A short description of the issue and affected module
- Steps to reproduce, proof of concept, or affected endpoint
- Expected impact, including whether dispatch, documents, customers, users, or audit data
  can be modified or read
- Suggested remediation if known

Rotate any exposed secret immediately. Assume exposed JWT signing keys, database passwords,
AWS keys, and document storage credentials are compromised.

---

## Defense in Depth Model

NVG Dispatch applies security in five layers. If one layer is bypassed, the others remain.
The core asset being protected is: Trip lifecycle data, dispatch documents, driver and
customer records, AI results, audit history, and financial rate data.

### Layer 5 — Perimeter

Controls at the network edge before requests reach the application.

| Control | Status |
|---|---|
| HTTPS redirection outside Development | ✅ Implemented |
| CORS restricted to `FrontendBaseUrl` / `Cors:AllowedOrigins` | ✅ Implemented |
| Startup fails if no CORS origins configured | ✅ Implemented |
| Global API rate limiting plus strict auth endpoint limiting | ✅ Implemented |
| Security headers: HSTS, CSP, X-Content-Type-Options, Referrer-Policy | ✅ Implemented outside Development |
| Swagger/OpenAPI restricted to Development only | ✅ Implemented |

### Layer 4 — Network / Transport

Controls between the reverse proxy and internal services.

| Control | Status |
|---|---|
| TLS termination at reverse proxy | ✅ Implemented (Docker Compose design) |
| Internal container-to-container HTTP only within isolated network | ✅ Implemented |
| `Encrypt=False` disallowed outside local/container network | ✅ Policy enforced |
| SQL Server password via `SA_PASSWORD` environment variable | ✅ Implemented |

### Layer 3 — Application

Controls inside the API that govern what authenticated users can do.

| Control | Status |
|---|---|
| `[Authorize]` on all controllers by default | ✅ Implemented |
| Policy-based role restrictions per endpoint | ✅ Implemented |
| Frontend `RoleGate` as UI convenience only; backend is authoritative | ✅ Implemented |
| Driver trip/document access scoped to `Trip.DriverUserId` | ✅ Implemented |
| Customer portal scoped to authenticated `CustomerId` | ✅ Implemented |
| EF Core parameterized queries; no raw SQL string concatenation | ✅ Implemented |
| User administration actions audited without password values | ✅ Implemented |
| AI suggestions require human confirmation before affecting operations | ✅ By design |
| AI/model service failures return safe errors; dispatch lifecycle continues | ✅ By design |

### Layer 2 — Authentication

Controls governing who can prove identity and obtain a session.

| Control | Status |
|---|---|
| BCrypt password hashing | ✅ Implemented |
| JWT bearer auth with issuer/audience/lifetime validation | ✅ Implemented |
| 32+ byte JWT signing key requirement enforced at startup | ✅ Implemented |
| 1-minute clock skew | ✅ Implemented |
| Login/refresh rate limiting at 5 attempts/IP/min | ✅ Implemented |
| Password policy: 15+ chars, uppercase, number, special character | ✅ Implemented for user creation and reset |
| Common password rejection | ✅ Implemented for user creation and reset |
| MFA / step-up auth for Admin, Manager, Finance, password reset | ⚠️ Backlog |
| Token revocation or short-lived access + refresh token rotation | ✅ Refresh-token rotation implemented |
| Browser token storage hardening (HttpOnly refresh cookie + CSRF marker) | ✅ Implemented |

### Layer 1 — Data

Controls protecting data at rest and ensuring tamper-evident history.

| Control | Status |
|---|---|
| AES-256-CBC field encryption for dispatch financial fields | ✅ Implemented |
| AES-256-CBC field encryption for Customer PII fields | ⚠️ Backlog |
| Destructive reseed/reset guard for disposable database names | ✅ Implemented |
| Append-only `AuditLog` with actor, role snapshot, trace ID | ✅ Implemented |
| `AuthEvent` records for login outcomes | ✅ Implemented |
| EF Core value converters for encrypted fields | ✅ Implemented via `TripConfiguration.cs` |
| All AI suggestions, extraction results, overrides stored for traceability | ✅ By design |
| Encryption key rotation automation | ✅ Implemented for dispatch financial fields |
| Signed short-lived URLs for document download | ⚠️ Backlog |
| Real file upload controls: MIME type, size limit, malware scan, checksum | ⚠️ Backlog |

---

## Configuration Hierarchy

NVG Dispatch uses a three-layer config hierarchy. Each layer overrides the one above it.
**No secrets ever belong in source-controlled files.**

### Layer 1 — `appsettings.json` (committed, no secrets)

Non-secret configuration only:

- JWT issuer, audience, token lifetime, clock skew
- CORS origins placeholder (blank, overridden by environment)
- Log levels
- Feature flags
- Rate limiting defaults

All secret fields must be blank strings or absent entirely.

### Layer 2 — `appsettings.Development.json` (gitignored, dev only)

Developer convenience values:

- `Seed:DevPassword`
- Local SQL Server connection string
- Local JWT key for dev only

This file must never be committed. It must never contain production credentials.

### Layer 3 — Environment variables via `.env` (gitignored, injected by Docker Compose)

All production secrets live here. Docker Compose reads `.env` and injects values as
environment variables into the container. ASP.NET Core reads them natively — the app
never touches the `.env` file directly.

**Do not use the `DotNetEnv` NuGet package.** Docker handles the injection.

Commit `.env.example` with placeholder values as documentation. Never commit `.env`.

Required variables:

```env
# Database
ConnectionStrings__DefaultConnection=

# JWT
Jwt__Key=
Jwt__Issuer=
Jwt__Audience=
Jwt__RefreshTokenDays=

# CORS
FrontendBaseUrl=
Cors__AllowedOrigins=

# SQL Server (Docker Compose SQL service)
SA_PASSWORD=

# Field encryption
EncryptionKey=
# For key rotation use:
# Encryption__CurrentKeyId=2026-06
# Encryption__Keys__2026-05=previous-key
# Encryption__Keys__2026-06=current-key

# Rate limiting overrides (optional)
RateLimiting__Global__PermitLimit=
RateLimiting__Auth__PermitLimit=

# AI / AWS (add when AI module is implemented)
AWS__AccessKey=
AWS__SecretKey=
AWS__Region=
AWS__S3Bucket=
```

---

## Field-Level Encryption

Dispatch financial fields are encrypted through EF Core value converters defined in
`TripConfiguration.cs`. The converters store encrypted strings in SQL Server and decrypt
values only when EF materializes the entity.

`EncryptionKey` must contain at least 32 bytes. Production and other non-Development
environments fail startup if the key is missing.

For key rotation, configure a key ring:

```json
{
  "Encryption": {
    "CurrentKeyId": "2026-06",
    "Keys": {
      "2026-05": "previous-32-byte-or-longer-key",
      "2026-06": "current-32-byte-or-longer-key"
    }
  }
}
```

New encrypted writes use the current key ID. Older payloads remain readable as long as
their key remains configured. After changing `CurrentKeyId`, rotate stored fields:

```powershell
dotnet run -- rotate-encryption-key
```

### Currently encrypted fields

Implemented via `SensitiveFieldValueConverters` in EF Core configuration:

| Entity | Field | Storage column |
|---|---|---|
| `Trip` / `DispatchTrip` | `Rate` | `rate_encrypted` |
| `Trip` / `DispatchTrip` | `Payroll` | `payroll_encrypted` |
| `Trip` / `DispatchTrip` | `Allowance` | `allowance_encrypted` |
| `Trip` / `DispatchTrip` | `FuelAmount` | `fuel_amount_encrypted` |
| `Trip` / `DispatchTrip` | `FuelPricePerLiter` | `fuel_price_per_liter_encrypted` |
| `Trip` / `DispatchTrip` | `OfficialReceiptNumber` | `official_receipt_number_encrypted` |
| `DispatchCustomer` | `Address` | `address_encrypted` |
| `DispatchCustomer` | `Contact` | `contact_encrypted` |
| `DispatchCustomer` | `ContactPerson` | `contact_person_encrypted` |
| `DispatchCustomer` | `ContactEmail` | `contact_email_encrypted` |
| `DispatchCustomer` | `Phone` | `phone_encrypted` |
| `Supplier` | `ContactPhone` | `contact_phone_encrypted` |
| `Supplier` | `ContactEmail` | `contact_email_encrypted` |
| `Supplier` | `Address` | `address_encrypted` |

For existing plaintext customer and supplier data, apply migrations and then run:

```powershell
dotnet run -- migrate-pii-encryption
```

### Encryption backlog — PII fields not yet encrypted

These fields are candidates for a future data hardening pass:

| Entity | Field | Notes |
|---|---|---|
| `User` | `PhoneNumber` | Property does not exist yet on `User.cs` — add and encrypt together |
| Payment proof reference numbers | TBD | Entities not found in current repo; add when implemented |
| Payment reference numbers | TBD | Entities not found in current repo; add when implemented |

When implementing, follow the same `SensitiveFieldValueConverters` pattern used in
the current encrypted fields. Do not write raw SQL inserts into encrypted columns.

### Encrypted seed data rule

All seed scripts must write encrypted fields through EF Core (`DbContext.Add`, domain
services, and `SaveChanges`). Raw SQL inserts into encrypted columns are prohibited because
they bypass converters and store plaintext.

### Sensitive field masking

Role behavior for revealing encrypted fields in the UI:

| Role | Can view full value |
|---|---|
| Admin, CEO | Yes — all fields |
| Manager, HeadOfFinance | Yes — financial fields; masked driver PII |
| Dispatcher | Masked by default; may reveal operational fields |
| Driver | Own records only |
| Customer | Own records only |

Implement masking through `ISensitiveFieldService` / `RevealField` pattern.

---

## Security Inventory

| Area | Current controls found in code |
|---|---|
| Authentication | JWT bearer auth, issuer/audience/lifetime validation, 1 minute clock skew, 32+ byte signing key requirement, BCrypt password hashing |
| API rate limiting | Global fixed-window limit defaults to 120 requests per user/IP per minute. Login and refresh share a stricter auth limit of 5 requests per IP per minute |
| Authorization | Controllers use `[Authorize]` and role-based restrictions. Frontend `RoleGate` is only UI gating; backend policy remains authoritative |
| Dispatch ownership | Driver trip/document access checks compare the authenticated user to `Trip.DriverUserId`; privileged dispatch roles can access wider trip views |
| Customer portal ownership | Portal users must have a linked `CustomerId`; shipment requests are filtered by that customer |
| Auditability | `AuditLog`, `AuthEvent`, trace IDs, correlation IDs, actor IDs, role snapshots, and login outcome records are stored |
| Data access | EF Core is used for normal queries. The one `FromSqlRaw` inventory lock query uses a parameter placeholder rather than string concatenation |
| CORS | Allowed origins are configured from `FrontendBaseUrl` and `Cors:AllowedOrigins`; startup fails if no origins are configured |
| Transport | HTTPS redirection is enabled outside Development. Containers expose HTTP internally and should sit behind TLS termination |
| Secrets | Production/staging appsettings leave connection strings blank and expect environment/user-secret configuration. No production secret was found in the scan |
| Documents | Current document "upload" APIs store `StorageKey` metadata, not file bytes. Treat these keys as sensitive references |

---

## Current Scan Results

Commands run on 2026-06-11:

```powershell
dotnet list NVGInventory.sln package --vulnerable
npm audit --audit-level=high
```

Results:

- `NVGInventory` and `NVGInventory.Tests`: no vulnerable NuGet packages reported.
- `frontend/`: 7 advisories including high-severity issues in React Router, Rollup, and Picomatch.
- `landing/`: 5 advisories including high-severity issues in Vite, Flatted, and Picomatch.

Run `npm audit fix` in each affected frontend workspace, review lockfile changes, then rebuild
and smoke test. Avoid `npm audit fix --force` unless breaking upgrades have been reviewed.

---

## Required Production Configuration

Set these outside source control via `.env` or deployment-managed environment variables:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Key` — at least 32 random bytes/characters
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__RefreshTokenDays`
- `RateLimiting__Global__PermitLimit`
- `RateLimiting__Auth__PermitLimit`
- `FrontendBaseUrl` or `Cors__AllowedOrigins`
- `SA_PASSWORD` — when using the SQL Server Docker Compose profile
- `EncryptionKey` — at least 32 random bytes/characters for field-level encryption
- For rotation: `Encryption__CurrentKeyId` plus `Encryption__Keys__<key-id>` entries
- Future AI/AWS: `AWS__AccessKey`, `AWS__SecretKey`, region, S3 bucket, model service credentials

Production must not use:

- `Seed:DevPassword` from `appsettings.Development.json`
- Demo credentials from seed scripts
- Destructive reseed/reset commands — limited to `Development` and database names
  ending in `_Dev`, `_Demo`, `_Perf`, or `_Tests`
- `Encrypt=False` database connections outside an isolated local/container network
- Broad CORS origins or wildcard document storage access

---

## SaaS-Aware Security Notes

The system is currently single-tenant. The architecture is designed for future multi-tenant
evolution. When that transition happens, these security controls must be extended:

**Data isolation:** Every entity that is currently company-scoped by deployment must gain a
`TenantId` column. All queries must filter by the authenticated user's tenant claim. A missing
`TenantId` filter is a cross-tenant data leak — treat it with the same severity as a missing
`[Authorize]` attribute.

**JWT claims:** The JWT payload must include a `tenant_id` claim. The authorization middleware
must validate that the claim matches the resource being accessed on every request.

**Encryption keys:** The current single AES key should evolve to per-tenant keys stored in a
secrets manager (e.g. Azure Key Vault, AWS Secrets Manager). This prevents a compromised key
from exposing all tenants' data.

**Audit log:** `AuditLog` and `AuthEvent` must include `TenantId` so that platform-level
admins and tenant-level admins see only their own audit trails.

**Document storage:** S3 keys must be namespaced by tenant ID. A tenant must never be able to
construct or guess another tenant's storage key.

**AI model isolation:** If per-tenant ML models are introduced in the future, model artifacts
and training data must be stored in tenant-scoped storage with no cross-tenant access.

**Super admin scope:** A platform-level super admin role must be strictly separated from
tenant-level admin roles. Super admins can manage tenants but must not access tenant
operational data except through an explicit, audited break-glass procedure.

---

## Known Hardening Backlog

- Encrypt `Customer` PII fields (`Address`, `Contact`, `Phone`) using the existing
  `SensitiveFieldValueConverters` pattern from `TripConfiguration.cs`.
- Add `PhoneNumber` to `User` entity and encrypt it at the same time.
- Add MFA or step-up authentication for `Admin`, `SuperAdmin`, manager approval, finance
  verification, and password reset flows.
- Add optional access-token JTI revocation for immediate access-token invalidation.
  Refresh tokens are server-side hashed, rotated, and revocable.
- Add real file upload controls before accepting document bytes: signed upload/download URLs,
  allowed MIME types, file size limits, malware scanning, checksum capture, and
  tenant/customer/trip-scoped storage keys.
- Replace direct `StorageKey` download responses with signed, short-lived URLs or a
  controlled file streaming endpoint.
- Remove or secure `InventoryERP.Api/` before deployment if it is no longer a sample.

---

## Secure Development Checklist

- Every new controller must start with `[Authorize]`; add `[AllowAnonymous]` only with a
  written reason.
- Backend roles must enforce all privileged actions. Do not rely on frontend route hiding.
- Dispatch lifecycle changes must create `TripStatusHistory` and audit records.
- Customer-facing endpoints must filter by authenticated customer ownership.
- Driver endpoints must filter by authenticated driver assignment.
- Use EF Core parameterized queries. Do not concatenate SQL.
- Do not log passwords, tokens, JWT signing keys, connection strings, document contents,
  or raw Textract responses containing sensitive customer data.
- Store all generated AI suggestions, extraction results, overrides, and manager reviews
  for traceability.
- If an AI/Textract/model service fails, return a safe error and let the dispatch lifecycle
  continue unblocked.
- When adding new entities, ask: does this contain PII, financial data, or operational
  cost data? If yes, add it to the field encryption backlog before merging.
- All seed scripts must write encrypted fields through EF Core only — no raw SQL inserts
  into encrypted columns.
- Run these before every release:

```powershell
dotnet test NVGInventory.Tests\NVGInventory.Tests.csproj
dotnet list NVGInventory.sln package --vulnerable
cd frontend
npm audit --audit-level=high
cd ..\landing
npm audit --audit-level=high
```
