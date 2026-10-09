using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NHN.Power.API.DTOs.Receipt;

namespace NHN.Power.API.Services
{
    public interface IReceiptService
    {
        Task<ReceiptResponseDto> UploadAndProcessReceiptAsync(Stream fileStream, string fileName, string contentType);
        Task<IEnumerable<ReceiptResponseDto>> GetAllReceiptsAsync(string? status = null);
        Task<ReceiptResponseDto?> GetReceiptByIdAsync(Guid id);
        Task<ReceiptResponseDto?> UpdateReceiptAsync(Guid id, UpdateReceiptDto dto);
        Task<bool> DeleteReceiptAsync(Guid id);
    }
}
