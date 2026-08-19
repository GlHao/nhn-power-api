using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs;
using NHN.Power.API.Models;
using NHN.Power.API.Repositories;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    public class PublicPhotoUploadFunction
    {
        private readonly ILogger<PublicPhotoUploadFunction> _logger;
        private readonly IQuoteRepository _quoteRepository;
        private readonly IStorageService _storageService;

        public PublicPhotoUploadFunction(
            ILogger<PublicPhotoUploadFunction> logger,
            IQuoteRepository quoteRepository,
            IStorageService storageService)
        {
            _logger = logger;
            _quoteRepository = quoteRepository;
            _storageService = storageService;
        }

        [Function("PublicPhotoUploadUrl")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "public/quote-photos/upload-url")] HttpRequestData req)
        {
            _logger.LogInformation("Processing public photo upload URL request.");

            PhotoUploadRequest? uploadRequest;
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                uploadRequest = JsonSerializer.Deserialize<PhotoUploadRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid request body."));
                return badResponse;
            }

            if (uploadRequest == null || string.IsNullOrWhiteSpace(uploadRequest.FileName) || string.IsNullOrWhiteSpace(uploadRequest.ContentType))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("FileName and ContentType are required."));
                return badResponse;
            }

            // Validate File Size (Max 8MB)
            const long maxFileSize = 8 * 1024 * 1024;
            if (uploadRequest.FileSize > maxFileSize)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("File size exceeds 8MB limit."));
                return badResponse;
            }

            // Validate Content Type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            var contentType = uploadRequest.ContentType.ToLower();
            if (!Array.Exists(allowedTypes, type => type == contentType))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid content type. Only jpeg, png, and webp are allowed."));
                return badResponse;
            }

            // Generate unique FileKey
            var extension = Path.GetExtension(uploadRequest.FileName);
            if (string.IsNullOrEmpty(extension))
            {
                // Fallback extension based on content type
                extension = contentType == "image/jpeg" ? ".jpg" : 
                            contentType == "image/png" ? ".png" : ".webp";
            }
            
            var fileKey = $"quotes/temp/{Guid.NewGuid()}{extension}";

            // Generate Presigned URL
            var uploadUrl = _storageService.GeneratePreSignedUploadUrl(fileKey, TimeSpan.FromMinutes(15), contentType);

            if (string.IsNullOrEmpty(uploadUrl))
            {
                var serverErrorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await serverErrorResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError("Failed to generate upload URL. Storage might not be configured."));
                return serverErrorResponse;
            }

            // Create Metadata Record
            var photoId = await _quoteRepository.CreateQuotePhotoMetadataAsync(
                uploadRequest.FileName, 
                fileKey, 
                contentType, 
                uploadRequest.FileSize);

            var responseData = new PhotoUploadResponse
            {
                PhotoId = photoId,
                FileKey = fileKey,
                UploadUrl = uploadUrl
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<PhotoUploadResponse>.CreateSuccess(responseData));
            return response;
        }
    }
}
