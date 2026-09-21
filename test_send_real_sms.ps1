# ClickSend Real SMS Test Script (Bilingual Clear Format)
param(
    [string]$Username = "hao.wang.au@gmail.com",
    [string]$ApiKey = "E78F3450-5A5F-0198-D228-DA0C922191FF",
    [string]$ToPhone = "+61478575988",
    [string]$Message = "[Auntie Cleaning] Hi Hao, we received your quote request. Our team will contact you shortly! (Auntie Cleaning 收到您的报价申请)"
)

$pair = "$($Username):$($ApiKey)"
$bytes = [System.Text.Encoding]::ASCII.GetBytes($pair)
$base64 = [Convert]::ToBase64String($bytes)

$headers = @{
    "Authorization" = "Basic $base64"
    "Content-Type" = "application/json"
}

$body = @{
    messages = @(
        @{
            source = "api"
            from = "AuntieClean"
            body = $Message
            to = $ToPhone
        }
    )
} | ConvertTo-Json -Depth 5

Write-Host "Sending real SMS to $ToPhone via ClickSend API..." -ForegroundColor Cyan

try {
    $res = Invoke-RestMethod -Uri "https://rest.clicksend.com/v3/sms/send" -Method Post -Headers $headers -Body $body
    Write-Host "ClickSend SMS API Result:" -ForegroundColor Green
    $res | ConvertTo-Json -Depth 5
} catch {
    Write-Host "Failed to send SMS:" -ForegroundColor Red
    Write-Host $_.Exception.Message
}
