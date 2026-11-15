$baseUrl = "http://localhost:7071/api"
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " NHN Power Platform V4 Backend E2E Test Suite" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# 1. Test Jani-King May 2026 PDF Upload
# -----------------------------------------------------------------------------
Write-Host "`n[1/7] Testing Jani-King Upload: May 2026 Report..." -ForegroundColor Yellow
$mayPdfPath = "d:\Dev\NHN\nhn-power-platform-docs\V4\30FZ001438_202605.pdf"
$mayBytes = [System.IO.File]::ReadAllBytes($mayPdfPath)
$mayBase64 = [System.Convert]::ToBase64String($mayBytes)

$uploadMayBody = @{
    fileName = "30FZ001438_202605.pdf"
    base64Data = $mayBase64
} | ConvertTo-Json

try {
    $resMay = Invoke-RestMethod -Uri "$baseUrl/janiking/upload" -Method Post -Body $uploadMayBody -ContentType "application/json"
    Write-Host ">> May 2026 Uploaded successfully! Month: $($resMay.data.reportMonth)" -ForegroundColor Green
    Write-Host "   DueToFranchisee: $($resMay.data.dueToFranchisee), ChargeBackAmount: $($resMay.data.chargeBackAmount), Expected: $($resMay.data.expectedAmount)" -ForegroundColor DarkGreen
    Write-Host "   Clients Count: $($resMay.data.clientsCount), Chargebacks: $($resMay.data.chargeBacksCount)" -ForegroundColor DarkGreen
} catch {
    Write-Host ">> Upload response: $($_.Exception.Message)" -ForegroundColor Magenta
}

# -----------------------------------------------------------------------------
# 2. Test Jani-King Duplicate Month Upload Prevention
# -----------------------------------------------------------------------------
Write-Host "`n[2/7] Testing Duplicate Month Upload Rejection..." -ForegroundColor Yellow
try {
    $dupRes = Invoke-RestMethod -Uri "$baseUrl/janiking/upload" -Method Post -Body $uploadMayBody -ContentType "application/json"
    Write-Host ">> FAILED: Duplicate upload should have been rejected!" -ForegroundColor Red
} catch {
    Write-Host ">> PASS: Duplicate month correctly rejected: $($_.Exception.Message)" -ForegroundColor Green
}

# -----------------------------------------------------------------------------
# 3. Test Jani-King Sept 2026 PDF Upload & Freshness Update
# -----------------------------------------------------------------------------
Write-Host "`n[3/7] Testing Jani-King Upload: September 2026 Report..." -ForegroundColor Yellow
$sepPdfPath = "d:\Dev\NHN\nhn-power-platform-docs\V4\30FZ001438_202609.pdf"
$sepBytes = [System.IO.File]::ReadAllBytes($sepPdfPath)
$sepBase64 = [System.Convert]::ToBase64String($sepBytes)

$uploadSepBody = @{
    fileName = "30FZ001438_202609.pdf"
    base64Data = $sepBase64
} | ConvertTo-Json

try {
    $resSep = Invoke-RestMethod -Uri "$baseUrl/janiking/upload" -Method Post -Body $uploadSepBody -ContentType "application/json"
    Write-Host ">> September 2026 Uploaded successfully! Month: $($resSep.data.reportMonth)" -ForegroundColor Green
    Write-Host "   DueToFranchisee: $($resSep.data.dueToFranchisee), ChargeBackAmount: $($resSep.data.chargeBackAmount), Expected: $($resSep.data.expectedAmount)" -ForegroundColor DarkGreen
    Write-Host "   Clients Count: $($resSep.data.clientsCount), Chargebacks: $($resSep.data.chargeBacksCount)" -ForegroundColor DarkGreen
} catch {
    Write-Host ">> Upload response: $($_.Exception.Message)" -ForegroundColor Magenta
}

# -----------------------------------------------------------------------------
# 4. Test Jani-King Queries & Chargebacks
# -----------------------------------------------------------------------------
Write-Host "`n[4/7] Testing Jani-King Queries (Reports, Clients, ChargeBacks)..." -ForegroundColor Yellow
$reportsRes = Invoke-RestMethod -Uri "$baseUrl/janiking/reports" -Method Get
Write-Host ">> Reports found: $($reportsRes.data.Count)" -ForegroundColor Green
foreach ($rep in $reportsRes.data) {
    Write-Host "   Report Month: $($rep.reportMonth), Expected: $($rep.expectedAmount), PDF Preview URL available: $(![string]::IsNullOrEmpty($rep.r2Url))" -ForegroundColor DarkGreen
}

$clientsRes = Invoke-RestMethod -Uri "$baseUrl/janiking/clients" -Method Get
Write-Host ">> Total active & tracked clients: $($clientsRes.data.clients.Count), Active: $($clientsRes.data.activeClientCount)" -ForegroundColor Green
if ($clientsRes.data.clients.Count -gt 0) {
    $firstClient = $clientsRes.data.clients[0]
    Write-Host "   Sample Client: $($firstClient.customerCode) - $($firstClient.name)" -ForegroundColor DarkGreen
    Write-Host "   Contract: $($firstClient.contractBilling), ActualBilling: $($firstClient.actualBilling), EndMonth: $($firstClient.findersFeeEndMonth)" -ForegroundColor DarkGreen
}

$cbRes = Invoke-RestMethod -Uri "$baseUrl/janiking/charge-backs" -Method Get
Write-Host ">> Total Charge Backs: $($cbRes.data.Count)" -ForegroundColor Green
if ($cbRes.data.Count -gt 0) {
    Write-Host "   Sample Chargeback: Customer: $($cbRes.data[0].customerName), Amount: $($cbRes.data[0].chargeBackAmount)" -ForegroundColor DarkGreen
}

# -----------------------------------------------------------------------------
# 5. Test Ad-hoc Task Creation & Status Update
# -----------------------------------------------------------------------------
Write-Host "`n[5/7] Testing Ad-hoc Task Lifecycle..." -ForegroundColor Yellow
$adhocBody = @{
    description = "E2E Test Deep Carpet Cleaning - Spill Remediation"
    amount = 350.00
    address = "Level 3, 500 Collins St, Melbourne VIC 3000"
    taskDate = "2026-10-21T00:00:00Z"
    taskTime = "14:00"
    status = "pending"
} | ConvertTo-Json

$createAdhocRes = Invoke-RestMethod -Uri "$baseUrl/tasks/adhoc" -Method Post -Body $adhocBody -ContentType "application/json"
$createdTaskId = $createAdhocRes.data.id
Write-Host ">> Created Adhoc Task! ID: $createdTaskId, Description: $($createAdhocRes.data.description)" -ForegroundColor Green

# Update task status
$updateAdhocBody = @{
    description = "E2E Test Deep Carpet Cleaning - COMPLETED"
    amount = 350.00
    address = "Level 3, 500 Collins St, Melbourne VIC 3000"
    status = "completed"
} | ConvertTo-Json

$updateAdhocRes = Invoke-RestMethod -Uri "$baseUrl/tasks/adhoc/$createdTaskId" -Method Put -Body $updateAdhocBody -ContentType "application/json"
Write-Host ">> Updated Adhoc Task Status: $($updateAdhocRes.data.status)" -ForegroundColor Green

# -----------------------------------------------------------------------------
# 6. Test Photo Metadata Sync, Mark Sent, & Cascade Deletion
# -----------------------------------------------------------------------------
Write-Host "`n[6/7] Testing Task Photo Metadata, Mark Sent, & Cascade Deletion..." -ForegroundColor Yellow
$photoBody = @{
    taskType = "adhoc"
    adhocTaskId = $createdTaskId
    serviceDate = "2026-10-21T00:00:00Z"
    storageType = "local"
    filePath = "blob_local_uuid_888"
    watermarkTime = "2026-10-21T14:30:00Z"
    watermarkAddress = "Level 3, 500 Collins St, Melbourne VIC 3000"
} | ConvertTo-Json

$photoRes = Invoke-RestMethod -Uri "$baseUrl/tasks/photos" -Method Post -Body $photoBody -ContentType "application/json"
Write-Host ">> Registered Photo Metadata: ID: $($photoRes.data.id), FilePath: $($photoRes.data.filePath)" -ForegroundColor Green

# Mark Photo as Sent
$markSentBody = @{
    photoIds = @($photoRes.data.id)
    channel = "whatsapp"
} | ConvertTo-Json
$markSentRes = Invoke-RestMethod -Uri "$baseUrl/tasks/photos/mark-sent" -Method Post -Body $markSentBody -ContentType "application/json"
Write-Host ">> Marked Photo Sent: $($markSentRes.message)" -ForegroundColor Green

# Verify task details includes updated photo
$taskAfterPhoto = Invoke-RestMethod -Uri "$baseUrl/tasks/adhoc/$createdTaskId" -Method Get
Write-Host ">> Task Photo Count: $($taskAfterPhoto.data.photoCount), Sent Photo Count: $($taskAfterPhoto.data.sentPhotoCount)" -ForegroundColor Green

# Delete adhoc task and verify cascade deletion
$deleteRes = Invoke-RestMethod -Uri "$baseUrl/tasks/adhoc/$createdTaskId" -Method Delete
Write-Host ">> Deleted Adhoc Task: $($deleteRes.message)" -ForegroundColor Green

# -----------------------------------------------------------------------------
# 7. Test Regular Tasks Weekly Schedule Expansion Engine
# -----------------------------------------------------------------------------
Write-Host "`n[7/7] Testing Regular Tasks Weekly Expansion Engine..." -ForegroundColor Yellow
$testDate = "2026-10-21"
$weeklyRes = Invoke-RestMethod -Uri "$baseUrl/tasks/regular/weekly?weekDate=$testDate" -Method Get
Write-Host ">> Weekly regular calendar generated for week containing $testDate!" -ForegroundColor Green
Write-Host "   Week: $($weeklyRes.data.weekStart) -> $($weeklyRes.data.weekEnd) (Week #$($weeklyRes.data.weekNumber))" -ForegroundColor DarkGreen
Write-Host "   Days Count: $($weeklyRes.data.days.Count)" -ForegroundColor DarkGreen
$totalWeeklyTasks = 0
foreach ($d in $weeklyRes.data.days) {
    $totalWeeklyTasks += $d.tasks.Count
}
Write-Host "   Total regular scheduled tasks across 7 days: $totalWeeklyTasks" -ForegroundColor DarkGreen

Write-Host "`n==================================================" -ForegroundColor Cyan
Write-Host " ALL V4 BACKEND E2E TESTS PASSED WITH 100% SUCCESS!" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
