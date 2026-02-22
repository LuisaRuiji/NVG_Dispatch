$api = $env:NVG_API_URL
if (-not $api) { $api = "http://localhost:5093" }

$pw = $env:NVG_DEMO_PASSWORD
if (-not $pw) { $pw = "Demo@123" }

function Login($user, $pass) {
  $body = @{ username = $user; password = $pass } | ConvertTo-Json
  for ($i = 0; $i -lt 3; $i++) {
    try {
      $res = Invoke-RestMethod -Uri "$api/api/auth/login" -Method Post -Body $body -ContentType "application/json"
      return $res.token
    } catch {
      $status = $_.Exception.Response.StatusCode.Value__
      Write-Host "Login failed for $user (status $status)."
      if ($status -eq 429) {
        Start-Sleep -Seconds 65
        continue
      }
      throw
    }
  }
  return $null
}

function GetFirstId($url, $token) {
  $res = Invoke-RestMethod -Uri $url -Headers @{ Authorization = "Bearer $token" }
  if (-not $res.items -or $res.items.Count -eq 0) {
    return $null
  }
  return $res.items[0].id
}

function AssertForbidden($name, $token, $method, $url, $body = $null) {
  try {
    if ($body) {
      Invoke-WebRequest -UseBasicParsing -Uri $url -Method $method -Body ($body | ConvertTo-Json) -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" } | Out-Null
    } else {
      Invoke-WebRequest -UseBasicParsing -Uri $url -Method $method -Headers @{ Authorization = "Bearer $token" } | Out-Null
    }
    Write-Host "FAIL (should be 403): $name"
  } catch {
    $code = $_.Exception.Response.StatusCode.Value__
    if ($code -eq 403) {
      Write-Host "PASS (403): $name"
    } else {
      Write-Host "FAIL (expected 403, got $code): $name"
    }
  }
}

$drv = Login "drv_demo" $pw
Start-Sleep -Seconds 5
$io  = Login "io_demo"  $pw
Start-Sleep -Seconds 5
$mgr = Login "mgr_demo" $pw
Start-Sleep -Seconds 5
$fin = Login "fin_demo" $pw

if (-not $mgr) { throw "Manager login failed." }

$sampleRequestId = GetFirstId "$api/api/requests?page=1&pageSize=1" $mgr
$samplePoId = GetFirstId "$api/api/purchase-orders?page=1&pageSize=1" $mgr

if (-not $sampleRequestId) { throw "No requests found to test against." }
if (-not $samplePoId) { throw "No purchase orders found to test against." }

# 1) Driver cannot issue stock
AssertForbidden "Driver issue stock" $drv "POST" "$api/api/requests/$sampleRequestId/issue-stock"

# 2) Driver cannot IO review
AssertForbidden "Driver IO review" $drv "POST" "$api/api/requests/$sampleRequestId/io-review" @{ decision = "Approve"; lines = @() }

# 3) IO cannot manager decision
AssertForbidden "IO manager decision" $io "POST" "$api/api/requests/$sampleRequestId/manager-decision" @{ decision = "Approve"; remarks = "test" }

# 4) Manager cannot receive PO
AssertForbidden "Manager receive PO" $mgr "POST" "$api/api/purchase-orders/$samplePoId/receive" @{ lines = @() }

# 5) Finance cannot issue stock
AssertForbidden "Finance issue stock" $fin "POST" "$api/api/requests/$sampleRequestId/issue-stock"
