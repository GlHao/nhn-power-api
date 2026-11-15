using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("task_photos")]
    public class TaskPhoto
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(20)]
        [Column("task_type")]
        public string TaskType { get; set; } = string.Empty; // "adhoc" | "regular"

        [Column("adhoc_task_id")]
        public Guid? AdhocTaskId { get; set; }

        [ForeignKey("AdhocTaskId")]
        public AdhocTask? AdhocTask { get; set; }

        [Column("site_id")]
        public Guid? SiteId { get; set; }

        [ForeignKey("SiteId")]
        public Site? Site { get; set; }

        [Column("service_date", TypeName = "date")]
        public DateTime ServiceDate { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("storage_type")]
        public string StorageType { get; set; } = "local"; // "local" | "r2"

        [Required]
        [Column("file_path")]
        public string FilePath { get; set; } = string.Empty;

        [Column("watermark_time")]
        public DateTimeOffset WatermarkTime { get; set; }

        [Required]
        [Column("watermark_address")]
        public string WatermarkAddress { get; set; } = string.Empty;

        [Column("is_sent")]
        public bool IsSent { get; set; } = false;

        [Column("sent_at")]
        public DateTimeOffset? SentAt { get; set; }

        [MaxLength(50)]
        [Column("sent_channel")]
        public string? SentChannel { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
