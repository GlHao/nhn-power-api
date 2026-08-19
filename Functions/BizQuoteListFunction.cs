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
    public class BizQuoteListFunction
    {
        private readonly ILogger<BizQuoteListFunction> _logger;
        private readonly IQuoteRepository _quoteRepository;

        public BizQuoteListFunction(
            ILogger<BizQuoteListFunction> logger,
            IQuoteRepository quoteRepository)
        {
            _logger = logger;
            _quoteRepository = quoteRepository;
        }

        [Function("BizQuoteList")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "biz/quotes")] HttpRequestData req)
        {
            _logger.LogInformation("Processing biz quote list request.");

            var quotes = await _quoteRepository.GetQuotesAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<System.Collections.Generic.IEnumerable<QuoteListDto>>.CreateSuccess(quotes));
            return response;
        }
    }
}
