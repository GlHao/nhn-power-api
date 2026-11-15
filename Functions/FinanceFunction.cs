using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs.Finance;
using NHN.Power.API.Models;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    public class FinanceFunction
    {
        private readonly IFinanceService _financeService;
        private readonly ILogger<FinanceFunction> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public FinanceFunction(IFinanceService financeService, ILogger<FinanceFunction> logger)
        {
            _financeService = financeService;
            _logger = logger;
        }

        [Function("GetFinancialAnalytics")]
        public async Task<HttpResponseData> GetFinancialAnalytics(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "finance/analytics")] HttpRequestData req)
        {
            _logger.LogInformation("Processing GetFinancialAnalytics request.");

            try
            {
                var analytics = await _financeService.GetFinancialAnalyticsAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<FinancialAnalyticsDto>.CreateSuccess(analytics));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve financial analytics.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to retrieve financial analytics: {ex.Message}"));
                return errorResponse;
            }
        }

        [Function("CalculateTax")]
        public async Task<HttpResponseData> CalculateTax(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "finance/tax-calculate")] HttpRequestData req)
        {
            _logger.LogInformation("Processing CalculateTax request.");

            try
            {
                var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<TaxCalculateRequestDto>(bodyText, JsonOptions);
                if (dto == null)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Request body cannot be empty."));
                    return badReq;
                }

                var result = await _financeService.CalculateTaxAsync(dto);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<TaxCalculateResultDto>.CreateSuccess(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate tax.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to calculate tax: {ex.Message}"));
                return errorResponse;
            }
        }

        [Function("GetTaxBrackets")]
        public async Task<HttpResponseData> GetTaxBrackets(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "finance/tax-brackets")] HttpRequestData req)
        {
            _logger.LogInformation("Processing GetTaxBrackets request.");

            try
            {
                var query = HttpUtility.ParseQueryString(req.Url.Query);
                string? financialYear = query["financialYear"];

                var brackets = await _financeService.GetTaxBracketsAsync(financialYear);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(brackets));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get tax brackets.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to get tax brackets: {ex.Message}"));
                return errorResponse;
            }
        }

        [Function("CreateTaxBracket")]
        public async Task<HttpResponseData> CreateTaxBracket(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "finance/tax-brackets")] HttpRequestData req)
        {
            _logger.LogInformation("Processing CreateTaxBracket request.");

            try
            {
                var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<CreateTaxBracketDto>(bodyText, JsonOptions);
                if (dto == null)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Request body cannot be empty."));
                    return badReq;
                }

                var created = await _financeService.CreateTaxBracketAsync(dto);
                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(ApiResponse<TaxBracketDto>.CreateSuccess(created, "Tax bracket created successfully."));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create tax bracket.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to create tax bracket: {ex.Message}"));
                return errorResponse;
            }
        }

        [Function("UpdateTaxBracket")]
        public async Task<HttpResponseData> UpdateTaxBracket(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "finance/tax-brackets/{id}")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation("Processing UpdateTaxBracket request for ID {Id}.", id);

            if (!Guid.TryParse(id, out var bracketId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid bracket ID format."));
                return badReq;
            }

            try
            {
                var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<UpdateTaxBracketDto>(bodyText, JsonOptions);
                if (dto == null)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Request body cannot be empty."));
                    return badReq;
                }

                var updated = await _financeService.UpdateTaxBracketAsync(bracketId, dto);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<TaxBracketDto>.CreateSuccess(updated, "Tax bracket updated successfully."));
                return response;
            }
            catch (KeyNotFoundException)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Tax bracket with ID {id} not found."));
                return notFound;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update tax bracket.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to update tax bracket: {ex.Message}"));
                return errorResponse;
            }
        }

        [Function("DeleteTaxBracket")]
        public async Task<HttpResponseData> DeleteTaxBracket(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "finance/tax-brackets/{id}")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation("Processing DeleteTaxBracket request for ID {Id}.", id);

            if (!Guid.TryParse(id, out var bracketId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid bracket ID format."));
                return badReq;
            }

            try
            {
                var success = await _financeService.DeleteTaxBracketAsync(bracketId);
                if (!success)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Tax bracket with ID {id} not found."));
                    return notFound;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<bool>.CreateSuccess(true, "Tax bracket deleted successfully."));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete tax bracket.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to delete tax bracket: {ex.Message}"));
                return errorResponse;
            }
        }
    }
}
