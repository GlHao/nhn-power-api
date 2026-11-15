using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NHN.Power.API.DTOs.PropertyLead;

namespace NHN.Power.API.Services
{
    public interface IPropertyLeadService
    {
        Task<List<PropertyLeadDto>> GetPropertyLeadsAsync(bool? isAdvertised = null, string? status = null);
        Task<BatchUploadPropertyLeadsResultDto> BatchUploadAndParseAsync(BatchUploadPropertyLeadsRequestDto request);
        Task<PropertyLeadDto?> ToggleAdvertisedAsync(Guid id, bool? isAdvertised = null);
        Task<bool> DeletePropertyLeadAsync(Guid id);
    }
}
