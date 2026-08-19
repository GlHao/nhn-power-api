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
    public class BizLeadSourcesFunction
    {
        private readonly ILogger<BizLeadSourcesFunction> _logger;
        private readonly ITrackingRepository _trackingRepository;

        public BizLeadSourcesFunction(
            ILogger<BizLeadSourcesFunction> logger,
            ITrackingRepository trackingRepository)
        {
            _logger = logger;
            _trackingRepository = trackingRepository;
        }

        [Function("BizLeadSourcesGet")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "biz/lead-sources")] HttpRequestData req)
        {
            _logger.LogInformation("Processing GET biz lead sources.");

            // Extract User ID from JWT to validate auth
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

            var leadSources = await _trackingRepository.GetActiveLeadSourcesAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(leadSources));
            return response;
        }
    }
}
