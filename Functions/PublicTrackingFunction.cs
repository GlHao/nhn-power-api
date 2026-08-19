using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs;
using NHN.Power.API.Models;
using NHN.Power.API.Repositories;

namespace NHN.Power.API.Functions
{
    public class PublicTrackingFunction
    {
        private readonly ILogger<PublicTrackingFunction> _logger;
        private readonly ITrackingRepository _trackingRepository;

        public PublicTrackingFunction(
            ILogger<PublicTrackingFunction> logger,
            ITrackingRepository trackingRepository)
        {
            _logger = logger;
            _trackingRepository = trackingRepository;
        }

        [Function("PublicQrScan")]
        public async Task<HttpResponseData> CreateQrScanAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "public/qr-scans")] HttpRequestData req)
        {
            _logger.LogInformation("Processing public QR scan request.");

            QrScanRequest? scanRequest;
            try
            {
                var requestBody = await new System.IO.StreamReader(req.Body).ReadToEndAsync();
                scanRequest = JsonSerializer.Deserialize<QrScanRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid request body."));
                return badResponse;
            }

            if (scanRequest == null || string.IsNullOrWhiteSpace(scanRequest.QrCampaignCode))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("QrCampaignCode is required."));
                return badResponse;
            }

            var leadSourceCode = string.IsNullOrWhiteSpace(scanRequest.LeadSourceCode) ? "flyer_qr" : scanRequest.LeadSourceCode;

            // Resolve Campaign
            var campaignId = await _trackingRepository.GetQrCampaignByCodeAsync(scanRequest.QrCampaignCode);
            if (campaignId == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid QrCampaignCode."));
                return badResponse;
            }

            // Resolve Lead Source
            var leadSourceId = await _trackingRepository.GetLeadSourceByCodeAsync(leadSourceCode);
            if (leadSourceId == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid LeadSourceCode."));
                return badResponse;
            }

            // Extract IP Address
            string? ipAddress = null;
            if (req.Headers.TryGetValues("X-Forwarded-For", out var forwardedValues))
            {
                ipAddress = forwardedValues.FirstOrDefault()?.Split(',')[0].Trim();
            }
            if (string.IsNullOrEmpty(ipAddress) && req.Headers.TryGetValues("X-Real-IP", out var realIpValues))
            {
                ipAddress = realIpValues.FirstOrDefault();
            }

            // Extract User Agent
            string? userAgent = null;
            if (req.Headers.TryGetValues("User-Agent", out var uaValues))
            {
                userAgent = uaValues.FirstOrDefault();
            }

            var record = new CreateQrScanRecord
            {
                QrCampaignId = campaignId.Value,
                LeadSourceId = leadSourceId.Value,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Referrer = scanRequest.Referrer,
                LandingUrl = scanRequest.LandingUrl
            };

            var scanId = await _trackingRepository.CreateQrScanAsync(record);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<QrScanResponse>.CreateSuccess(new QrScanResponse { QrScanId = scanId }));
            return response;
        }

        [Function("PublicQrRedirect")]
        public async Task<HttpResponseData> RedirectQrCodeAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "r/{code}")] HttpRequestData req,
            string code)
        {
            _logger.LogInformation("Processing QR redirect for code: {Code}", code);

            try
            {
                var target = await _trackingRepository.GetCampaignTargetByCodeAsync(code);
                var redirectUrl = target?.TargetUrl ?? "https://nhnpower.com.au";

                if (target != null)
                {
                    // Asynchronously record scan
                    string? ipAddress = null;
                    if (req.Headers.TryGetValues("X-Forwarded-For", out var forwardedValues))
                        ipAddress = forwardedValues.FirstOrDefault()?.Split(',')[0].Trim();
                    else if (req.Headers.TryGetValues("X-Real-IP", out var realIpValues))
                        ipAddress = realIpValues.FirstOrDefault();

                    string? userAgent = null;
                    if (req.Headers.TryGetValues("User-Agent", out var uaValues))
                        userAgent = uaValues.FirstOrDefault();

                    var leadSourceId = await _trackingRepository.GetLeadSourceByCodeAsync("flyer_qr");

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _trackingRepository.CreateQrScanAsync(new CreateQrScanRecord
                            {
                                QrCampaignId = target.Id,
                                LeadSourceId = leadSourceId,
                                IpAddress = ipAddress,
                                UserAgent = userAgent,
                                LandingUrl = req.Url.ToString()
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to record QR scan during redirect for code {Code}", code);
                        }
                    });
                }

                var redirectResponse = req.CreateResponse(HttpStatusCode.Redirect);
                redirectResponse.Headers.Add("Location", redirectUrl);
                return redirectResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during QR redirect for code {Code}", code);
                var fallbackResponse = req.CreateResponse(HttpStatusCode.Redirect);
                fallbackResponse.Headers.Add("Location", "https://nhnpower.com.au");
                return fallbackResponse;
            }
        }
    }
}
