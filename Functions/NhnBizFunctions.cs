using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs.Client;
using NHN.Power.API.DTOs.Site;
using NHN.Power.API.DTOs.BillingRule;
using NHN.Power.API.DTOs.ScheduleRule;
using NHN.Power.API.DTOs.TaxInvoice;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    public class ClientsFunction
    {
        private readonly ILogger _logger;
        private readonly IClientService _clientService;

        public ClientsFunction(ILoggerFactory loggerFactory, IClientService clientService)
        {
            _logger = loggerFactory.CreateLogger<ClientsFunction>();
            _clientService = clientService;
        }

        [Function("GetClients")]
        public async Task<HttpResponseData> GetClients(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clients")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all clients");
            try
            {
                var clients = await _clientService.GetAllClientsAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(clients);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting clients: {Message}", ex.Message);
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteStringAsync($"Error: {ex.Message} | StackTrace: {ex.StackTrace}");
                return errResponse;
            }
        }


        [Function("GetClientById")]
        public async Task<HttpResponseData> GetClientById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clients/{id:guid}")] HttpRequestData req, Guid id)
        {
            var client = await _clientService.GetClientByIdAsync(id);
            if (client == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(client);
            return response;
        }

        [Function("CreateClient")]
        public async Task<HttpResponseData> CreateClient(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "clients")] HttpRequestData req)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<CreateClientRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data == null || string.IsNullOrWhiteSpace(data.Name))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Name is required" });
                return badRequest;
            }

            var created = await _clientService.CreateClientAsync(data);
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(created);
            return response;
        }

        [Function("UpdateClient")]
        public async Task<HttpResponseData> UpdateClient(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "clients/{id:guid}")] HttpRequestData req, Guid id)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<UpdateClientRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data == null || string.IsNullOrWhiteSpace(data.Name))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Name is required" });
                return badRequest;
            }

            var updated = await _clientService.UpdateClientAsync(id, data);
            if (updated == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(updated);
            return response;
        }

        [Function("DeleteClient")]
        public async Task<HttpResponseData> DeleteClient(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "clients/{id:guid}")] HttpRequestData req, Guid id)
        {
            var success = await _clientService.DeleteClientAsync(id);
            if (!success)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
    }

    public class SitesFunction
    {
        private readonly ILogger _logger;
        private readonly ISiteService _siteService;
        private readonly IClientService _clientService;

        public SitesFunction(ILoggerFactory loggerFactory, ISiteService siteService, IClientService clientService)
        {
            _logger = loggerFactory.CreateLogger<SitesFunction>();
            _siteService = siteService;
            _clientService = clientService;
        }

        [Function("GetSitesByClientId")]
        public async Task<HttpResponseData> GetSitesByClientId(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clients/{clientId:guid}/sites")] HttpRequestData req, Guid clientId)
        {
            var client = await _clientService.GetClientByIdAsync(clientId);
            if (client == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var sites = await _siteService.GetSitesByClientIdAsync(clientId);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(sites);
            return response;
        }

        [Function("GetSiteById")]
        public async Task<HttpResponseData> GetSiteById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sites/{id:guid}")] HttpRequestData req, Guid id)
        {
            var site = await _siteService.GetSiteByIdAsync(id);
            if (site == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(site);
            return response;
        }

        [Function("CreateSite")]
        public async Task<HttpResponseData> CreateSite(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sites")] HttpRequestData req)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<CreateSiteRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data == null || string.IsNullOrWhiteSpace(data.Name) || data.ClientId == Guid.Empty)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Name and ClientId are required" });
                return badRequest;
            }

            var created = await _siteService.CreateSiteAsync(data);
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(created);
            return response;
        }

        [Function("UpdateSite")]
        public async Task<HttpResponseData> UpdateSite(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "sites/{id:guid}")] HttpRequestData req, Guid id)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<UpdateSiteRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data == null || string.IsNullOrWhiteSpace(data.Name))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Name is required" });
                return badRequest;
            }

            var updated = await _siteService.UpdateSiteAsync(id, data);
            if (updated == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(updated);
            return response;
        }

        [Function("DeleteSite")]
        public async Task<HttpResponseData> DeleteSite(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "sites/{id:guid}")] HttpRequestData req, Guid id)
        {
            var success = await _siteService.DeleteSiteAsync(id);
            if (!success)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
    }

    public class BillingRulesFunction
    {
        private readonly ILogger _logger;
        private readonly IBillingRuleService _billingRuleService;
        private readonly ISiteService _siteService;

        public BillingRulesFunction(ILoggerFactory loggerFactory, IBillingRuleService billingRuleService, ISiteService siteService)
        {
            _logger = loggerFactory.CreateLogger<BillingRulesFunction>();
            _billingRuleService = billingRuleService;
            _siteService = siteService;
        }

        [Function("GetBillingRulesBySiteId")]
        public async Task<HttpResponseData> GetBillingRulesBySiteId(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sites/{siteId:guid}/billing-rules")] HttpRequestData req, Guid siteId)
        {
            var site = await _siteService.GetSiteByIdAsync(siteId);
            if (site == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var rules = await _billingRuleService.GetRulesBySiteIdAsync(siteId);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(rules);
            return response;
        }

        [Function("GetBillingRuleById")]
        public async Task<HttpResponseData> GetBillingRuleById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "billing-rules/{id:guid}")] HttpRequestData req, Guid id)
        {
            var rule = await _billingRuleService.GetRuleByIdAsync(id);
            if (rule == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(rule);
            return response;
        }

        [Function("CreateBillingRule")]
        public async Task<HttpResponseData> CreateBillingRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sites/{siteId:guid}/billing-rules")] HttpRequestData req, Guid siteId)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<CreateBillingRuleRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid request body" });
                return badRequest;
            }

            data.SiteId = siteId;

            if (data.DayOfWeek < 0 || data.DayOfWeek > 6)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "DayOfWeek must be between 0 (Sunday) and 6 (Saturday)" });
                return badRequest;
            }

            if (data.AmountExGst < 0 || data.PublicHolidayExtraExGst < 0)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Amounts cannot be negative" });
                return badRequest;
            }

            var site = await _siteService.GetSiteByIdAsync(siteId);
            if (site == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var created = await _billingRuleService.CreateRuleAsync(data);
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(created);
            return response;
        }

        [Function("UpdateBillingRule")]
        public async Task<HttpResponseData> UpdateBillingRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "billing-rules/{id:guid}")] HttpRequestData req, Guid id)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<UpdateBillingRuleRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid request body" });
                return badRequest;
            }

            if (data.DayOfWeek < 0 || data.DayOfWeek > 6)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "DayOfWeek must be between 0 (Sunday) and 6 (Saturday)" });
                return badRequest;
            }

            if (data.AmountExGst < 0 || data.PublicHolidayExtraExGst < 0)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Amounts cannot be negative" });
                return badRequest;
            }

            var updated = await _billingRuleService.UpdateRuleAsync(id, data);
            if (updated == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(updated);
            return response;
        }

        [Function("DeleteBillingRule")]
        public async Task<HttpResponseData> DeleteBillingRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "billing-rules/{id:guid}")] HttpRequestData req, Guid id)
        {
            var success = await _billingRuleService.DeleteRuleAsync(id);
            if (!success)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
    }

    public class ScheduleRulesFunction
    {
        private readonly ILogger _logger;
        private readonly IScheduleRuleService _scheduleRuleService;
        private readonly ISiteService _siteService;

        public ScheduleRulesFunction(ILoggerFactory loggerFactory, IScheduleRuleService scheduleRuleService, ISiteService siteService)
        {
            _logger = loggerFactory.CreateLogger<ScheduleRulesFunction>();
            _scheduleRuleService = scheduleRuleService;
            _siteService = siteService;
        }

        [Function("GetScheduleRulesBySiteId")]
        public async Task<HttpResponseData> GetScheduleRulesBySiteId(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sites/{siteId:guid}/schedule-rules")] HttpRequestData req, Guid siteId)
        {
            var site = await _siteService.GetSiteByIdAsync(siteId);
            if (site == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var rules = await _scheduleRuleService.GetRulesBySiteIdAsync(siteId);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(rules);
            return response;
        }

        [Function("GetScheduleRuleById")]
        public async Task<HttpResponseData> GetScheduleRuleById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "schedule-rules/{id:guid}")] HttpRequestData req, Guid id)
        {
            var rule = await _scheduleRuleService.GetRuleByIdAsync(id);
            if (rule == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(rule);
            return response;
        }

        [Function("CreateScheduleRule")]
        public async Task<HttpResponseData> CreateScheduleRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sites/{siteId:guid}/schedule-rules")] HttpRequestData req, Guid siteId)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<CreateScheduleRuleRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid request body" });
                return badRequest;
            }

            data.SiteId = siteId;

            if (data.FrequencyType != "weekly" && data.FrequencyType != "fortnightly" && data.FrequencyType != "monthly")
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "FrequencyType must be weekly, fortnightly, or monthly" });
                return badRequest;
            }

            var site = await _siteService.GetSiteByIdAsync(siteId);
            if (site == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var created = await _scheduleRuleService.CreateRuleAsync(data);
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(created);
            return response;
        }

        [Function("UpdateScheduleRule")]
        public async Task<HttpResponseData> UpdateScheduleRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "schedule-rules/{id:guid}")] HttpRequestData req, Guid id)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<UpdateScheduleRuleRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "Invalid request body" });
                return badRequest;
            }

            if (data.FrequencyType != "weekly" && data.FrequencyType != "fortnightly" && data.FrequencyType != "monthly")
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "FrequencyType must be weekly, fortnightly, or monthly" });
                return badRequest;
            }

            var updated = await _scheduleRuleService.UpdateRuleAsync(id, data);
            if (updated == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(updated);
            return response;
        }

        [Function("DeleteScheduleRule")]
        public async Task<HttpResponseData> DeleteScheduleRule(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "schedule-rules/{id:guid}")] HttpRequestData req, Guid id)
        {
            var success = await _scheduleRuleService.DeleteRuleAsync(id);
            if (!success)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
    }

    public class TaxInvoicesFunction
    {
        private readonly ILogger _logger;
        private readonly ITaxInvoicePreviewService _previewService;

        public TaxInvoicesFunction(ILoggerFactory loggerFactory, ITaxInvoicePreviewService previewService)
        {
            _logger = loggerFactory.CreateLogger<TaxInvoicesFunction>();
            _previewService = previewService;
        }

        [Function("GenerateTaxInvoicePreview")]
        public async Task<HttpResponseData> GeneratePreview(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tax-invoices/preview")] HttpRequestData req)
        {
            _logger.LogInformation("Generating tax invoice preview.");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var requestDto = JsonSerializer.Deserialize<TaxInvoicePreviewRequestDto>(requestBody, options);

                if (requestDto == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid request body.");
                    return badResponse;
                }

                var preview = await _previewService.GeneratePreviewAsync(requestDto);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(preview);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating preview: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync(ex.Message);
                return errorResponse;
            }
        }

        [Function("GetBusinesses")]
        public async Task<HttpResponseData> GetBusinesses(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tax-invoices/businesses")] HttpRequestData req)
        {
            _logger.LogInformation("Getting hardcoded businesses.");

            var businesses = new[]
            {
                new BusinessEntityDto
                {
                    Id = "business_1",
                    Name = "NHN POWER Pty Ltd",
                    Address = "Frankston, VIC 3199",
                    Abn = "66 659 323 010",
                    Mobile = "0451010588",
                    Email = "manager@nhnpower.com.au",
                    Bsb = "062-692",
                    AccountNumber = "7073 5308"
                },
                new BusinessEntityDto
                {
                    Id = "business_2",
                    Name = "N SHA  & H WANG",
                    Address = "Langwarrin, VIC 3910",
                    Abn = "75 441 866 318",
                    Mobile = "0451010588",
                    Email = "manager@nhnpower.com.au",
                    Bsb = "063-607",
                    AccountNumber = "1087 6647"
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(businesses);
            return response;
        }

        [Function("GenerateTaxInvoicePdf")]
        public async Task<HttpResponseData> GeneratePdf(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tax-invoices/pdf")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("TaxInvoicesFunction");
            logger.LogInformation("Generating tax invoice PDF.");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var requestDto = JsonSerializer.Deserialize<TaxInvoicePdfRequestDto>(requestBody, options);

                if (requestDto == null || requestDto.Preview == null || requestDto.Business == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid request body. Preview and Business are required.");
                    return badResponse;
                }

                var pdfService = executionContext.InstanceServices.GetRequiredService<ITaxInvoicePdfService>();
                var pdfBytes = pdfService.GeneratePdf(requestDto);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/pdf");
                response.Headers.Add("Content-Disposition", $"attachment; filename=TaxInvoice_{requestDto.Preview.ClientName.Replace(" ", "_")}.pdf");
                await response.Body.WriteAsync(pdfBytes, 0, pdfBytes.Length);
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error generating PDF: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync(ex.Message);
                return errorResponse;
            }
        }

        [Function("SendTaxInvoiceEmail")]
        public async Task<HttpResponseData> SendEmail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tax-invoices/send-email")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("TaxInvoicesFunction");
            logger.LogInformation("Sending Tax Invoice via Email.");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var requestDto = JsonSerializer.Deserialize<SendTaxInvoiceEmailRequestDto>(requestBody, options);

                if (requestDto == null || requestDto.PdfRequest == null || requestDto.PdfRequest.Preview == null || requestDto.PdfRequest.Business == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid request body. PdfRequest, Preview and Business are required.");
                    return badResponse;
                }

                var pdfService = executionContext.InstanceServices.GetRequiredService<ITaxInvoicePdfService>();
                var result = await pdfService.SendTaxInvoiceEmailAsync(requestDto);

                if (!result.Success)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { success = false, message = result.Message });
                    return badResponse;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, message = result.Message, r2Path = result.R2Path });
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error sending email: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { success = false, message = ex.Message });
                return errorResponse;
            }
        }
    }
}
