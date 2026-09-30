using System;

namespace NHN.Power.API.DTOs.Site
{
    public class SiteResponse
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CreateSiteRequest
    {
        public Guid ClientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateSiteRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Notes { get; set; }
    }
}
