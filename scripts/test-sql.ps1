$ErrorActionPreference = "Stop"

if (-not $env:TEST_SQLSERVER_CONNECTION_STRING -or [string]::IsNullOrWhiteSpace($env:TEST_SQLSERVER_CONNECTION_STRING)) {
    $env:TEST_SQLSERVER_CONNECTION_STRING = "Server=localhost;Database=NVG_Inventory_Tests;Trusted_Connection=True;TrustServerCertificate=True;"
}

Write-Host "Using TEST_SQLSERVER_CONNECTION_STRING:"
Write-Host $env:TEST_SQLSERVER_CONNECTION_STRING
Write-Host ""

dotnet test
