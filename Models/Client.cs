using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("clients")]
    public class Client
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("abn")]
        public string? Abn { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("brand_id")]
        public Guid? BrandId { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("deleted_at")]
        public DateTimeOffset? DeletedAt { get; set; }
    }
}
