param(
    [string]$ConnectionString
)

if ($ConnectionString) {
    $env:ConnectionStrings__DefaultConnection = $ConnectionString
}

if (-not $env:ASPNETCORE_ENVIRONMENT) {
    $env:ASPNETCORE_ENVIRONMENT = "Development"
}

dotnet run -- seed-demo --reset
