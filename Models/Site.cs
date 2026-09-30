using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("sites")]
    public class Site
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("client_id")]
        public Guid ClientId { get; set; }

        [ForeignKey("ClientId")]
        public Client Client { get; set; } = null!;

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("address")]
        public string? Address { get; set; }

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
