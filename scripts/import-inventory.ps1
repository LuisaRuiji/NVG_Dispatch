Param(
  [string]$CsvPath = "C:\Users\luigi\Downloads\inventory_clean.csv",
  [string]$ApiUrl = "http://localhost:5105",
  [string]$Username = "Superadmin",
  [string]$Password = "Super123!",
  [switch]$DryRun
)

function Get-Token {
  param([string]$ApiUrl,[string]$Username,[string]$Password)
  $body = @{ username = $Username; password = $Password } | ConvertTo-Json
  $resp = Invoke-RestMethod -Uri "$ApiUrl/api/auth/login" -Method Post -Body $body -ContentType "application/json"
  return $resp.accessToken
}

function Get-ItemType {
  param([string]$Unit)
  if (-not $Unit) { return "Consumable" }
  $u = $Unit.Trim().ToUpperInvariant()
  $nonConsumableUnits = @("PC","PCS","EA","UNIT","SET","KIT")
  if ($nonConsumableUnits -contains $u) { return "NonConsumable" }
  return "Consumable"
}

if (-not (Test-Path $CsvPath)) {
  Write-Error "CSV not found: $CsvPath"
  exit 1
}

$token = Get-Token -ApiUrl $ApiUrl -Username $Username -Password $Password
$headers = @{ Authorization = "Bearer $token" }

# Fetch existing inventory names to avoid duplicates
$existing = @{}
try {
  $items = Invoke-RestMethod -Uri "$ApiUrl/api/inventory" -Headers $headers -Method Get
  foreach ($item in $items) {
    if ($item.name) { $existing[$item.name.ToLowerInvariant()] = $true }
  }
} catch {
  Write-Warning "Could not load existing inventory. Will attempt to create all rows."
}

$rows = Import-Csv -Path $CsvPath
$created = 0
$skipped = 0

foreach ($row in $rows) {
  $name = ($row.stock_item -as [string])
  if ($null -eq $name) { $name = "" }
  $name = $name.Trim()
  if ([string]::IsNullOrWhiteSpace($name)) { continue }

  $key = $name.ToLowerInvariant()
  if ($existing.ContainsKey($key)) {
    $skipped++
    continue
  }

  $unit = ($row.unit -as [string])
  if ($null -eq $unit) { $unit = "" }
  $unit = $unit.Trim()
  $quantity = 0
  if ($row.balance) {
    $quantity = [decimal]$row.balance
  }

  $location = ($row.stock_ref -as [string])
  if ($null -eq $location) { $location = "" }
  $location = $location.Trim()
  if ([string]::IsNullOrWhiteSpace($location)) {
    $location = ($row.section -as [string])
    if ($null -eq $location) { $location = "" }
    $location = $location.Trim()
  }

  $itemType = Get-ItemType -Unit $unit

  $payload = @{ 
    name = $name
    unit = $unit
    itemType = $itemType
    quantity = $quantity
    reorderLevel = $null
    location = if ($location) { $location } else { $null }
    unitValue = $null
  } | ConvertTo-Json

  if ($DryRun) {
    Write-Output "DRYRUN: $name | $itemType | $unit | $quantity"
    $created++
    continue
  }

  try {
    Invoke-RestMethod -Uri "$ApiUrl/api/inventory" -Headers $headers -Method Post -Body $payload -ContentType "application/json" | Out-Null
    $created++
    $existing[$key] = $true
  } catch {
    Write-Warning ("Failed to create {0}: {1}" -f $name, $_.Exception.Message)
  }
}

Write-Output "Imported: $created"
Write-Output "Skipped (existing): $skipped"
