$ErrorActionPreference = 'Stop'

Write-Host "--- Phase 2: Testing Health Endpoint ---"
$healthResponse = Invoke-RestMethod -Uri "http://localhost:7071/api/health" -Method Get
Write-Host "Health Check Status: " $healthResponse.success

Write-Host "`n--- Phase 3: Testing Auth Login ---"
$loginBody = @{
    email = "admin@nhn-power.com"
    password = "Admin123!"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "http://localhost:7071/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginResponse.data.token
Write-Host "Login Success. Token acquired: " ($token.Substring(0, 20) + "...")

Write-Host "`n--- Phase 4: Testing Public Quote Submit ---"
$quoteBody = @{
    customerName = "John Doe"
    customerPhone = "0400000000"
    customerEmail = "john.doe@example.com"
    propertyAddress = "123 Test St"
    suburb = "Sydney"
    postcode = "2000"
    propertyType = "House"
    bedrooms = 3
    bathrooms = 2
    services = @(
        @{ serviceCode = "standard_clean"; serviceName = "Standard Clean" },
        @{ serviceCode = "oven_clean"; serviceName = "Oven Clean" }
    )
} | ConvertTo-Json -Depth 10

$quoteResponse = Invoke-RestMethod -Uri "http://localhost:7071/api/public/quotes" -Method Post -Body $quoteBody -ContentType "application/json"
Write-Host "Quote Submit Success. Quote Number: " $quoteResponse.data.quoteNumber

Write-Host "`n--- Phase 5: Testing Biz Quote List ---"
$headers = @{
    Authorization = "Bearer $token"
}
$listResponse = Invoke-RestMethod -Uri "http://localhost:7071/api/biz/quotes" -Method Get -Headers $headers
$quoteCount = $listResponse.data.Count
Write-Host "Biz Quote List Success. Found $quoteCount quotes."
$firstQuote = $listResponse.data[0]
Write-Host "First Quote Number: " $firstQuote.quoteNumber ", Customer: " $firstQuote.customerName ", Status: " $firstQuote.status

Write-Host "`nAll phases verified successfully!"
