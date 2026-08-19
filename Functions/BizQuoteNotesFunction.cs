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
    public class BizQuoteNotesFunction
    {
        private readonly ILogger<BizQuoteNotesFunction> _logger;
        private readonly IQuoteRepository _quoteRepository;

        public BizQuoteNotesFunction(
            ILogger<BizQuoteNotesFunction> logger,
            IQuoteRepository quoteRepository)
        {
            _logger = logger;
            _quoteRepository = quoteRepository;
        }

        [Function("BizQuoteNotesGet")]
        public async Task<HttpResponseData> GetNotesAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "biz/quotes/{id}/notes")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation("Processing GET quote notes for ID {Id}.", id);

            if (!Guid.TryParse(id, out Guid quoteId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid quote ID format."));
                return badResponse;
            }

            // Optional: check JWT validity
            if (!ValidateJwt(req, out Guid _))
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            var notes = await _quoteRepository.GetQuoteNotesAsync(quoteId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(notes));
            return response;
        }

        [Function("BizQuoteNotesAdd")]
        public async Task<HttpResponseData> AddNoteAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "biz/quotes/{id}/notes")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation("Processing POST quote note for ID {Id}.", id);

            if (!Guid.TryParse(id, out Guid quoteId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid quote ID format."));
                return badResponse;
            }

            AddQuoteNoteRequest? addRequest;
            try
            {
                var requestBody = await new System.IO.StreamReader(req.Body).ReadToEndAsync();
                addRequest = JsonSerializer.Deserialize<AddQuoteNoteRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid request body."));
                return badResponse;
            }

            if (addRequest == null || string.IsNullOrWhiteSpace(addRequest.Note))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Note content is required."));
                return badResponse;
            }

            if (!ValidateJwt(req, out Guid adminId))
            {
                var unauthResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid user token."));
                return unauthResponse;
            }

            var added = await _quoteRepository.AddQuoteNoteAsync(quoteId, addRequest.Note, adminId);

            if (!added)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Quote not found."));
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(new object(), "Note added successfully."));
            return response;
        }

        private bool ValidateJwt(HttpRequestData req, out Guid userId)
        {
            userId = Guid.Empty;
            var authHeader = req.Headers.GetValues("Authorization").FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                return false;
            }

            var jwt = handler.ReadJwtToken(token);
            var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out userId))
            {
                return false;
            }

            return true;
        }
    }
}
