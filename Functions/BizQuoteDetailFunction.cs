using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs;
using NHN.Power.API.Models;
using NHN.Power.API.Repositories;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    [Authorize]
    public class BizQuoteDetailFunction
    {
        private readonly ILogger<BizQuoteDetailFunction> _logger;
        private readonly IQuoteRepository _quoteRepository;
        private readonly IStorageService _storageService;

        public BizQuoteDetailFunction(
            ILogger<BizQuoteDetailFunction> logger,
            IQuoteRepository quoteRepository,
            IStorageService storageService)
        {
            _logger = logger;
            _quoteRepository = quoteRepository;
            _storageService = storageService;
        }

        [Function("BizQuoteDetail")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "biz/quotes/{id}")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation("Processing biz quote detail request for ID {Id}.", id);

            if (!Guid.TryParse(id, out Guid quoteId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid quote ID format."));
                return badResponse;
            }

            var quoteDetail = await _quoteRepository.GetQuoteDetailAsync(quoteId);

            if (quoteDetail == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Quote not found."));
                return notFoundResponse;
            }

            // Generate signed URLs for photos
            if (quoteDetail.Photos != null)
            {
                foreach (var photo in quoteDetail.Photos)
                {
                    if (!string.IsNullOrEmpty(photo.FileKey))
                    {
                        try
                        {
                            photo.FileUrl = _storageService.GeneratePreSignedReadUrl(photo.FileKey, TimeSpan.FromHours(1));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to generate presigned URL for file key {FileKey}", photo.FileKey);
                        }
                    }
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<QuoteDetailDto>.CreateSuccess(quoteDetail));
            return response;
        }
    }
}
