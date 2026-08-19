using System;

namespace NHN.Power.API.DTOs
{
    public class QrScanRequest
    {
        public string? QrCampaignCode { get; set; }
        public string? LeadSourceCode { get; set; }
        public string? Referrer { get; set; }
        public string? LandingUrl { get; set; }
    }

    public class QrScanResponse
    {
        public Guid QrScanId { get; set; }
    }

    public class LeadSourceDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class CreateQrScanRecord
    {
        public Guid? QrCampaignId { get; set; }
        public Guid? LeadSourceId { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Referrer { get; set; }
        public string? LandingUrl { get; set; }
    }

    public class QrCampaignTargetDto
    {
        public Guid Id { get; set; }
        public Guid? LeadSourceId { get; set; }
        public string TargetUrl { get; set; } = string.Empty;
    }
}
