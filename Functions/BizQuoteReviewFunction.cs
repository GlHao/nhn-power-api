using System;
using System.IdentityModel.Tokens.Jwt;
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
    public class BizQuoteReviewFunction
    {
        private readonly ILogger<BizQuoteReviewFunction> _logger;
        private readonly IQuoteRepository _quoteRepository;

        public BizQuoteReviewFunction(
            ILogger<BizQuoteReviewFunction> logger,
            IQuoteRepository quoteRepository)
        {
            _logger = logger;
            _quoteRepository = quoteRepository;
        }

        [Function("BizQuoteReview")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "biz/quotes/{id}/review")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation("Processing biz quote review update for ID {Id}.", id);

            if (!Guid.TryParse(id, out Guid quoteId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid quote ID format."));
                return badResponse;
            }

            QuoteUpdateReviewRequest? reviewRequest;
            try
            {
                var requestBody = await new System.IO.StreamReader(req.Body).ReadToEndAsync();
                reviewRequest = JsonSerializer.Deserialize<QuoteUpdateReviewRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid request body."));
                return badResponse;
            }

            if (reviewRequest == null || reviewRequest.ReviewedAmount < 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("ReviewedAmount is required and must be non-negative."));
                return badResponse;
            }

            // Extract User ID from JWT
            var authHeader = req.Headers.GetValues("Authorization").FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var unauthResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                return unauthResponse;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                var unauthResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                return unauthResponse;
            }

            var jwt = handler.ReadJwtToken(token);
            var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid adminId))
            {
                var unauthResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid user token."));
                return unauthResponse;
            }

            // Get existing quote to find the old reviewed amount
            var existingQuote = await _quoteRepository.GetQuoteDetailAsync(quoteId);
            if (existingQuote == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Quote not found."));
                return notFoundResponse;
            }

            var oldAmount = existingQuote.ReviewedAmount;

            var updated = await _quoteRepository.UpdateQuoteReviewedAmountAsync(quoteId, reviewRequest.ReviewedAmount, adminId, oldAmount);

            if (!updated)
            {
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Failed to update reviewed amount. It may have been updated concurrently."));
                return conflictResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(new object(), "Reviewed amount updated successfully."));
            return response;
        }
    }
}
