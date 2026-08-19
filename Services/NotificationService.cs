using System;
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

        public NotificationService(
            IOptions<AppSettings> appSettings,
            ILogger<NotificationService> logger,
            IQuoteRepository quoteRepository)
        {
            _appSettings = appSettings.Value;
            _logger = logger;
            _quoteRepository = quoteRepository;
        }

        public async Task SendQuoteNotificationsAsync(Guid quoteId, QuoteSubmitRequest request)
        {
            if (string.IsNullOrEmpty(_appSettings.SmtpHost) || _appSettings.SmtpPort == 0)
            {
                _logger.LogWarning("SMTP is not configured. Skipping email notifications.");
                await _quoteRepository.UpdateNotificationLogStatusAsync(quoteId, "failed", "SMTP not configured");
                return;
            }

            try
            {
                using var client = new SmtpClient(_appSettings.SmtpHost, _appSettings.SmtpPort)
                {
                    Credentials = new NetworkCredential(_appSettings.SmtpUsername, _appSettings.SmtpPassword),
                    EnableSsl = true
                };

                // Internal manager email
                var managerMessage = new MailMessage
                {
                    From = new MailAddress(_appSettings.SmtpFromEmail, "NHN Power Portal"),
                    Subject = "New Quote Request Received",
                    Body = $"A new quote request has been received from {request.CustomerName}.\nPhone: {request.CustomerPhone}\nEmail: {request.CustomerEmail}",
                    IsBodyHtml = false
                };
                managerMessage.To.Add(_appSettings.SmtpFromEmail); // Using from-email as manager email for MVP

                await client.SendMailAsync(managerMessage);

                // Optional Customer Email
                if (!string.IsNullOrEmpty(request.CustomerEmail))
                {
                    var customerMessage = new MailMessage
                    {
                        From = new MailAddress(_appSettings.SmtpFromEmail, "NHN Power Services"),
                        Subject = "Your Quote Request - NHN Power",
                        Body = $"Hi {request.CustomerName},\n\nWe have received your quote request and will get back to you shortly.\n\nThank you,\nNHN Power",
                        IsBodyHtml = false
                    };
                    customerMessage.To.Add(request.CustomerEmail);

                    await client.SendMailAsync(customerMessage);
                }

                await _quoteRepository.UpdateNotificationLogStatusAsync(quoteId, "sent");
                _logger.LogInformation("Successfully sent quote notifications for {QuoteId}", quoteId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email notifications for quote {QuoteId}", quoteId);
                await _quoteRepository.UpdateNotificationLogStatusAsync(quoteId, "failed", ex.Message);
            }
        }
    }
}
