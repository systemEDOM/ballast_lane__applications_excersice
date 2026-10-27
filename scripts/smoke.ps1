# Smoke test against a running stack.
# Usage: powershell -File scripts/smoke.ps1
param(
    [string]$BaseUrl = "http://localhost:5080"
)

$ErrorActionPreference = "Stop"
$script:failures = 0

function Assert-Equal($name, $expected, $actual) {
    if ($expected -eq $actual) {
        Write-Host "  ok   $name ($actual)" -ForegroundColor Green
    }
    else {
        Write-Host "  FAIL $name (expected $expected, got $actual)" -ForegroundColor Red
        $script:failures++
    }
}

Write-Host "Health"
$health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get
Assert-Equal "health status" "ok" $health.status

Write-Host "Public endpoint"
$info = Invoke-RestMethod -Uri "$BaseUrl/api/public/info" -Method Get
Assert-Equal "public info" "Ballast Lane Tasks API" $info.name

Write-Host "Anonymous access is rejected"
try {
    Invoke-WebRequest -Uri "$BaseUrl/api/tasks" -Method Get -UseBasicParsing | Out-Null
    Assert-Equal "tasks without token" 401 200
}
catch {
    Assert-Equal "tasks without token" 401 ([int]$_.Exception.Response.StatusCode)
}

Write-Host "Login"
$login = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post -ContentType "application/json" `
    -Body '{"email":"demo@ballastlane.test","password":"Demo12345!"}'
Assert-Equal "token issued" $true ([bool]$login.accessToken)
$headers = @{ Authorization = "Bearer $($login.accessToken)" }

Write-Host "Profile"
$me = Invoke-RestMethod -Uri "$BaseUrl/api/auth/me" -Headers $headers -Method Get
Assert-Equal "current user" "demo@ballastlane.test" $me.email

Write-Host "List"
$beforeResponse = Invoke-RestMethod -Uri "$BaseUrl/api/tasks" -Headers $headers -Method Get
$beforeCount = if ($beforeResponse -is [System.Array]) { $beforeResponse.Length } else { 1 }
Write-Host "  seeded tasks: $beforeCount"
if ($beforeCount -lt 6) {
    Write-Host "  FAIL expected at least 6 seeded tasks" -ForegroundColor Red
    $script:failures++
}

Write-Host "Create"
$due = (Get-Date).AddDays(3).ToString("yyyy-MM-dd")
$created = Invoke-RestMethod -Uri "$BaseUrl/api/tasks" -Headers $headers -Method Post -ContentType "application/json" `
    -Body "{`"title`":`"Smoke test task`",`"description`":`"created by scripts/smoke.ps1`",`"dueDate`":`"$due`"}"
Assert-Equal "created status" "Todo" $created.status

Write-Host "Read"
$fetched = Invoke-RestMethod -Uri "$BaseUrl/api/tasks/$($created.id)" -Headers $headers -Method Get
Assert-Equal "read back" $created.id $fetched.id

Write-Host "Update"
$updated = Invoke-RestMethod -Uri "$BaseUrl/api/tasks/$($created.id)" -Headers $headers -Method Put -ContentType "application/json" `
    -Body "{`"title`":`"Smoke test task (edited)`",`"description`":null,`"dueDate`":`"$due`"}"
Assert-Equal "title updated" "Smoke test task (edited)" $updated.title

Write-Host "Status transition"
try {
    Invoke-RestMethod -Uri "$BaseUrl/api/tasks/$($created.id)/status" -Headers $headers -Method Patch -ContentType "application/json" `
        -Body '{"status":"Done"}' | Out-Null
    $moved = Invoke-RestMethod -Uri "$BaseUrl/api/tasks/$($created.id)/status" -Headers $headers -Method Patch -ContentType "application/json" `
        -Body '{"status":"Todo"}'
    Assert-Equal "backwards transition rejected" 400 200
}
catch {
    Assert-Equal "backwards transition rejected" 400 ([int]$_.Exception.Response.StatusCode)
}

Write-Host "Past due date is rejected"
$past = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd")
try {
    Invoke-RestMethod -Uri "$BaseUrl/api/tasks" -Headers $headers -Method Post -ContentType "application/json" `
        -Body "{`"title`":`"Should fail`",`"dueDate`":`"$past`"}" | Out-Null
    Assert-Equal "past due date" 400 201
}
catch {
    Assert-Equal "past due date" 400 ([int]$_.Exception.Response.StatusCode)
}

Write-Host "Delete"
$response = Invoke-WebRequest -Uri "$BaseUrl/api/tasks/$($created.id)" -Headers $headers -Method Delete -UseBasicParsing
Assert-Equal "delete" 204 $response.StatusCode

Write-Host ""
if ($script:failures -eq 0) {
    Write-Host "Smoke test passed." -ForegroundColor Green
    exit 0
}

Write-Host "$($script:failures) assertion(s) failed." -ForegroundColor Red
exit 1
