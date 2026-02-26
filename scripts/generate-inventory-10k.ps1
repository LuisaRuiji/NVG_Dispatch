Param(
  [string]$InputPath = "C:\Users\luigi\Downloads\inventory_clean.csv",
  [string]$OutputPath = "C:\Users\luigi\Downloads\inventory_clean_10k.csv",
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

$rand = New-Object System.Random
$results = New-Object System.Collections.Generic.List[object]

for ($i = 1; $i -le $Rows; $i++) {
  $row = $source[$rand.Next(0, $source.Count)]

  $nameBase = ($row.stock_item -as [string])
  if ([string]::IsNullOrWhiteSpace($nameBase)) { $nameBase = "ITEM" }
  $name = "{0} - Batch {1}" -f $nameBase.Trim(), $i

  $unit = ($row.unit -as [string])
  if ($null -eq $unit) { $unit = "PC" }

  $section = ($row.section -as [string])
  if ($null -eq $section -or $section.Trim().Length -eq 0) { $section = "MISC" }

  $stockRef = ($row.stock_ref -as [string])
  if ($null -eq $stockRef -or $stockRef.Trim().Length -eq 0) { $stockRef = "REF" }
  $stockRef = "{0}-{1}" -f $stockRef.Trim(), $i

  $initial = [math]::Round(($rand.NextDouble() * 50), 1)
  $stockIn = [math]::Round(($rand.NextDouble() * 50), 1)
  $stockOut = [math]::Round(($rand.NextDouble() * 50), 1)
  $balance = $initial + $stockIn - $stockOut
  if ($balance -lt 0) {
    $stockOut = $initial + $stockIn
    $balance = 0
  }

  $results.Add([pscustomobject]@{
    page = $row.page
    section = $section
    inv_no = $row.inv_no
    stock_item = $name
    stock_ref = $stockRef
    unit = $unit
    initial_stock = "{0:N1}" -f $initial
    stock_in = "{0:N1}" -f $stockIn
    stock_out = "{0:N1}" -f $stockOut
    balance = "{0:N1}" -f $balance
  }) | Out-Null
}

$results | Export-Csv -Path $OutputPath -NoTypeInformation
Write-Output "Wrote $Rows rows to $OutputPath"
