using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using NHN.Power.API.Models;

namespace NHN.Power.API.Middleware
{
    public class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
    {
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during function execution.");

                var req = await context.GetHttpRequestDataAsync();
                if (req != null)
                {
                    var res = req.CreateResponse(HttpStatusCode.InternalServerError);
                    res.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    
                    var apiResponse = ApiResponse<object>.CreateError("An unexpected error occurred. Please try again later.");
                    var json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    
                    await res.WriteStringAsync(json);
                    
                    context.GetInvocationResult().Value = res;
                }
            }
        }
    }
}
