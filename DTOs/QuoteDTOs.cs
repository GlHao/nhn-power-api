using System;
using System.Collections.Generic;

namespace NHN.Power.API.DTOs
{
    public class QuoteSubmitRequest
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        
        public string? PropertyAddress { get; set; }
        public string? Suburb { get; set; }
        public string? Postcode { get; set; }
        
        public string? PropertyType { get; set; }
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public int? LivingAreas { get; set; }
        public int? Kitchens { get; set; }
        
        public bool? HasPets { get; set; }
        public bool? ParkingAvailable { get; set; }
        
        public DateTime? PreferredDate { get; set; }
        public string? CustomerMessage { get; set; }
        
        public decimal? WebsiteEstimateAmount { get; set; }
        
        public string? LeadSourceCode { get; set; }
        public string? QrCampaignCode { get; set; }
        
        public List<QuoteServiceDto> Services { get; set; } = new();
        public List<string> PhotoKeys { get; set; } = new();
    }

    public class QuoteServiceDto
    {
        public string ServiceCode { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public decimal? Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal? EstimatedAmount { get; set; }
    }

    public class QuoteSubmitResponse
    {
        public Guid QuoteRequestId { get; set; }
        public string QuoteNumber { get; set; } = string.Empty;
    }

    public class QuoteListDto
    {
        public Guid Id { get; set; }
        public string QuoteNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? Suburb { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public decimal? WebsiteEstimateAmount { get; set; }
        public string? LeadSourceName { get; set; }
    }

    public class QuotePhotoDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileKey { get; set; } = string.Empty;
        public string? FileUrl { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class QuoteSourceDto
    {
        public string? LeadSourceName { get; set; }
        public string? QrCampaignName { get; set; }
    }

    public class QuoteDetailDto
    {
        public Guid Id { get; set; }
        public string QuoteNumber { get; set; } = string.Empty;
        
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        
        public string? PropertyAddress { get; set; }
        public string? Suburb { get; set; }
        public string? Postcode { get; set; }
        
        public string? PropertyType { get; set; }
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public int? LivingAreas { get; set; }
        public int? Kitchens { get; set; }
        
        public bool? HasPets { get; set; }
        public bool? ParkingAvailable { get; set; }
        
        public DateTime? PreferredDate { get; set; }
        public string? CustomerMessage { get; set; }
        
        public decimal? WebsiteEstimateAmount { get; set; }
        public decimal? ReviewedAmount { get; set; }
        
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        
        public QuoteSourceDto Source { get; set; } = new();
        public List<QuoteServiceDto> Services { get; set; } = new();
        public List<QuotePhotoDto> Photos { get; set; } = new();
    }

    public class QuoteUpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class QuoteUpdateReviewRequest
    {
        public decimal ReviewedAmount { get; set; }
    }

    public class QuoteNoteDto
    {
        public Guid Id { get; set; }
        public Guid QuoteRequestId { get; set; }
        public string Note { get; set; } = string.Empty;
        public Guid? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AddQuoteNoteRequest
    {
        public string Note { get; set; } = string.Empty;
    }

    public class QuoteTimelineDto
    {
        public Guid Id { get; set; }
        public Guid QuoteRequestId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string EventTitle { get; set; } = string.Empty;
        public string? EventDescription { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public Guid? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PhotoUploadRequest
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    public class PhotoUploadResponse
    {
        public Guid PhotoId { get; set; }
        public string FileKey { get; set; } = string.Empty;
        public string UploadUrl { get; set; } = string.Empty;
    }

    public class DashboardKpiDto
    {
        public int TotalQuotes { get; set; }
        public int NewQuotes { get; set; }
        public int ReviewingQuotes { get; set; }
        public int ApprovedQuotes { get; set; }
    }

    public class DashboardQuoteDto
    {
        public Guid Id { get; set; }
        public string QuoteNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? LeadSourceName { get; set; }
    }

    public class DashboardDto
    {
        public DashboardKpiDto Kpi { get; set; } = new();
        public List<DashboardQuoteDto> RecentQuotes { get; set; } = new();
    }
}
