using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs.PropertyLead;
using NHN.Power.API.Models;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    public class PropertyLeadsFunction
    {
        private readonly IPropertyLeadService _leadService;
        private readonly ILogger<PropertyLeadsFunction> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public PropertyLeadsFunction(IPropertyLeadService leadService, ILogger<PropertyLeadsFunction> logger)
        {
            _leadService = leadService;
            _logger = logger;
        }

        [Function("GetPropertyLeads")]
        public async Task<HttpResponseData> GetPropertyLeads(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "property-leads")] HttpRequestData req)
        {
            _logger.LogInformation("Processing GetPropertyLeads request.");

            try
            {
                var query = HttpUtility.ParseQueryString(req.Url.Query);
                bool? isAdvertised = null;
                if (bool.TryParse(query["isAdvertised"], out var adv))
                {
                    isAdvertised = adv;
                }

                string? status = query["status"];

                var leads = await _leadService.GetPropertyLeadsAsync(isAdvertised, status);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(leads));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve property leads.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to retrieve property leads: {ex.Message}"));
                return errorResponse;
            }
        }

        [Function("BatchUploadPropertyLeads")]
        public async Task<HttpResponseData> BatchUploadPropertyLeads(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "property-leads/batch-upload")] HttpRequestData req)
        {
            _logger.LogInformation("Processing BatchUploadPropertyLeads request.");

            try
            {
                var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(bodyText))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Request body cannot be empty."));
                    return badReq;
                }

                var requestDto = JsonSerializer.Deserialize<BatchUploadPropertyLeadsRequestDto>(bodyText, JsonOptions);
                if (requestDto == null || requestDto.Images == null || !requestDto.Images.Any())
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Images array cannot be empty."));
                    return badReq;
                }

                var result = await _leadService.BatchUploadAndParseAsync(requestDto);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<BatchUploadPropertyLeadsResultDto>.CreateSuccess(
                    result,
                    $"Batch processed {result.TotalImagesProcessed} image(s): {result.CreatedCount} created, {result.UpdatedCount} updated, {result.SkippedCount} skipped, {result.FailedCount} failed."));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to batch upload and parse property leads.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to batch upload: {ex.Message}"));
                return errorResponse;
            }
        }

        [Function("ToggleAdvertisedPropertyLead")]
        public async Task<HttpResponseData> ToggleAdvertisedPropertyLead(
            [HttpTrigger(AuthorizationLevel.Anonymous, "patch", "put", Route = "property-leads/{id}/toggle-advertised")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation("Processing ToggleAdvertisedPropertyLead for ID {Id}.", id);

            if (!Guid.TryParse(id, out var leadId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid lead ID format."));
                return badReq;
            }

            try
            {
                bool? isAdvertised = null;
                var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    var toggleDto = JsonSerializer.Deserialize<ToggleAdvertisedRequestDto>(bodyText, JsonOptions);
                    isAdvertised = toggleDto?.IsAdvertised;
                }

                var updated = await _leadService.ToggleAdvertisedAsync(leadId, isAdvertised);
                if (updated == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Property lead with ID {id} not found."));
                    return notFound;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<PropertyLeadDto>.CreateSuccess(
                    updated,
                    updated.IsAdvertised ? "Marked as advertised." : "Unmarked advertisement."));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle advertised status.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to toggle advertised: {ex.Message}"));
                return errorResponse;
            }
        }

        [Function("DeletePropertyLead")]
        public async Task<HttpResponseData> DeletePropertyLead(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "property-leads/{id}")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation("Processing DeletePropertyLead for ID {Id}.", id);

            if (!Guid.TryParse(id, out var leadId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid lead ID format."));
                return badReq;
            }

            try
            {
                var success = await _leadService.DeletePropertyLeadAsync(leadId);
                if (!success)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Property lead with ID {id} not found."));
                    return notFound;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<bool>.CreateSuccess(true, "Property lead deleted successfully."));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete property lead.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to delete property lead: {ex.Message}"));
                return errorResponse;
            }
        }
    }
}
