using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NHN.Power.API.DTOs.Janiking;

namespace NHN.Power.API.Services
{
    public interface IJanikingService
    {
        Task<JanikingUploadResultDto> UploadAndProcessReportAsync(Stream fileStream, string fileName);
        Task<JanikingClientsSummaryDto> GetClientsSummaryAsync();
        Task<List<JanikingChargeBackDto>> GetChargeBacksAsync();
        Task<bool> DeleteChargeBackAsync(Guid id);
        Task<List<JanikingReportDto>> GetReportsAsync();
    }
}
