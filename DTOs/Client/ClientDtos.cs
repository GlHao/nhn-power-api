using System;

namespace NHN.Power.API.DTOs.Client
{
    public class ClientResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Abn { get; set; }
        public string? Email { get; set; }
        public Guid? BrandId { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CreateClientRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Abn { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateClientRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Abn { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
    }
}
