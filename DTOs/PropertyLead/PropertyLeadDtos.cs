using System;
using System.Collections.Generic;

namespace NHN.Power.API.DTOs.PropertyLead
{
    public class PropertyLeadDto
    {
        public Guid Id { get; set; }
        public string Address { get; set; } = string.Empty;
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public int? CarSpaces { get; set; }
        public string Status { get; set; } = "Under offer";
        public string? PriceGuide { get; set; }
        public bool IsAdvertised { get; set; }
        public DateTimeOffset? AdvertisedAt { get; set; }
        public string? RawOcrText { get; set; }
        public string? ImageR2Key { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class BatchUploadPropertyLeadItemDto
    {
        public string FileName { get; set; } = string.Empty;
        public string Base64Data { get; set; } = string.Empty;
    }

    public class BatchUploadPropertyLeadsRequestDto
    {
        public List<BatchUploadPropertyLeadItemDto> Images { get; set; } = new();
    }

    public class PropertyLeadProcessingItemResultDto
    {
        public string FileName { get; set; } = string.Empty;
        public string Action { get; set; } = "created"; // "created" | "updated" | "skipped" | "failed"
        public string? Address { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
        public PropertyLeadDto? Lead { get; set; }
    }

    public class BatchUploadPropertyLeadsResultDto
    {
        public int TotalImagesProcessed { get; set; }
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
        public List<PropertyLeadProcessingItemResultDto> Results { get; set; } = new();
    }

    public class ToggleAdvertisedRequestDto
    {
        public bool? IsAdvertised { get; set; }
    }
}
