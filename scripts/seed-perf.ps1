param(
    [string]$ConnectionString,
    [int]$Requests = 1000,
    [int]$PendingManager = 200,
    [int]$Approved = 100,
    [switch]$Reset
)

if ($ConnectionString) {
    $env:ConnectionStrings__DefaultConnection = $ConnectionString
}

if (-not $env:ASPNETCORE_ENVIRONMENT) {
    $env:ASPNETCORE_ENVIRONMENT = "Development"
}

$argsList = @("seed-perf", "--requests=$Requests", "--pending-manager=$PendingManager", "--approved=$Approved")
if ($Reset) {
    $argsList += "--reset"
}

dotnet run -- $argsList
