using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("billing_rules")]
    public class BillingRule
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("site_id")]
        public Guid SiteId { get; set; }

        [ForeignKey("SiteId")]
        public Site Site { get; set; } = null!;

        [Required]
        [Column("day_of_week")]
        public int DayOfWeek { get; set; }

        [Required]
        [Column("amount_ex_gst", TypeName = "decimal(18,2)")]
        public decimal AmountExGst { get; set; } = 0;

        [Required]
        [Column("public_holiday_action")]
        [MaxLength(50)]
        public string PublicHolidayAction { get; set; } = "no_change";

        [Column("public_holiday_extra_ex_gst", TypeName = "decimal(18,2)")]
        public decimal PublicHolidayExtraExGst { get; set; } = 0;

        [Column("description")]
        public string? Description { get; set; }

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
