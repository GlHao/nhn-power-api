using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
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
    public class BizQuoteTimelineFunction
    {
        private readonly ILogger<BizQuoteTimelineFunction> _logger;
        private readonly IQuoteRepository _quoteRepository;

        public BizQuoteTimelineFunction(
            ILogger<BizQuoteTimelineFunction> logger,
            IQuoteRepository quoteRepository)
        {
            _logger = logger;
            _quoteRepository = quoteRepository;
        }

        [Function("BizQuoteTimeline")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "biz/quotes/{id}/timeline")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation("Processing GET quote timeline for ID {Id}.", id);

            if (!Guid.TryParse(id, out Guid quoteId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid quote ID format."));
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

            var timeline = await _quoteRepository.GetQuoteTimelineAsync(quoteId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(timeline));
            return response;
        }
    }
}
