Param(
  [string]$CsvPath = (Join-Path $PSScriptRoot "..\\inventory_clean.csv"),
  [string]$ApiUrl = "http://localhost:5105",
  [string]$Username = "Superadmin",
  [string]$Password = "Super123!",
  [switch]$DryRun,
  [int]$BatchSize = 100
)

function Get-Token {
  param([string]$ApiUrl,[string]$Username,[string]$Password)
  $body = @{ username = $Username; password = $Password } | ConvertTo-Json
  $resp = Invoke-RestMethod -Uri "$ApiUrl/api/auth/login" -Method Post -Body $body -ContentType "application/json"
  return $resp.accessToken
}

function Get-IsKit {
  param([string]$Unit)
  if (-not $Unit) { return $false }
  $u = $Unit.Trim().ToUpperInvariant()
  return $u -eq "SET" -or $u -eq "KIT"
}

function Get-ItemType {
  param([string]$Unit)
  if (-not $Unit) { return "CONSUMABLE" }
  $u = $Unit.Trim().ToUpperInvariant()
  $nonConsumableUnits = @("PC","PCS","EA","UNIT","SET","KIT")
  if ($nonConsumableUnits -contains $u) { return "NON_CONSUMABLE" }
  return "CONSUMABLE"
}

function Is-HeaderGarbage {
  param([string]$Name)
  if (-not $Name) { return $true }
  $n = $Name.Trim()
  if ($n.Length -gt 200) { return $true }
  if ($n -match '(?i)\bINVTY\.|\bSTOCK ITEM\b|\bSTOCK REF\b|\bSTOCK OUT\b|\bBALANCE\b') { return $true }
  return $false
}

function Ensure-Role {
  param([string]$ApiUrl,[hashtable]$Headers,[string]$UserId,[string]$RoleName)
  $payload = @{ roleName = $RoleName } | ConvertTo-Json
  try {
    Invoke-RestMethod -Uri "$ApiUrl/api/users/$UserId/roles" -Headers $Headers -Method Post -Body $payload -ContentType "application/json" | Out-Null
  } catch {
    Write-Warning ("Failed to assign role {0} to user {1}: {2}" -f $RoleName, $UserId, $_.Exception.Message)
  }
}

if (-not (Test-Path $CsvPath)) {
  Write-Error "CSV not found: $CsvPath"
  exit 1
}

$rows = Import-Csv -Path $CsvPath
$parsedByName = @{}
$parsed = @()
$skippedInvalid = 0
$skippedDuplicate = 0
$skippedHeader = 0

foreach ($row in $rows) {
  $name = ($row.stock_item -as [string])
  if ($null -eq $name) { $name = "" }
  $name = $name.Trim()
  if ([string]::IsNullOrWhiteSpace($name)) { continue }
  if (Is-HeaderGarbage -Name $name) {
    $skippedHeader++
    continue
  }

  $unit = ($row.unit -as [string])
  if ($null -eq $unit) { $unit = "" }
  $unit = $unit.Trim()
  if ([string]::IsNullOrWhiteSpace($unit)) {
    Write-Warning ("Missing unit for '{0}' (skipping)" -f $name)
    $skippedInvalid++
    continue
  }

  $quantity = 0
  $rawBalance = ($row.balance -as [string])
  if ([string]::IsNullOrWhiteSpace($rawBalance)) {
    $quantity = 0
  } else {
    $parsedBalance = 0
    $ok = [decimal]::TryParse(
      $rawBalance,
      [System.Globalization.NumberStyles]::Number,
      [System.Globalization.CultureInfo]::InvariantCulture,
      [ref]$parsedBalance)
    if (-not $ok) {
      Write-Warning ("Invalid balance for '{0}': '{1}' (skipping)" -f $name, $rawBalance)
      $skippedInvalid++
      continue
    }
    $quantity = $parsedBalance
  }

  $quantity = [Math]::Round([decimal]$quantity, 2)
  if ($quantity -lt 0) {
    Write-Warning ("Negative balance for '{0}': {1} (skipping)" -f $name, $quantity)
    $skippedInvalid++
    continue
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
  $isKit = Get-IsKit -Unit $unit

  $key = $name.ToLowerInvariant()
  if ($parsedByName.ContainsKey($key)) {
    $skippedDuplicate++
    continue
  }

  $item = [pscustomobject]@{
    Name = $name
    Unit = $unit
    ItemType = $itemType
    IsKit = $isKit
    Quantity = $quantity
    Location = if ($location) { $location } else { $null }
  }
  $parsedByName[$key] = $item
}

$parsed = $parsedByName.Values

if ($BatchSize -le 0) { $BatchSize = 100 }

if ($DryRun) {
  foreach ($item in $parsed) {
    Write-Output ("DRYRUN: {0} | {1} | {2} | {3}" -f $item.Name, $item.ItemType, $item.Unit, $item.Quantity)
  }
  Write-Output ("Parsed: {0}" -f $parsed.Count)
  Write-Output ("Skipped (header/garbage): {0}" -f $skippedHeader)
  Write-Output ("Skipped (duplicate names): {0}" -f $skippedDuplicate)
  Write-Output ("Skipped (invalid): {0}" -f $skippedInvalid)
  exit 0
}

$token = Get-Token -ApiUrl $ApiUrl -Username $Username -Password $Password
$headers = @{ Authorization = "Bearer $token" }

$me = Invoke-RestMethod -Uri "$ApiUrl/api/auth/me" -Headers $headers -Method Get
$userId = $me.userId
Ensure-Role -ApiUrl $ApiUrl -Headers $headers -UserId $userId -RoleName "InventoryOfficer"
Ensure-Role -ApiUrl $ApiUrl -Headers $headers -UserId $userId -RoleName "Manager"

# Fetch existing inventory names to avoid duplicates
$existing = @{}
try {
  $items = Invoke-RestMethod -Uri "$ApiUrl/api/inventory" -Headers $headers -Method Get
  foreach ($item in $items) {
    if ($item.name) {
      $existing[$item.name.ToLowerInvariant()] = [pscustomobject]@{
        id = $item.id
        quantity = [Math]::Round([decimal]$item.quantity, 2)
        itemType = $item.itemType
        isKit = $item.isKit
      }
    }
  }
} catch {
  Write-Warning "Could not load existing inventory. Will attempt to create all rows."
}

$created = 0
$matchedExisting = 0
$adjustedExisting = 0
$adjustedNew = 0
$adjustLines = @()

foreach ($row in $parsed) {
  $name = $row.Name
  $key = $name.ToLowerInvariant()
  if ($existing.ContainsKey($key)) {
    $matchedExisting++
    $existingItem = $existing[$key]
    $currentQty = [decimal]$existingItem.quantity
    $targetQty = [decimal]$row.Quantity
    $delta = [Math]::Round($targetQty - $currentQty, 2)
    if ($delta -ne 0) {
      $adjustLines += [pscustomobject]@{
        inventoryId = $existingItem.id
        qtyDelta = $delta
        remarks = "CSV import"
      }
      $adjustedExisting++
    }
    continue
  }

  $payload = @{ 
    name = $name
    unit = $row.Unit
    itemType = $row.ItemType
    isKit = $row.IsKit
    quantity = 0
    reorderLevel = $null
    location = $row.Location
    unitValue = $null
  } | ConvertTo-Json

  try {
    $resp = Invoke-RestMethod -Uri "$ApiUrl/api/inventory" -Headers $headers -Method Post -Body $payload -ContentType "application/json"
    $created++
    $existing[$key] = [pscustomobject]@{ id = $resp.id; quantity = 0; itemType = $row.ItemType; isKit = $row.IsKit }
    if ($row.Quantity -ne 0) {
      $adjustLines += [pscustomobject]@{
        inventoryId = $resp.id
        qtyDelta = $row.Quantity
        remarks = "CSV import"
      }
      $adjustedNew++
    }
  } catch {
    Write-Warning ("Failed to create {0}: {1}" -f $name, $_.Exception.Message)
  }
}

foreach ($row in $parsed) {
  if (-not $row.IsKit) { continue }
  $key = $row.Name.ToLowerInvariant()
  if (-not $existing.ContainsKey($key)) { continue }
  $existingItem = $existing[$key]
  if ($existingItem.isKit -eq $true) { continue }
  if ($existingItem.itemType -ne "NON_CONSUMABLE") {
    Write-Warning ("Cannot mark kit for '{0}' because itemType is {1}" -f $row.Name, $existingItem.itemType)
    continue
  }

  $payload = @{ isKit = $true } | ConvertTo-Json
  try {
    Invoke-RestMethod -Uri "$ApiUrl/api/inventory/$($existingItem.id)/kit" -Headers $headers -Method Patch -Body $payload -ContentType "application/json" | Out-Null
  } catch {
    Write-Warning ("Failed to set kit flag for {0}: {1}" -f $row.Name, $_.Exception.Message)
  }
}

if ($adjustLines.Count -gt 0) {
  $reason = "CSV import"
  for ($i = 0; $i -lt $adjustLines.Count; $i += $BatchSize) {
    $upper = [Math]::Min($i + $BatchSize - 1, $adjustLines.Count - 1)
    $batch = $adjustLines[$i..$upper]
    $draftPayload = @{
      reason = $reason
      lines = $batch
    } | ConvertTo-Json -Depth 6

    try {
      $draft = Invoke-RestMethod -Uri "$ApiUrl/api/inventory-adjustments" -Headers $headers -Method Post -Body $draftPayload -ContentType "application/json"
      Invoke-RestMethod -Uri "$ApiUrl/api/inventory-adjustments/$($draft.adjustmentId)/submit" -Headers $headers -Method Post | Out-Null
      $approvePayload = @{ remarks = "CSV import" } | ConvertTo-Json
      Invoke-RestMethod -Uri "$ApiUrl/api/inventory-adjustments/$($draft.adjustmentId)/approve" -Headers $headers -Method Post -Body $approvePayload -ContentType "application/json" | Out-Null
    } catch {
      Write-Warning ("Failed to apply adjustment batch starting at index {0}: {1}" -f $i, $_.Exception.Message)
    }
  }
}

Write-Output "Imported: $created"
Write-Output "Matched (existing): $matchedExisting"
Write-Output "Adjusted (existing): $adjustedExisting"
Write-Output "Adjusted (new): $adjustedNew"
Write-Output "Skipped (header/garbage): $skippedHeader"
Write-Output "Skipped (duplicate names): $skippedDuplicate"
Write-Output "Skipped (invalid): $skippedInvalid"
