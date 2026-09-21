using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NHN.Power.API.DTOs;
using NHN.Power.API.Models;
using NHN.Power.API.Repositories;

namespace NHN.Power.API.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppSettings _appSettings;
        private readonly ILogger<NotificationService> _logger;
        private readonly IQuoteRepository _quoteRepository;
        private readonly ISmsService _smsService;

        public NotificationService(
            IOptions<AppSettings> appSettings,
            ILogger<NotificationService> logger,
            IQuoteRepository quoteRepository,
            ISmsService smsService)
        {
            _appSettings = appSettings.Value;
            _logger = logger;
            _quoteRepository = quoteRepository;
            _smsService = smsService;
        }

        public async Task SendQuoteNotificationsAsync(Guid quoteId, QuoteSubmitRequest request)
        {
            // 1. Send SMS if phone is provided and SMS provider is enabled
            if (!string.IsNullOrWhiteSpace(request.CustomerPhone) && 
                !string.Equals(_appSettings.SmsProvider, "disabled", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    bool isAuntie = string.Equals(request.LeadSourceCode, "auntiecleaning_website", StringComparison.OrdinalIgnoreCase);
                    
                    string smsBody = isAuntie
                        ? $"[Auntie Cleaning] Hi {request.CustomerName}, we received your quote request. Our team will contact you shortly!"
                        : $"[NHN Power] Hi {request.CustomerName}, we have received your quote request. Our team will contact you shortly!";

                    string senderId = isAuntie ? "AuntieClean" : "NHNPower";

                    bool smsSuccess = await _smsService.SendSmsAsync(request.CustomerPhone, smsBody, senderId);
                    
                    if (smsSuccess)
                    {
                        _logger.LogInformation("SMS notification sent successfully to {Phone} for quote {QuoteId}", request.CustomerPhone, quoteId);
                    }
                    else
                    {
                        _logger.LogWarning("SMS notification failed to send to {Phone} for quote {QuoteId}", request.CustomerPhone, quoteId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while sending SMS notification for quote {QuoteId}", quoteId);
                }
            }

            // 2. Send Email if SMTP is configured
            if (string.IsNullOrEmpty(_appSettings.SmtpHost) || _appSettings.SmtpPort == 0)
            {
                _logger.LogWarning("SMTP is not configured. Skipping email notifications.");
                return;
            }

            try
            {
                bool isAuntie = string.Equals(request.LeadSourceCode, "auntiecleaning_website", StringComparison.OrdinalIgnoreCase);
                string brandName = isAuntie ? "Auntie Cleaning" : "NHN Power";
                string brandColor = isAuntie ? "#e11d48" : "#2563eb"; // Rose red for Auntie Cleaning, Royal blue for NHN Power

                using var client = new SmtpClient(_appSettings.SmtpHost, _appSettings.SmtpPort)
                {
                    Credentials = new NetworkCredential(_appSettings.SmtpUsername, _appSettings.SmtpPassword),
                    EnableSsl = true
                };

                // Dynamic email templates & field mapping
                string summaryTitle = isAuntie ? "申请明细 Preview" : "Request Summary";
                string serviceLabel = isAuntie ? "服务项目" : "Service";
                string addressLabel = isAuntie ? "服务地址" : "Address";
                string urgentNotice = isAuntie ? "如需紧急咨询，欢迎直接回复此邮件或拨打客服电话。" : "If you have urgent questions, feel free to reply to this email directly.";

                string serviceSummary = (request.Services != null && request.Services.Count > 0)
                    ? string.Join(", ", request.Services.Select(s => string.IsNullOrEmpty(s.ServiceName) ? s.ServiceCode : s.ServiceName))
                    : "Standard Service";

                string propTypeRow = string.IsNullOrEmpty(request.PropertyType) ? "" : $"<tr><td style=\"padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-weight: bold;\">Property Type:</td><td style=\"padding: 8px 0; border-bottom: 1px solid #f1f5f9;\">{request.PropertyType}</td></tr>";
                string bedRow = request.Bedrooms.HasValue ? $"<tr><td style=\"padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-weight: bold;\">Bedrooms:</td><td style=\"padding: 8px 0; border-bottom: 1px solid #f1f5f9;\">{request.Bedrooms}</td></tr>" : "";
                string bathRow = request.Bathrooms.HasValue ? $"<tr><td style=\"padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-weight: bold;\">Bathrooms:</td><td style=\"padding: 8px 0; border-bottom: 1px solid #f1f5f9;\">{request.Bathrooms}</td></tr>" : "";
                string msgRow = string.IsNullOrEmpty(request.CustomerMessage) ? "" : $"<tr><td style=\"padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-weight: bold;\">Message:</td><td style=\"padding: 8px 0; border-bottom: 1px solid #f1f5f9;\">{request.CustomerMessage}</td></tr>";
                string estimateRow = request.WebsiteEstimateAmount.HasValue ? $"<tr><td style=\"padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-weight: bold;\">Estimate Amount:</td><td style=\"padding: 8px 0; border-bottom: 1px solid #f1f5f9;\">${request.WebsiteEstimateAmount.Value:F2}</td></tr>" : "";

                // Internal manager email
                string managerSubject = $"[{brandName}] New Quote Request - {request.CustomerName}";
                string managerBody = $@"
<div style=""font-family: Arial, 'Microsoft YaHei', sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;"">
    <div style=""background-color: {brandColor}; color: #ffffff; padding: 20px; text-align: center;"">
        <h2 style=""margin: 0; font-size: 20px;"">{brandName} - New Quote Request</h2>
    </div>
    <div style=""padding: 24px; color: #334155; line-height: 1.6;"">
        <p style=""font-size: 16px; margin-top: 0;"">A new quote request has been submitted from <strong>{request.LeadSourceCode ?? "Website"}</strong>.</p>
        <table style=""width: 100%; border-collapse: collapse; margin-top: 16px;"">
            <tr><td style=""padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-weight: bold; width: 140px;"">Customer Name:</td><td style=""padding: 8px 0; border-bottom: 1px solid #f1f5f9;"">{request.CustomerName}</td></tr>
            <tr><td style=""padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-weight: bold;"">Phone:</td><td style=""padding: 8px 0; border-bottom: 1px solid #f1f5f9;"">{request.CustomerPhone}</td></tr>
            <tr><td style=""padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-weight: bold;"">Email:</td><td style=""padding: 8px 0; border-bottom: 1px solid #f1f5f9;"">{request.CustomerEmail}</td></tr>
            <tr><td style=""padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-weight: bold;"">Services:</td><td style=""padding: 8px 0; border-bottom: 1px solid #f1f5f9;"">{serviceSummary}</td></tr>
            <tr><td style=""padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-weight: bold;"">Address:</td><td style=""padding: 8px 0; border-bottom: 1px solid #f1f5f9;"">{request.PropertyAddress}</td></tr>
            {propTypeRow}
            {bedRow}
            {bathRow}
            {msgRow}
            {estimateRow}
        </table>
    </div>
    <div style=""background-color: #f8fafc; padding: 12px 24px; text-align: center; color: #64748b; font-size: 12px;"">
        NHN Power Platform Automated System
    </div>
</div>";

                var managerMessage = new MailMessage
                {
                    From = new MailAddress(_appSettings.SmtpFromEmail, $"{brandName} Portal"),
                    Subject = managerSubject,
                    Body = managerBody,
                    IsBodyHtml = true
                };
                managerMessage.To.Add(_appSettings.SmtpFromEmail);

                await client.SendMailAsync(managerMessage);

                // Optional Customer Confirmation Email
                if (!string.IsNullOrEmpty(request.CustomerEmail))
                {
                    string customerSubject = isAuntie 
                        ? $"【姨姨清洁 Auntie Cleaning】已收到您的清洁报价申请！" 
                        : $"[{brandName}] Thank you! We received your quote request";

                    string customerGreeting = isAuntie
                        ? $"亲爱的 {request.CustomerName}："
                        : $"Hi {request.CustomerName},";

                    string customerIntro = isAuntie
                        ? "感谢您选择<b>姨姨清洁 Auntie Cleaning</b>！把温馨还给生活，把繁琐清洁交给姨姨。我们已收到您的询价申请，工作人员会尽快为您核算最优惠的细致清洁方案并与您联系。"
                        : $"Thank you for contacting <strong>{brandName}</strong>! We have received your quote request and our team is reviewing your requirements. We will contact you shortly with a personalized quote.";

                    string customerBody = $@"
<div style=""font-family: Arial, 'Microsoft YaHei', sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;"">
    <div style=""background-color: {brandColor}; color: #ffffff; padding: 24px; text-align: center;"">
        <h2 style=""margin: 0; font-size: 22px;"">{brandName}</h2>
    </div>
    <div style=""padding: 28px; color: #334155; line-height: 1.7;"">
        <p style=""font-size: 16px; font-weight: bold; margin-top: 0;"">{customerGreeting}</p>
        <p>{customerIntro}</p>
        <div style=""background-color: #f8fafc; border-left: 4px solid {brandColor}; padding: 16px; margin: 20px 0; border-radius: 4px;"">
            <h4 style=""margin: 0 0 8px 0; color: #1e293b;"">{summaryTitle}</h4>
            <ul style=""margin: 0; padding-left: 20px; color: #475569;"">
                <li>{serviceLabel}: {serviceSummary}</li>
                <li>{addressLabel}: {request.PropertyAddress}</li>
            </ul>
        </div>
        <p style=""color: #64748b; font-size: 14px;"">{urgentNotice}</p>
    </div>
    <div style=""background-color: #f1f5f9; padding: 16px 24px; text-align: center; color: #64748b; font-size: 13px;"">
        &copy; {DateTime.UtcNow.Year} {brandName}. All rights reserved.
    </div>
</div>";

                    var customerMessage = new MailMessage
                    {
                        From = new MailAddress(_appSettings.SmtpFromEmail, brandName),
                        Subject = customerSubject,
                        Body = customerBody,
                        IsBodyHtml = true
                    };
                    customerMessage.To.Add(request.CustomerEmail);

                    await client.SendMailAsync(customerMessage);
                }

                await _quoteRepository.UpdateNotificationLogStatusAsync(quoteId, "sent");
                _logger.LogInformation("Successfully sent dual-brand email notifications for quote {QuoteId}", quoteId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email notifications for quote {QuoteId}", quoteId);
                await _quoteRepository.UpdateNotificationLogStatusAsync(quoteId, "failed", ex.Message);
            }
        }
    }
}
