# NVGInventory

## Database (Local SQL Server)
Default dev connection targets `localhost\\SQLEXPRESS`. If your instance name differs, override with:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=localhost\\YOUR_INSTANCE;Database=NVG_Inventory;Trusted_Connection=True;TrustServerCertificate=True;"
```

## Tests (SQL Server)
Integration tests run against a real SQL Server database. Set the env var before running:

```powershell
$env:TEST_SQLSERVER_CONNECTION_STRING="Server=localhost;Database=NVG_Inventory_Tests;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet test
```

Notes:
- The database name must end with `_Tests` or tests will refuse to run.
- The fixture drops and recreates the test database at the start of the test run.

## Auth (JWT)
Set a signing key (32+ chars) via user-secrets or environment variable `JWT__KEY`.
Login at `POST /api/auth/login` to obtain a bearer token, then include:

```
Authorization: Bearer <token>
```

Passwords are stored using BCrypt hashes.

Current user endpoint:
- `GET /api/auth/me`

Dev seed users (Development only):
- `io1` / `plaintext`
- `mgr1` / `plaintext`
- `drv1` / `plaintext`

Password is configured in `appsettings.Development.json` under `Seed:DevPassword`.

Example (PowerShell):
```powershell
dotnet user-secrets set "Jwt:Key" "CHANGE_ME_TO_A_32+_CHAR_RANDOM_SECRET"
```
