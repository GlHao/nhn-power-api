# iCloud SMTP Email Test Script
param(
    [string]$SmtpHost = "smtp.mail.me.com",
    [int]$SmtpPort = 587,
    [string]$Username = "nhn.studio@icloud.com",
    [string]$Password = "drum-fydq-dpnz-ibrn",
    [string]$FromEmail = "info@nhnpower.com.au",
    [string]$ToEmail = "hao.wang.au@gmail.com"
)

Write-Host "Connecting to iCloud SMTP (${SmtpHost}:${SmtpPort})..." -ForegroundColor Cyan
Write-Host "From: $FromEmail ($Username)" -ForegroundColor Cyan
Write-Host "To: $ToEmail" -ForegroundColor Cyan

try {
    $smtpClient = New-Object System.Net.Mail.SmtpClient($SmtpHost, $SmtpPort)
    $smtpClient.EnableSsl = $true
    $smtpClient.Credentials = New-Object System.Net.NetworkCredential($Username, $Password)

    $mailMessage = New-Object System.Net.Mail.MailMessage
    $mailMessage.From = New-Object System.Net.Mail.MailAddress($FromEmail, "NHN Power Platform Test")
    $mailMessage.To.Add($ToEmail)
    $mailMessage.Subject = "[NHN Power Test] iCloud+ Custom Domain SMTP Verification"
    $mailMessage.IsBodyHtml = $true
    $mailMessage.Body = @"
<div style="font-family: Arial, sans-serif; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;">
    <h2 style="color: #2563eb;">NHN Power Platform - Email Verification Success</h2>
    <p>Hi Hao,</p>
    <p>This is a test notification sent from <strong>info@nhnpower.com.au</strong> via iCloud+ Custom Domain SMTP.</p>
    <ul>
        <li><strong>SMTP Server:</strong> ${SmtpHost}:${SmtpPort}</li>
        <li><strong>Sender:</strong> $FromEmail</li>
        <li><strong>Timestamp:</strong> $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</li>
    </ul>
    <p style="color: #16a34a; font-weight: bold;">✓ iCloud+ Custom Domain Email SMTP integration is working perfectly!</p>
</div>
"@

    $smtpClient.Send($mailMessage)
    Write-Host "SUCCESS: Email delivered successfully to $ToEmail!" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Failed to send email via SMTP!" -ForegroundColor Red
    Write-Host $_.Exception.ToString() -ForegroundColor Red
}
