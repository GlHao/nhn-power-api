using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NHN.Power.API.DTOs;

namespace NHN.Power.API.Repositories
{
    public interface ITrackingRepository
    {
        Task<Guid?> GetLeadSourceByCodeAsync(string code);
        Task<Guid?> GetQrCampaignByCodeAsync(string campaignCode);
        Task<QrCampaignTargetDto?> GetCampaignTargetByCodeAsync(string campaignCode);
        Task<Guid> CreateQrScanAsync(CreateQrScanRecord record);
        Task<IEnumerable<LeadSourceDto>> GetActiveLeadSourcesAsync();
    }
}
