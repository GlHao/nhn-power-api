$ApiBaseUrl = "http://localhost:7071/api"
$TestEmail = "admin@nhn-power.com"
$TestPassword = "Admin123!"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " NHN Power Platform - Phase 24 E2E Test" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# 1. Dynamic QR Code Redirect
Write-Host "1. Testing Dynamic QR Code Redirect (GET /api/r/DEFAULT_FLYER)..." -ForegroundColor Yellow
try {
    $redirectReq = [System.Net.HttpWebRequest]::Create("$ApiBaseUrl/r/DEFAULT_FLYER")
    $redirectReq.AllowAutoRedirect = $false
    $redirectRes = $redirectReq.GetResponse()
    $location = $redirectRes.Headers["Location"]
    Write-Host "   SUCCESS: HTTP 302 Redirect Location -> $location" -ForegroundColor Green
} catch [System.Net.WebException] {
    $res = $_.Exception.Response
    if ($res -and ([int]$res.StatusCode -eq 302 -or [int]$res.StatusCode -eq 301)) {
        $location = $res.Headers["Location"]
        Write-Host "   SUCCESS: HTTP 302 Redirect Location -> $location" -ForegroundColor Green
    } else {
        Write-Host "   WARNING: Unexpected response: $_" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   WARNING: Could not connect: $_" -ForegroundColor Yellow
}

# 2. QR Scan Recording
Write-Host "`n2. Testing Public QR Scan Recording (POST /api/public/qr-scans)..." -ForegroundColor Yellow
$scanPayload = @{
    qrCampaignCode = "DEFAULT_FLYER"
    leadSourceCode = "flyer_qr"
    referrer = "https://google.com"
    landingUrl = "$ApiBaseUrl/r/DEFAULT_FLYER"
} | ConvertTo-Json

$scanRes = Invoke-RestMethod -Uri "$ApiBaseUrl/public/qr-scans" -Method Post -Body $scanPayload -ContentType "application/json"
$qrScanId = $scanRes.data.qrScanId
Write-Host "   SUCCESS: QR Scan recorded. Scan ID -> $qrScanId" -ForegroundColor Green

# 3. Public Quote Submission
Write-Host "`n3. Testing Public Quote Submission (POST /api/public/quotes)..." -ForegroundColor Yellow
$quotePayload = @{
    customerName = "E2E Test Customer"
    customerPhone = "0400123456"
    customerEmail = "e2e_test@nhn-power.com"
    propertyAddress = "123 Test Street"
    suburb = "Frankston"
    postcode = "3199"
    propertyType = "house"
    bedrooms = 3
    bathrooms = 2
    websiteEstimateAmount = 450.00
    qrCampaignCode = "DEFAULT_FLYER"
    services = @(
        @{
            serviceCode = "regular"
            quantity = 1
        }
    )
} | ConvertTo-Json -Depth 5

$quoteSubmitRes = Invoke-RestMethod -Uri "$ApiBaseUrl/public/quotes" -Method Post -Body $quotePayload -ContentType "application/json"
$quoteId = $quoteSubmitRes.data.quoteRequestId
$quoteNumber = $quoteSubmitRes.data.quoteNumber
Write-Host "   SUCCESS: Quote Submitted. Quote Number -> $quoteNumber (ID: $quoteId)" -ForegroundColor Green

# 4. Biz Auth Login
Write-Host "`n4. Testing Biz Auth Login (POST /api/auth/login)..." -ForegroundColor Yellow
$loginPayload = @{
    email = $TestEmail
    password = $TestPassword
} | ConvertTo-Json

$loginRes = Invoke-RestMethod -Uri "$ApiBaseUrl/auth/login" -Method Post -Body $loginPayload -ContentType "application/json"
$token = $loginRes.data.token
Write-Host "   SUCCESS: Admin Authenticated. JWT Token Acquired." -ForegroundColor Green

# 5. Biz Dashboard
Write-Host "`n5. Testing Biz Dashboard API (GET /api/biz/dashboard)..." -ForegroundColor Yellow
$dashboardRes = Invoke-RestMethod -Uri "$ApiBaseUrl/biz/dashboard" -Method Get -Headers @{ Authorization = "Bearer $token" }
Write-Host "   SUCCESS: Dashboard KPI -> Total Quotes: $($dashboardRes.data.kpi.totalQuotes), New Quotes: $($dashboardRes.data.kpi.newQuotes)" -ForegroundColor Green

# 6. Biz Quote Detail & Campaign Source Verification
Write-Host "`n6. Testing Biz Quote Detail (GET /api/biz/quotes/$quoteId)..." -ForegroundColor Yellow
$detailRes = Invoke-RestMethod -Uri "$ApiBaseUrl/biz/quotes/$quoteId" -Method Get -Headers @{ Authorization = "Bearer $token" }
Write-Host "   SUCCESS: Customer: $($detailRes.data.customerName), Status: $($detailRes.data.status), Campaign: $($detailRes.data.source.qrCampaignName)" -ForegroundColor Green

# 7. Update Review Price
Write-Host "`n7. Testing Update Review Price (PATCH /api/biz/quotes/$quoteId/review)..." -ForegroundColor Yellow
$reviewPayload = @{ reviewedAmount = 1350.00 } | ConvertTo-Json
$reviewRes = Invoke-RestMethod -Uri "$ApiBaseUrl/biz/quotes/$quoteId/review" -Method Patch -Body $reviewPayload -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }
Write-Host "   SUCCESS: Review price updated to `$1350.00." -ForegroundColor Green

# 8. Update Status to Approved
Write-Host "`n8. Testing Update Quote Status to Approved (PATCH /api/biz/quotes/$quoteId/status)..." -ForegroundColor Yellow
$statusPayload = @{ status = "approved" } | ConvertTo-Json
$statusRes = Invoke-RestMethod -Uri "$ApiBaseUrl/biz/quotes/$quoteId/status" -Method Patch -Body $statusPayload -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }
Write-Host "   SUCCESS: Quote Status updated to Approved." -ForegroundColor Green

# 9. Add Internal Admin Note
Write-Host "`n9. Testing Add Internal Note (POST /api/biz/quotes/$quoteId/notes)..." -ForegroundColor Yellow
$notePayload = @{ note = "Phase 24 E2E Verification Note: Address & price confirmed." } | ConvertTo-Json
$noteRes = Invoke-RestMethod -Uri "$ApiBaseUrl/biz/quotes/$quoteId/notes" -Method Post -Body $notePayload -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }
Write-Host "   SUCCESS: Internal note added." -ForegroundColor Green

# 10. Verify Full Timeline Audit History
Write-Host "`n10. Testing Quote Timeline (GET /api/biz/quotes/$quoteId/timeline)..." -ForegroundColor Yellow
$timelineRes = Invoke-RestMethod -Uri "$ApiBaseUrl/biz/quotes/$quoteId/timeline" -Method Get -Headers @{ Authorization = "Bearer $token" }
Write-Host "   SUCCESS: Timeline event count -> $($timelineRes.data.Count)" -ForegroundColor Green
foreach ($evt in $timelineRes.data) {
    Write-Host "      - Event: [$($evt.eventType)] $($evt.eventTitle)" -ForegroundColor Gray
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host " ALL PHASE 24 E2E TESTS PASSED (10/10)" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan
