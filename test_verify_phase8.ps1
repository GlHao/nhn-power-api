$ApiBaseUrl = "http://localhost:7071/api"
$TestEmail = "admin@nhn-power.com"
$TestPassword = "Admin123!"

# 1. Login
Write-Host "--- Testing Auth Login ---"
$loginPayload = @{
    email = $TestEmail
    password = $TestPassword
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/auth/login" -Method Post -Body $loginPayload -ContentType "application/json"
$token = $loginResponse.data.token
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Failed to get token!" -ForegroundColor Red
    exit 1
}
Write-Host "Login Success. Token acquired.`n" -ForegroundColor Green

# 2. Get the specific Quote by ID
Write-Host "--- Getting Quote ID ---"
$quoteListResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/biz/quotes?limit=1" -Method Get -Headers @{ Authorization = "Bearer $token" }
if ($quoteListResponse.data.Count -eq 0 -and $null -eq $quoteListResponse.data[0]) {
    Write-Host "No quotes found to test!" -ForegroundColor Red
    exit 1
}
$quoteId = $quoteListResponse.data[0].id
$quoteRef = $quoteListResponse.data[0].quoteNumber
Write-Host "Found quote: $quoteRef (ID: $quoteId)`n" -ForegroundColor Green

# 3. Update Review Amount
Write-Host "--- Phase 8: Updating Reviewed Amount to 1550.00 ---"
$reviewPayload = @{
    reviewedAmount = 1550.00
} | ConvertTo-Json

$reviewResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/biz/quotes/$quoteId/review" -Method Patch -Body $reviewPayload -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }
Write-Host "Review Update Success: " $reviewResponse.message -ForegroundColor Green

Write-Host "`nPhase 8 API Verified!" -ForegroundColor Cyan
