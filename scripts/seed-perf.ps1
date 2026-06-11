param(
    [string]$ConnectionString,
    [int]$Requests = 1000,
    [int]$PendingManager = 200,
    [int]$Approved = 100,
    [switch]$Reset
)

$ErrorActionPreference = "Stop"

if ($ConnectionString) {
    $env:ConnectionStrings__DefaultConnection = $ConnectionString
}

if (-not $env:ASPNETCORE_ENVIRONMENT) {
    $env:ASPNETCORE_ENVIRONMENT = "Development"
}

$argsList = @("seed-perf", "--requests=$Requests", "--pending-manager=$PendingManager", "--approved=$Approved")
if ($Reset) {
    Write-Warning "This command recreates the target database. The database name must end in _Dev, _Demo, _Perf, or _Tests."
    $argsList += "--reset"
}

dotnet run -- $argsList
