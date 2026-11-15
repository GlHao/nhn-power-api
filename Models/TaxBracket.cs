using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("tax_brackets")]
    public class TaxBracket
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(20)]
        [Column("financial_year")]
        public string FinancialYear { get; set; } = string.Empty;

        [Column("threshold_min", TypeName = "numeric(12, 2)")]
        public decimal ThresholdMin { get; set; } = 0.00m;

        [Column("threshold_max", TypeName = "numeric(12, 2)")]
        public decimal? ThresholdMax { get; set; }

        [Column("base_tax", TypeName = "numeric(12, 2)")]
        public decimal BaseTax { get; set; } = 0.00m;

        [Column("tax_rate", TypeName = "numeric(6, 4)")]
        public decimal TaxRate { get; set; } = 0.0000m;

        [Column("medicare_rate", TypeName = "numeric(6, 4)")]
        public decimal MedicareRate { get; set; } = 0.0200m;

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
