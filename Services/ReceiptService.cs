using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs.Receipt;
using NHN.Power.API.Models;
using NHN.Power.API.Repositories;

namespace NHN.Power.API.Services
{
    public class ReceiptService : IReceiptService
    {
        private readonly IReceiptRepository _receiptRepository;
        private readonly IStorageService _storageService;
        private readonly IAzureDocumentIntelligenceService _ocrService;
        private readonly ILogger<ReceiptService> _logger;

        public ReceiptService(
            IReceiptRepository receiptRepository,
            IStorageService storageService,
            IAzureDocumentIntelligenceService ocrService,
            ILogger<ReceiptService> logger)
        {
            _receiptRepository = receiptRepository;
            _storageService = storageService;
            _ocrService = ocrService;
            _logger = logger;
        }

        public async Task<ReceiptResponseDto> UploadAndProcessReceiptAsync(Stream fileStream, string fileName, string contentType)
        {
            // 1. Read stream to byte array
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            byte[] fileBytes = memoryStream.ToArray();

            // 2. Generate R2 key: receipts/2026/guid_filename
            string year = DateTime.UtcNow.Year.ToString();
            string fileExt = Path.GetExtension(fileName);
            string safeFileName = Path.GetFileNameWithoutExtension(fileName).Replace(" ", "_");
            string r2Key = $"receipts/{year}/{Guid.NewGuid()}_{safeFileName}{fileExt}";

            // 3. Upload to R2 via StorageService
            bool uploadSuccess = await _storageService.UploadFileAsync(r2Key, fileBytes, contentType);
            if (!uploadSuccess)
            {
                _logger.LogWarning("Failed to upload receipt file to Cloudflare R2 key: {r2Key}", r2Key);
            }

            // 4. Generate R2 pre-signed read URL or public URL
            string r2Url = _storageService.GeneratePreSignedReadUrl(r2Key, TimeSpan.FromDays(7));

            // 5. OCR Processing with Azure Document Intelligence
            var ocrResult = await _ocrService.AnalyzeReceiptAsync(fileBytes, contentType);

            // 6. Save to Database
            var entity = new NhnReceipt
            {
                Id = Guid.NewGuid(),
                MerchantName = ocrResult.MerchantName,
                TransactionDate = ocrResult.TransactionDate,
                TotalAmount = ocrResult.TotalAmount,
                GstAmount = ocrResult.GstAmount,
                R2Key = r2Key,
                R2Url = r2Url,
                Status = "pending",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _receiptRepository.AddAsync(entity);

            return MapToDto(entity);
        }

        public async Task<IEnumerable<ReceiptResponseDto>> GetAllReceiptsAsync(string? status = null)
        {
            var entities = await _receiptRepository.GetAllAsync(status);
            return entities.Select(MapToDto);
        }

        public async Task<ReceiptResponseDto?> GetReceiptByIdAsync(Guid id)
        {
            var entity = await _receiptRepository.GetByIdAsync(id);
            return entity == null ? null : MapToDto(entity);
        }

        public async Task<ReceiptResponseDto?> UpdateReceiptAsync(Guid id, UpdateReceiptDto dto)
        {
            var entity = await _receiptRepository.GetByIdAsync(id);
            if (entity == null) return null;

            if (dto.MerchantName != null) entity.MerchantName = dto.MerchantName;
            if (dto.TransactionDate != null) entity.TransactionDate = dto.TransactionDate;
            if (dto.TotalAmount.HasValue) entity.TotalAmount = dto.TotalAmount.Value;
            if (dto.GstAmount.HasValue) entity.GstAmount = dto.GstAmount.Value;
            if (dto.Status != null) entity.Status = dto.Status;

            await _receiptRepository.UpdateAsync(entity);
            return MapToDto(entity);
        }

        public async Task<bool> DeleteReceiptAsync(Guid id)
        {
            var entity = await _receiptRepository.GetByIdAsync(id);
            if (entity == null) return false;

            await _receiptRepository.DeleteAsync(id);
            return true;
        }

        private ReceiptResponseDto MapToDto(NhnReceipt entity)
        {
            // Refresh URL if expired or empty
            string? r2Url = entity.R2Url;
            if (string.IsNullOrEmpty(r2Url) && !string.IsNullOrEmpty(entity.R2Key))
            {
                r2Url = _storageService.GeneratePreSignedReadUrl(entity.R2Key, TimeSpan.FromDays(7));
            }

            return new ReceiptResponseDto
            {
                Id = entity.Id,
                MerchantName = entity.MerchantName,
                TransactionDate = entity.TransactionDate,
                TotalAmount = entity.TotalAmount,
                GstAmount = entity.GstAmount,
                R2Key = entity.R2Key,
                R2Url = r2Url,
                Status = entity.Status,
                MatchedTransactionId = entity.MatchedTransactionId,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
