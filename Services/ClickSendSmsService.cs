using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NHN.Power.API.Models;

namespace NHN.Power.API.Services
{
    public class ClickSendSmsService : ISmsService
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _appSettings;
        private readonly ILogger<ClickSendSmsService> _logger;

        public ClickSendSmsService(
            HttpClient httpClient,
            IOptions<AppSettings> appSettings,
            ILogger<ClickSendSmsService> logger)
        {
            _httpClient = httpClient;
            _appSettings = appSettings.Value;
            _logger = logger;
        }

        public async Task<bool> SendSmsAsync(string toPhone, string messageText, string? senderId = null)
        {
            if (string.Equals(_appSettings.SmsProvider, "disabled", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("SMS sending skipped because SmsProvider is disabled.");
                return false;
            }

            if (string.IsNullOrEmpty(_appSettings.SmsUsername) || string.IsNullOrEmpty(_appSettings.SmsApiKey))
            {
                _logger.LogWarning("ClickSend SMS credentials (SmsUsername / SmsApiKey) are not configured.");
                return false;
            }

            var formattedPhone = FormatAustralianPhone(toPhone);

            var messageObj = new Dictionary<string, object>
            {
                { "source", "api" },
                { "to", formattedPhone },
                { "body", messageText }
            };

            if (!string.IsNullOrWhiteSpace(senderId))
            {
                messageObj["from"] = senderId;
            }

            var payload = new
            {
                messages = new[] { messageObj }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var requestContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var authenticationString = $"{_appSettings.SmsUsername}:{_appSettings.SmsApiKey}";
            var base64Auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(authenticationString));

            var request = new HttpRequestMessage(HttpMethod.Post, "https://rest.clicksend.com/v3/sms/send")
            {
                Content = requestContent
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);

            try
            {
                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully sent SMS via ClickSend to {Phone}. Response: {Response}", formattedPhone, responseBody);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to send SMS via ClickSend to {Phone}. Status: {StatusCode}, Error: {Response}", formattedPhone, response.StatusCode, responseBody);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending SMS via ClickSend to {Phone}", formattedPhone);
                return false;
            }
        }

        private static string FormatAustralianPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return phone;
            var cleanPhone = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

            if (cleanPhone.StartsWith("04") && cleanPhone.Length == 10)
            {
                return "+61" + cleanPhone.Substring(1);
            }
            if (cleanPhone.StartsWith("4") && cleanPhone.Length == 9)
            {
                return "+61" + cleanPhone;
            }
            if (!cleanPhone.StartsWith("+"))
            {
                return "+" + cleanPhone;
            }

            return cleanPhone;
        }
    }
}
