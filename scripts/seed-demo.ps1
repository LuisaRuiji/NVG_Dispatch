param(
    [string]$ConnectionString
)

$ErrorActionPreference = "Stop"

if ($ConnectionString) {
    $env:ConnectionStrings__DefaultConnection = $ConnectionString
}

if (-not $env:ASPNETCORE_ENVIRONMENT) {
    $env:ASPNETCORE_ENVIRONMENT = "Development"
}

Write-Warning "This command recreates the target database. The database name must end in _Dev, _Demo, _Perf, or _Tests."
dotnet run -- seed-demo --reset
