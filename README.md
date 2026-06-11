# NVGInventory

## Tests (SQL Server)
Integration tests run against a real SQL Server database. Set the env var before running:

```powershell
$env:TEST_SQLSERVER_CONNECTION_STRING="Server=localhost;Database=NVG_Inventory_Tests;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet test
```

Notes:
- The database name must end with `_Tests` or tests will refuse to run.
- The fixture drops and recreates the test database at the start of the test run.
- Demo/performance reseeding also refuses destructive resets unless the database name ends
  with `_Dev`, `_Demo`, `_Perf`, or `_Tests`.

## Auth (JWT)
Set a signing key (32+ chars) via user-secrets or environment variable `JWT__KEY`.
Login at `POST /api/auth/login` to obtain a bearer token, then include:

```
Authorization: Bearer <token>
```

Passwords are stored using BCrypt hashes.
New and reset passwords must be at least 15 characters and include one uppercase letter,
one number, and one special character.

Current user endpoint:
- `GET /api/auth/me`

Dev seed user (Development only):
- `Superadmin` / `SuperAdminDemo1!`

Example (PowerShell):
```powershell
dotnet user-secrets set "Jwt:Key" "CHANGE_ME_TO_A_32+_CHAR_RANDOM_SECRET"
```
