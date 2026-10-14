using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs.Ledger;
using NHN.Power.API.Models;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    public class ImportBase64Request
    {
        public string FileName { get; set; } = "import.xlsx";
        public string Base64Data { get; set; } = string.Empty;
        public string? Business { get; set; } = "NHN POWER";
    }

    public class LedgerFunction
    {
        private readonly ILedgerService _ledgerService;
        private readonly ILogger<LedgerFunction> _logger;

        public LedgerFunction(ILedgerService ledgerService, ILogger<LedgerFunction> logger)
        {
            _ledgerService = ledgerService;
            _logger = logger;
        }

        [Function("GetLedgerBusinesses")]
        public async Task<HttpResponseData> GetBusinesses(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ledger/businesses")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all active businesses.");
            var businesses = await _ledgerService.GetActiveBusinessesAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(businesses));
            return response;
        }

        [Function("GetQuarters")]
        public async Task<HttpResponseData> GetQuarters(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ledger/quarters")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all ledger quarters summary.");
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string? business = query["business"];
            var summaries = await _ledgerService.GetQuartersSummaryAsync(business);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(summaries));
            return response;
        }

        [Function("GetQuarterTransactions")]
        public async Task<HttpResponseData> GetQuarterTransactions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ledger/quarters/{quarterId}")] HttpRequestData req,
            string quarterId)
        {
            if (!Guid.TryParse(quarterId, out var qId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Quarter ID format."));
                return badReq;
            }

            var txs = await _ledgerService.GetQuarterTransactionsAsync(qId);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(txs));
            return response;
        }

        [Function("ImportHistoricalExcel")]
        public async Task<HttpResponseData> ImportHistoricalExcel(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ledger/import-history")] HttpRequestData req)
        {
            _logger.LogInformation("Importing historical Excel for rule learning.");
            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string? business = query["business"];

                Stream fileStream;
                if (req.Headers.TryGetValues("Content-Type", out var contentTypes) && 
                    string.Join("", contentTypes).Contains("multipart/form-data"))
                {
                    fileStream = req.Body;
                }
                else
                {
                    var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                    var dto = JsonSerializer.Deserialize<ImportBase64Request>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (dto == null || string.IsNullOrWhiteSpace(dto.Base64Data))
                    {
                        var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Base64Data or file content is required."));
                        return badReq;
                    }
                    if (!string.IsNullOrWhiteSpace(dto.Business))
                    {
                        business = dto.Business;
                    }
                    byte[] bytes = Convert.FromBase64String(dto.Base64Data);
                    fileStream = new MemoryStream(bytes);
                }

                var result = await _ledgerService.ImportHistoricalExcelAsync(fileStream, business);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<ImportHistoryResultDto>.CreateSuccess(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import historical Excel.");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to import Excel: {ex.Message}"));
                return err;
            }
        }

        [Function("ImportBankCsv")]
        public async Task<HttpResponseData> ImportBankCsv(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ledger/import-csv")] HttpRequestData req)
        {
            _logger.LogInformation("Importing bank CSV file.");
            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string? business = query["business"];

                Stream fileStream;
                if (req.Headers.TryGetValues("Content-Type", out var contentTypes) && 
                    string.Join("", contentTypes).Contains("multipart/form-data"))
                {
                    fileStream = req.Body;
                }
                else
                {
                    var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                    var dto = JsonSerializer.Deserialize<ImportBase64Request>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (dto == null || string.IsNullOrWhiteSpace(dto.Base64Data))
                    {
                        var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Base64Data or file content is required."));
                        return badReq;
                    }
                    if (!string.IsNullOrWhiteSpace(dto.Business))
                    {
                        business = dto.Business;
                    }
                    byte[] bytes = Convert.FromBase64String(dto.Base64Data);
                    fileStream = new MemoryStream(bytes);
                }

                var result = await _ledgerService.ImportBankCsvAsync(fileStream, business);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<ImportCsvResultDto>.CreateSuccess(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import bank CSV.");
                var err = req.CreateResponse(HttpStatusCode.BadRequest);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to import CSV: {ex.Message}"));
                return err;
            }
        }

        [Function("AutoMatchReceipts")]
        public async Task<HttpResponseData> AutoMatchReceipts(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ledger/quarters/{quarterId}/auto-match")] HttpRequestData req,
            string quarterId)
        {
            if (!Guid.TryParse(quarterId, out var qId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Quarter ID format."));
                return badReq;
            }

            int count = await _ledgerService.AutoMatchReceiptsForQuarterAsync(qId);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(new { MatchedCount = count }));
            return response;
        }

        [Function("UpdateTransaction")]
        public async Task<HttpResponseData> UpdateTransaction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "ledger/transactions/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var txId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Transaction ID format."));
                return badReq;
            }

            var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
            var dto = JsonSerializer.Deserialize<UpdateLedgerTransactionDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto == null)
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid request payload."));
                return badReq;
            }

            var updated = await _ledgerService.UpdateTransactionAsync(txId, dto);
            if (updated == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Transaction not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<LedgerTransactionDto>.CreateSuccess(updated));
            return response;
        }

        [Function("DeleteTransaction")]
        public async Task<HttpResponseData> DeleteTransaction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "ledger/transactions/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var txId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Transaction ID format."));
                return badReq;
            }

            var success = await _ledgerService.DeleteTransactionAsync(txId);
            if (!success)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Transaction not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<bool>.CreateSuccess(true));
            return response;
        }

        [Function("FinalizeQuarter")]
        public async Task<HttpResponseData> FinalizeQuarter(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ledger/quarters/{quarterId}/finalize")] HttpRequestData req,
            string quarterId)
        {
            if (!Guid.TryParse(quarterId, out var qId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Quarter ID format."));
                return badReq;
            }

            bool success = await _ledgerService.FinalizeQuarterAsync(qId);
            if (!success)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Quarter not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<string>.CreateSuccess("Quarter finalized successfully."));
            return response;
        }

        [Function("DeleteQuarter")]
        public async Task<HttpResponseData> DeleteQuarter(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "ledger/quarters/{quarterId}")] HttpRequestData req,
            string quarterId)
        {
            if (!Guid.TryParse(quarterId, out var qId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Quarter ID format."));
                return badReq;
            }

            bool success = await _ledgerService.DeleteQuarterAsync(qId);
            if (!success)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Quarter not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<string>.CreateSuccess("Quarter deleted successfully."));
            return response;
        }

        [Function("ExportQuarterExcel")]
        public async Task<HttpResponseData> ExportQuarterExcel(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ledger/quarters/{quarterId}/export")] HttpRequestData req,
            string quarterId)
        {
            if (!Guid.TryParse(quarterId, out var qId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Quarter ID format."));
                return badReq;
            }

            byte[] fileBytes = await _ledgerService.ExportQuarterExcelAsync(qId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Headers.Add("Content-Disposition", $"attachment; filename=Ledger_{quarterId}.xlsx");
            await response.WriteBytesAsync(fileBytes);
            return response;
        }

        [Function("GetAllRules")]
        public async Task<HttpResponseData> GetAllRules(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ledger/rules")] HttpRequestData req)
        {
            var rules = await _ledgerService.GetAllRulesAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(rules));
            return response;
        }

        [Function("CreateOrUpdateRule")]
        public async Task<HttpResponseData> CreateOrUpdateRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ledger/rules")] HttpRequestData req)
        {
            var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
            var dto = JsonSerializer.Deserialize<CreateLedgerRuleDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto == null || string.IsNullOrWhiteSpace(dto.Keyword) || string.IsNullOrWhiteSpace(dto.CategoryNote))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Keyword and CategoryNote are required."));
                return badReq;
            }

            var rule = await _ledgerService.CreateOrUpdateRuleAsync(dto);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<LedgerRuleDto>.CreateSuccess(rule));
            return response;
        }

        [Function("UpdateRule")]
        public async Task<HttpResponseData> UpdateRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "ledger/rules/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var ruleId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Rule ID format."));
                return badReq;
            }

            var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
            var dto = JsonSerializer.Deserialize<CreateLedgerRuleDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto == null || string.IsNullOrWhiteSpace(dto.Keyword) || string.IsNullOrWhiteSpace(dto.CategoryNote))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Keyword and CategoryNote are required."));
                return badReq;
            }

            try
            {
                var rule = await _ledgerService.UpdateRuleAsync(ruleId, dto);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<LedgerRuleDto>.CreateSuccess(rule));
                return response;
            }
            catch (KeyNotFoundException ex)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return notFound;
            }
        }

        [Function("DeleteRule")]
        public async Task<HttpResponseData> DeleteRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "ledger/rules/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var ruleId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Rule ID format."));
                return badReq;
            }

            await _ledgerService.DeleteRuleAsync(ruleId);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<string>.CreateSuccess("Rule deleted successfully."));
            return response;
        }
    }
}
