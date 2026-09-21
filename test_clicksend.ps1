# ClickSend Test Script
param(
    [string]$Username = "hao.wang.au@gmail.com",
    [string]$ApiKey = "E78F3450-5A5F-0198-D228-DA0C922191FF"
)

$pair = "$($Username):$($ApiKey)"
$bytes = [System.Text.Encoding]::ASCII.GetBytes($pair)
$base64 = [Convert]::ToBase64String($bytes)

$headers = @{
    "Authorization" = "Basic $base64"
    "Content-Type" = "application/json"
}

Write-Host "Checking ClickSend account balance & details..." -ForegroundColor Cyan

try {
    $res = Invoke-RestMethod -Uri "https://rest.clicksend.com/v3/account" -Method Get -Headers $headers
    Write-Host "ClickSend Account Verified Successfully!" -ForegroundColor Green
    $res | ConvertTo-Json -Depth 5
} catch {
    Write-Host "Failed to verify ClickSend account:" -ForegroundColor Red
    Write-Host $_.Exception.Message
}
