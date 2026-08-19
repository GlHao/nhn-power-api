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
    public class BizDashboardFunction
    {
        private readonly ILogger<BizDashboardFunction> _logger;
        private readonly IQuoteRepository _quoteRepository;

        public BizDashboardFunction(
            ILogger<BizDashboardFunction> logger,
            IQuoteRepository quoteRepository)
        {
            _logger = logger;
            _quoteRepository = quoteRepository;
        }

        [Function("BizDashboard")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "biz/dashboard")] HttpRequestData req)
        {
            _logger.LogInformation("Processing biz dashboard request.");

            var dashboardSummary = await _quoteRepository.GetDashboardSummaryAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<DashboardDto>.CreateSuccess(dashboardSummary));
            return response;
        }
    }
}
