using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs;
using NHN.Power.API.Middleware;
using NHN.Power.API.Models;
using NHN.Power.API.Repositories;
using NHN.Power.API.Services;
using System.Linq;

namespace NHN.Power.API.Functions
{
    public class PublicQuoteSubmitFunction
    {
        private readonly ILogger<PublicQuoteSubmitFunction> _logger;
        private readonly IQuoteRepository _quoteRepository;
        private readonly IPricingValidationService _pricingValidationService;
        private readonly INotificationService _notificationService;

        public PublicQuoteSubmitFunction(
            ILogger<PublicQuoteSubmitFunction> logger,
            IQuoteRepository quoteRepository,
            IPricingValidationService pricingValidationService,
            INotificationService notificationService)
        {
            _logger = logger;
            _quoteRepository = quoteRepository;
            _pricingValidationService = pricingValidationService;
            _notificationService = notificationService;
        }

        [Function("PublicQuoteSubmit")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "public/quotes")] HttpRequestData req)
        {
            _logger.LogInformation("Processing public quote submission request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<QuoteSubmitRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.CustomerPhone))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid request. Customer name and phone are required."));
                return badRequestResponse;
            }

            // Calculate reviewed_amount using PricingValidationService
            // For MVP, we extract the primary service code (if any) and addons.
            var primaryService = request.Services?.FirstOrDefault();
            var serviceCode = primaryService?.ServiceCode ?? "";
            
            var addons = request.Services?
                .Where(s => s.ServiceCode != serviceCode)
                .Select(s => s.ServiceCode)
                .ToList() ?? new System.Collections.Generic.List<string>();

            var quoteInput = new QuoteRequestInput
            {
                ServiceCode = serviceCode,
                Bedrooms = request.Bedrooms ?? 1,
                Bathrooms = request.Bathrooms ?? 1,
                AddonCodes = addons
            };

            var reviewedAmount = _pricingValidationService.CalculateEstimate(quoteInput);

            // Execute database transaction
            var result = await _quoteRepository.SubmitQuoteAsync(request, reviewedAmount);

            // Trigger notification
            try
            {
                await _notificationService.SendQuoteNotificationsAsync(result.QuoteRequestId, request);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Notification service failed but quote was submitted successfully.");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<QuoteSubmitResponse>.CreateSuccess(result));
            return response;
        }
    }
}
