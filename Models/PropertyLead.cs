using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("property_leads")]
    public class PropertyLead
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("address")]
        public string Address { get; set; } = string.Empty;

        [Column("bedrooms")]
        public int Bedrooms { get; set; } = 0;

        [Column("bathrooms")]
        public int Bathrooms { get; set; } = 0;

        [Column("car_spaces")]
        public int? CarSpaces { get; set; } = 0;

        [Required]
        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "Under offer";

        [Column("price_guide")]
        public string? PriceGuide { get; set; }

        [Column("is_advertised")]
        public bool IsAdvertised { get; set; } = false;

        [Column("advertised_at")]
        public DateTimeOffset? AdvertisedAt { get; set; }

        [Column("raw_ocr_text")]
        public string? RawOcrText { get; set; }

        [Column("image_r2_key")]
        public string? ImageR2Key { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
