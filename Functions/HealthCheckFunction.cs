using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.Models;
using System.Text.Json;
using System.Threading.Tasks;

namespace NHN.Power.API.Functions
{
    public class HealthCheckFunction
    {
        private readonly ILogger _logger;

        public HealthCheckFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HealthCheckFunction>();
        }

        [Function("HealthCheck")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
        {
            _logger.LogInformation("Health check endpoint called.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            var apiResponse = ApiResponse<object>.CreateSuccess(new { status = "ok" });
            var json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            await response.WriteStringAsync(json);

            return response;
        }
    }
}
