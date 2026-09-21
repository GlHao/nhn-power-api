# E2E Test Script for Email & SMS Notifications
param(
    [string]$ApiBaseUrl = "http://localhost:7071/api"
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Testing Dual-Brand Email & SMS Notifications" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Auntie Cleaning Quote Test
Write-Host "`n1. Submitting Auntie Cleaning Quote Request..." -ForegroundColor Yellow
$auntiePayload = @{
    customerName = "Hao Wang (Auntie Cleaning Test)"
    customerPhone = "+61434383457"
    customerEmail = "hao.wang.au@gmail.com"
    propertyAddress = "88 Queensbridge St, Southbank VIC 3006"
    propertyType = "Apartment"
    bedrooms = 2
    bathrooms = 2
    customerMessage = "这是一条来自姨姨清洁网站的退房清洁询价测试！"
    leadSourceCode = "auntiecleaning_website"
    services = @(
        @{
            serviceCode = "end_of_lease"
            serviceName = "退房深层清洁 (End of Lease Cleaning)"
        }
    )
} | ConvertTo-Json -Depth 5

try {
    $res1 = Invoke-RestMethod -Uri "$ApiBaseUrl/public/quotes" -Method Post -Body $auntiePayload -ContentType "application/json"
    Write-Host "Auntie Cleaning Quote Submitted! Quote Number: $($res1.data.quoteNumber)" -ForegroundColor Green
} catch {
    Write-Host "Failed to submit Auntie Cleaning Quote:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

# 2. NHN Power Quote Test
Write-Host "`n2. Submitting NHN Power Quote Request..." -ForegroundColor Yellow
$nhnPayload = @{
    customerName = "Hao Wang (NHN Power Test)"
    customerPhone = "+61434383457"
    customerEmail = "hao.wang.au@gmail.com"
    propertyAddress = "120 Collins St, Melbourne VIC 3000"
    propertyType = "Commercial"
    customerMessage = "This is an E2E test quote request for NHN Power commercial cleaning."
    leadSourceCode = "nhn_power_website"
    services = @(
        @{
            serviceCode = "commercial_cleaning"
            serviceName = "Commercial Office Cleaning"
        }
    )
} | ConvertTo-Json -Depth 5

try {
    $res2 = Invoke-RestMethod -Uri "$ApiBaseUrl/public/quotes" -Method Post -Body $nhnPayload -ContentType "application/json"
    Write-Host "NHN Power Quote Submitted! Quote Number: $($res2.data.quoteNumber)" -ForegroundColor Green
} catch {
    Write-Host "Failed to submit NHN Power Quote:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host "`nE2E Quote Submission Complete!" -ForegroundColor Cyan
