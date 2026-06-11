# Security Implementation Policy

Last updated: 2026-06-11

This file tracks the slow, practical implementation of `SECURITY.md`. Keep each item small
enough to build, test, and review independently.

## Completed

| Date | Item | Evidence |
|------------|---------------------------------|---------------------------------------------------------------------------------------------------------------------|
| 2026-06-11 | Production API security headers | `Program.cs` applies CSP, HSTS, X-Content-Type-Options, Referrer-Policy, Permissions-Policy, and X-Frame-Options outside Development |
| 2026-06-11 | Swagger/OpenAPI limited to Development | `Program.cs` only enables Swagger inside `app.Environment.IsDevelopment()` |
| 2026-06-11 | Password length and complexity policy | `PasswordPolicy` requires 15+ characters, one uppercase letter, one number, and one special character for user creation and password reset |
| 2026-06-11 | Common password rejection | `PasswordPolicy` rejects known predictable passwords even when they satisfy complexity rules |
| 2026-06-11 | Destructive database reset guard | `DatabaseResetGuard` allows reset only for disposable database names ending `_Dev`, `_Demo`, `_Perf`, or `_Tests` |
| 2026-06-11 | Dispatch financial field encryption | `Trip` financial fields are stored in `_encrypted` columns through EF Core value converters and migration `EncryptFinancialFields` |
| 2026-06-11 | User administration audit logging | User create, role changes, status changes, password resets, and deactivation write redacted `AuditLog` entries |
| 2026-06-11 | Refresh-token rotation | `RefreshToken` records store token hashes only; refresh rotates tokens and reuse revokes the token family |
| 2026-06-11 | Encryption key rotation support | Encrypted fields use key-ring-aware `v2` payloads and `dotnet run -- rotate-encryption-key` re-encrypts dispatch financial fields |
| 2026-06-11 | Browser token storage hardening | SPA access tokens are memory-only; refresh tokens are `HttpOnly` `SameSite=Strict` cookies under `/api/auth`; cookie refresh/logout require `X-NVG-CSRF` |
| 2026-06-11 | API rate limiting | Global API traffic is fixed-window limited per user/IP; login and refresh share a stricter per-IP auth limiter |

## In Progress

| Item | Target |
|---|---|
| Keep `SECURITY.md` aligned with implemented controls | Update status whenever a security control moves from backlog to code |

## Backlog

| Priority | Item | Notes |
|----------|----------------------|----------------------------------------------------------------------------|
| High | npm advisory remediation | `frontend/` and `landing/` currently have high-severity npm audit findings |
| Medium | Access-token JTI revocation | Optional future step for immediate invalidation of already-issued access tokens |
| Medium | Signed document links | Replace direct `StorageKey` responses with signed short-lived URLs or controlled streaming |
| Medium | Real file upload controls | MIME type allowlist, size limits, checksum capture, malware scanning |
| Low | Extend field-level encryption | Review remaining PII and financial reference candidates after dispatch financial fields settle |

## Field Encryption Policy

Dispatch financial fields are encrypted at rest through EF Core value converters:

- `Rate`
- `Payroll`
- `Allowance`
- `FuelAmount`
- `FuelPricePerLiter`
- `OfficialReceiptNumber`

`EncryptionKey` must contain at least 32 bytes. Non-Development environments fail startup if
the key is missing. Local Development can run without the key only while encrypted field values
remain null.

Seed and reseed code must write encrypted fields through EF Core (`DbContext.Add`, domain
services, and `SaveChanges`). Do not use raw SQL inserts for encrypted columns.

## Database Reset Policy

Destructive reseeding is allowed only for local disposable databases. Any database reset must
pass both checks:

- Application environment is `Development`
- Database name ends with `_Dev`, `_Demo`, `_Perf`, or `_Tests`

Do not point `seed-demo`, `seed-perf -Reset`, or any future destructive data script at a
shared, staging, or production database.

## Current Password Policy

New and reset passwords must meet all requirements:

- At least 15 characters
- At least one uppercase letter
- At least one numeric digit
- At least one special character
- Not a known common or predictable password

This policy is enforced in `UserService.CreateUserAsync` and `UserService.ResetPasswordAsync`.

## Refresh Token Policy

Refresh tokens are random secrets shown only to the client once. The database stores only
SHA-256 token hashes. Every refresh request rotates the refresh token; reuse of an old rotated
token revokes the active token family. Logout revokes the current refresh token.

## Browser Token Storage Policy

The React SPA must not persist bearer tokens in `localStorage` or `sessionStorage`. Access tokens
live only in frontend module memory and are reacquired through `/api/auth/refresh` after a page
reload. Refresh tokens are stored in an `HttpOnly`, `SameSite=Strict` cookie scoped to
`/api/auth`; JavaScript cannot read the refresh token value. Cookie-backed refresh and logout
requests must include `X-NVG-CSRF: 1`.

## Rate Limiting Policy

All API requests are protected by a global fixed-window limiter. Authenticated requests are
partitioned by user ID; anonymous requests are partitioned by remote IP. Login and refresh
requests share a stricter per-IP limiter to slow credential stuffing and refresh-token abuse.
Health checks are excluded from application-level rate limiting.

## Encryption Key Rotation Policy

Use `Encryption:CurrentKeyId` and `Encryption:Keys:{keyId}` when rotating field-encryption
keys. Keep the previous key configured until all encrypted data has been re-encrypted.

Run this after changing `Encryption:CurrentKeyId`:

```powershell
dotnet run -- rotate-encryption-key
```

The command currently re-encrypts dispatch financial fields through EF value converters. Do not
remove the old key until the command completes successfully and the app has been smoke-tested.
