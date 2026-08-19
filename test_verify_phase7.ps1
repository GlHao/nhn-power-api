$ErrorActionPreference = 'Stop'

Write-Host "--- Testing Auth Login ---"
$loginBody = @{
    email = "admin@nhn-power.com"
    password = "Admin123!"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "http://localhost:7071/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginResponse.data.token
Write-Host "Login Success. Token acquired."

Write-Host "`n--- Getting Quote ID ---"
$headers = @{ Authorization = "Bearer $token" }
$listResponse = Invoke-RestMethod -Uri "http://localhost:7071/api/biz/quotes" -Method Get -Headers $headers
$firstQuoteId = $listResponse.data[0].id
$quoteNumber = $listResponse.data[0].quoteNumber
Write-Host "Found quote: $quoteNumber (ID: $firstQuoteId)"

Write-Host "`n--- Phase 7: Updating Status to reviewing ---"
$updateBody = @{ status = "reviewing" } | ConvertTo-Json
$updateResponse = Invoke-RestMethod -Uri "http://localhost:7071/api/biz/quotes/$firstQuoteId/status" -Method Patch -Body $updateBody -ContentType "application/json" -Headers $headers
Write-Host "Status Update Success: " $updateResponse.message

Write-Host "`n--- Phase 7: Updating Status to approved ---"
$updateBody2 = @{ status = "approved" } | ConvertTo-Json
$updateResponse2 = Invoke-RestMethod -Uri "http://localhost:7071/api/biz/quotes/$firstQuoteId/status" -Method Patch -Body $updateBody2 -ContentType "application/json" -Headers $headers
Write-Host "Status Update Success: " $updateResponse2.message

Write-Host "`nPhase 7 API Verified!"
