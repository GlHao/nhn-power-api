using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs.Janiking;
using NHN.Power.API.Models;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    public class UploadJanikingReportRequest
    {
        public string FileName { get; set; } = "statement.pdf";
        public string Base64Data { get; set; } = string.Empty;
    }

    public class JanikingFunction
    {
        private readonly IJanikingService _janikingService;
        private readonly ILogger<JanikingFunction> _logger;

        public JanikingFunction(IJanikingService janikingService, ILogger<JanikingFunction> logger)
        {
            _janikingService = janikingService;
            _logger = logger;
        }

        [Function("UploadJanikingReport")]
        public async Task<HttpResponseData> UploadJanikingReport(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "janiking/upload")] HttpRequestData req)
        {
            _logger.LogInformation("Processing Jani-King PDF report upload.");

            try
            {
                Stream fileStream;
                string fileName = "statement.pdf";

                if (req.Headers.TryGetValues("Content-Type", out var contentTypes) &&
                    string.Join("", contentTypes).Contains("multipart/form-data"))
                {
                    fileStream = req.Body;
                    fileName = $"janiking_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
                }
                else
                {
                    var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                    if (string.IsNullOrWhiteSpace(bodyText))
                    {
                        var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Request body cannot be empty."));
                        return badReq;
                    }

                    var dto = JsonSerializer.Deserialize<UploadJanikingReportRequest>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (dto == null || string.IsNullOrWhiteSpace(dto.Base64Data))
                    {
                        var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Base64Data is required."));
                        return badReq;
                    }

                    byte[] bytes = Convert.FromBase64String(dto.Base64Data);
                    fileStream = new MemoryStream(bytes);
                    fileName = string.IsNullOrWhiteSpace(dto.FileName) ? "statement.pdf" : dto.FileName;
                }

                var result = await _janikingService.UploadAndProcessReportAsync(fileStream, fileName);

                if (!result.Success)
                {
                    var conflictResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await conflictResponse.WriteAsJsonAsync(ApiResponse<JanikingUploadResultDto>.CreateError(result.Message));
                    return conflictResponse;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<JanikingUploadResultDto>.CreateSuccess(result, result.Message));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading and processing Jani-King report");
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to process Jani-King report: {ex.Message}"));
                return errResponse;
            }
        }

        [Function("GetJanikingClients")]
        public async Task<HttpResponseData> GetJanikingClients(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "janiking/clients")] HttpRequestData req)
        {
            _logger.LogInformation("Getting Jani-King clients and financial summary.");

            try
            {
                var summary = await _janikingService.GetClientsSummaryAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<JanikingClientsSummaryDto>.CreateSuccess(summary));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Jani-King clients summary");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return err;
            }
        }

        [Function("GetJanikingChargeBacks")]
        public async Task<HttpResponseData> GetJanikingChargeBacks(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "janiking/charge-backs")] HttpRequestData req)
        {
            _logger.LogInformation("Getting Jani-King charge backs.");

            try
            {
                var chargeBacks = await _janikingService.GetChargeBacksAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(chargeBacks));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Jani-King charge backs");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return err;
            }
        }

        [Function("DeleteJanikingChargeBack")]
        public async Task<HttpResponseData> DeleteJanikingChargeBack(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "janiking/charge-backs/{id}")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation("Deleting Jani-King charge back: {Id}", id);

            if (!Guid.TryParse(id, out var cbId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Charge Back ID format."));
                return badReq;
            }

            try
            {
                bool deleted = await _janikingService.DeleteChargeBackAsync(cbId);
                if (!deleted)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Charge back record not found."));
                    return notFound;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<bool>.CreateSuccess(true, "Charge back record deleted successfully."));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Jani-King charge back");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return err;
            }
        }

        [Function("GetJanikingReports")]
        public async Task<HttpResponseData> GetJanikingReports(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "janiking/reports")] HttpRequestData req)
        {
            _logger.LogInformation("Getting Jani-King monthly reports history.");

            try
            {
                var reports = await _janikingService.GetReportsAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(reports));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Jani-King reports");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return err;
            }
        }
    }
}
