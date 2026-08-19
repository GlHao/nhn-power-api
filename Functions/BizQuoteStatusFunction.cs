using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs;
using NHN.Power.API.Models;
using NHN.Power.API.Repositories;

namespace NHN.Power.API.Functions
{
    [Authorize]
    public class BizQuoteStatusFunction
    {
        private readonly ILogger<BizQuoteStatusFunction> _logger;
        private readonly IQuoteRepository _quoteRepository;
        private static readonly string[] AllowedStatuses = { "new", "reviewing", "approved", "rejected", "closed" };

        public BizQuoteStatusFunction(
            ILogger<BizQuoteStatusFunction> logger,
            IQuoteRepository quoteRepository)
        {
            _logger = logger;
            _quoteRepository = quoteRepository;
        }

        [Function("BizQuoteStatus")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "biz/quotes/{id}/status")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation("Processing biz quote status update for ID {Id}.", id);

            if (!Guid.TryParse(id, out Guid quoteId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid quote ID format."));
                return badResponse;
            }

            QuoteUpdateStatusRequest? statusRequest;
            try
            {
                var requestBody = await new System.IO.StreamReader(req.Body).ReadToEndAsync();
                statusRequest = JsonSerializer.Deserialize<QuoteUpdateStatusRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid request body."));
                return badResponse;
            }

            if (statusRequest == null || string.IsNullOrWhiteSpace(statusRequest.Status))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Status is required."));
                return badResponse;
            }

            var newStatus = statusRequest.Status.ToLowerInvariant();
            if (!AllowedStatuses.Contains(newStatus))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Invalid status. Allowed values are: {string.Join(", ", AllowedStatuses)}"));
                return badResponse;
            }

            // Get existing quote to find the old status
            var existingQuote = await _quoteRepository.GetQuoteDetailAsync(quoteId);
            if (existingQuote == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Quote not found."));
                return notFoundResponse;
            }

            var oldStatus = existingQuote.Status;

            if (oldStatus == newStatus)
            {
                // No change needed
                var okResponse = req.CreateResponse(HttpStatusCode.OK);
                await okResponse.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(new object(), "Status is already up to date."));
                return okResponse;
            }

            var updated = await _quoteRepository.UpdateQuoteStatusAsync(quoteId, newStatus, oldStatus);

            if (!updated)
            {
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Failed to update status. It may have been updated concurrently."));
                return conflictResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(new object(), "Status updated successfully."));
            return response;
        }
    }
}
