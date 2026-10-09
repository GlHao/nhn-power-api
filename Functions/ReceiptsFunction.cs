using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs.Receipt;
using NHN.Power.API.Models;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    public class UploadReceiptRequest
    {
        public string FileName { get; set; } = "receipt.jpg";
        public string ContentType { get; set; } = "image/jpeg";
        public string Base64Data { get; set; } = string.Empty;
    }

    public class ReceiptsFunction
    {
        private readonly IReceiptService _receiptService;
        private readonly ILogger<ReceiptsFunction> _logger;

        public ReceiptsFunction(IReceiptService receiptService, ILogger<ReceiptsFunction> logger)
        {
            _receiptService = receiptService;
            _logger = logger;
        }

        [Function("UploadReceipt")]
        public async Task<HttpResponseData> UploadReceipt(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "receipts/upload")] HttpRequestData req)
        {
            _logger.LogInformation("Processing receipt upload request.");

            try
            {
                Stream fileStream;
                string fileName = "receipt.jpg";
                string contentType = "image/jpeg";

                // Check if request is JSON or multipart form
                if (req.Headers.TryGetValues("Content-Type", out var contentTypes) && 
                    string.Join("", contentTypes).Contains("multipart/form-data"))
                {
                    // HttpMultipartParser or ReadAsStream
                    fileStream = req.Body;
                    // Attempt header filename if present
                    fileName = $"receipt_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg";
                }
                else
                {
                    var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                    var dto = JsonSerializer.Deserialize<UploadReceiptRequest>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (dto == null || string.IsNullOrWhiteSpace(dto.Base64Data))
                    {
                        var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Base64Data or file content is required."));
                        return badReq;
                    }

                    byte[] bytes = Convert.FromBase64String(dto.Base64Data);
                    fileStream = new MemoryStream(bytes);
                    fileName = dto.FileName;
                    contentType = dto.ContentType;
                }

                var responseDto = await _receiptService.UploadAndProcessReceiptAsync(fileStream, fileName, contentType);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<ReceiptResponseDto>.CreateSuccess(responseDto));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading and processing receipt");
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteAsJsonAsync(ApiResponse<object>.CreateError($"Failed to process receipt: {ex.Message}"));
                return errResponse;
            }
        }

        [Function("GetAllReceipts")]
        public async Task<HttpResponseData> GetAllReceipts(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "receipts")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all receipts.");

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string? status = query["status"];

            var receipts = await _receiptService.GetAllReceiptsAsync(status);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(receipts));
            return response;
        }

        [Function("GetReceiptById")]
        public async Task<HttpResponseData> GetReceiptById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "receipts/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var receiptId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Receipt ID format."));
                return badReq;
            }

            var receipt = await _receiptService.GetReceiptByIdAsync(receiptId);
            if (receipt == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Receipt not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<ReceiptResponseDto>.CreateSuccess(receipt));
            return response;
        }

        [Function("UpdateReceipt")]
        public async Task<HttpResponseData> UpdateReceipt(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "receipts/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var receiptId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Receipt ID format."));
                return badReq;
            }

            var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
            var dto = JsonSerializer.Deserialize<UpdateReceiptDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto == null)
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid request payload."));
                return badReq;
            }

            var updated = await _receiptService.UpdateReceiptAsync(receiptId, dto);
            if (updated == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Receipt not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<ReceiptResponseDto>.CreateSuccess(updated));
            return response;
        }

        [Function("DeleteReceipt")]
        public async Task<HttpResponseData> DeleteReceipt(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "receipts/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var receiptId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Receipt ID format."));
                return badReq;
            }

            bool deleted = await _receiptService.DeleteReceiptAsync(receiptId);
            if (!deleted)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Receipt not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<string>.CreateSuccess("Receipt deleted successfully."));
            return response;
        }
    }
}
