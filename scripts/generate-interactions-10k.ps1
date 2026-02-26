Param(
  [string]$InputPath = "C:\Users\luigi\source\repos\Ecommerce-web-app\exports\interactions-2026-02-14.csv",
  [string]$OutputPath = "C:\Users\luigi\source\repos\Ecommerce-web-app\exports\interactions-2026-02-14-10k.csv",
  [int]$Rows = 10000
)

if (-not (Test-Path $InputPath)) {
  Write-Error "Input CSV not found: $InputPath"
  exit 1
}

$source = Import-Csv -Path $InputPath
if (-not $source -or $source.Count -eq 0) {
  Write-Error "Input CSV is empty."
  exit 1
}

# Collect ranges
$userIds = $source | ForEach-Object { [int]$_.userId } | Sort-Object -Unique
$productIds = $source | ForEach-Object { [int]$_.productId } | Sort-Object -Unique

if ($userIds.Count -eq 0 -or $productIds.Count -eq 0) {
  Write-Error "Input CSV missing userId/productId values."
  exit 1
}

$minUser = ($userIds | Measure-Object -Minimum).Minimum
$maxUser = ($userIds | Measure-Object -Maximum).Maximum
$minProduct = ($productIds | Measure-Object -Minimum).Minimum
$maxProduct = ($productIds | Measure-Object -Maximum).Maximum

# Action type distribution
$actions = @("VIEW","ADD_TO_CART","WISHLIST","PURCHASE")
$weights = @(0.7, 0.2, 0.07, 0.03)

function Get-RandomWeightedAction {
  $r = Get-Random -Minimum 0.0 -Maximum 1.0
  $sum = 0.0
  for ($i=0; $i -lt $actions.Length; $i++) {
    $sum += $weights[$i]
    if ($r -le $sum) { return $actions[$i] }
  }
  return $actions[-1]
}

# Time window based on existing data
$dates = $source | ForEach-Object { [datetime]$_.createdAt }
$minDate = ($dates | Measure-Object -Minimum).Minimum
$maxDate = ($dates | Measure-Object -Maximum).Maximum
if (-not $minDate -or -not $maxDate) {
  $maxDate = [datetime]::UtcNow
  $minDate = $maxDate.AddDays(-7)
}

$rand = New-Object System.Random
$results = New-Object System.Collections.Generic.List[object]

for ($i = 1; $i -le $Rows; $i++) {
  $userId = $rand.Next($minUser, $maxUser + 1)
  $productId = $rand.Next($minProduct, $maxProduct + 1)
  $action = Get-RandomWeightedAction

  $rangeTicks = ($maxDate - $minDate).Ticks
  $offsetTicks = [int64]($rand.NextDouble() * $rangeTicks)
  $createdAt = $minDate.AddTicks($offsetTicks).ToUniversalTime().ToString("o")

  $results.Add([pscustomobject]@{
    userId = $userId
    productId = $productId
    actionType = $action
    createdAt = $createdAt
  }) | Out-Null
}

$results | Export-Csv -Path $OutputPath -NoTypeInformation
Write-Output "Wrote $Rows rows to $OutputPath"
