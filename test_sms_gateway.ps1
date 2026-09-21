# SMS Gateway Verification Script (MessageCore)
param(
    [string]$ApiKey = "",
    [string]$EndpointUrl = "https://api.messagecore.com/v1/sms/send",
    [string]$ToPhone = "",
    [string]$Message = "【Auntie Cleaning 测验】这是一条系统测试短信，收到说明 SMS 网关配置成功！"
)

if ([string]::IsNullOrEmpty($ApiKey) -or [string]::IsNullOrEmpty($ToPhone)) {
    Write-Host "Usage: .\test_sms_gateway.ps1 -ApiKey 'YOUR_API_KEY' -ToPhone '0400000000' [-EndpointUrl 'URL']" -ForegroundColor Yellow
    exit 1
}

Write-Host "Sending test SMS to $ToPhone via SMS Gateway..." -ForegroundColor Cyan

$body = @{
    to = $ToPhone
    message = $Message
} | ConvertTo-Json

try {
    $headers = @{
        "Authorization" = "Bearer $ApiKey"
        "Content-Type" = "application/json"
    }
    
    $response = Invoke-RestMethod -Uri $EndpointUrl -Method Post -Headers $headers -Body $body
    Write-Host "Success Response:" -ForegroundColor Green
    $response | ConvertTo-Json
} catch {
    Write-Host "Error sending SMS:" -ForegroundColor Red
    Write-Host $_.Exception.Message
}
