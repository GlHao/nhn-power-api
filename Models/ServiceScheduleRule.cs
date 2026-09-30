using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("service_schedule_rules")]
    public class ServiceScheduleRule
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
        [Column("frequency_type")]
        [MaxLength(20)]
        public string FrequencyType { get; set; } = null!; // "weekly", "fortnightly", "monthly"

        [Column("days_of_week")]
        public List<int>? DaysOfWeek { get; set; } // For "weekly"

        [Column("day_of_week")]
        public int? DayOfWeek { get; set; } // For "fortnightly", "monthly"

        [Column("start_date")]
        public DateTimeOffset? StartDate { get; set; } // For "fortnightly"

        [Column("monthly_pattern")]
        [MaxLength(20)]
        public string? MonthlyPattern { get; set; } // "first", "last" for "monthly"

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
