using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("janiking_reports")]
    public class JanikingReport
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(7)]
        [Column("report_month")]
        public string ReportMonth { get; set; } = string.Empty;

        [Column("due_to_franchisee", TypeName = "numeric(12, 2)")]
        public decimal DueToFranchisee { get; set; } = 0.00m;

        [Column("charge_back_amount", TypeName = "numeric(12, 2)")]
        public decimal ChargeBackAmount { get; set; } = 0.00m;

        [Column("expected_amount", TypeName = "numeric(12, 2)")]
        public decimal ExpectedAmount { get; set; } = 0.00m;

        [Column("total_contract_billing", TypeName = "numeric(12, 2)")]
        public decimal TotalContractBilling { get; set; } = 0.00m;

        [Required]
        [Column("r2_key")]
        public string R2Key { get; set; } = string.Empty;

        [Column("r2_url")]
        public string? R2Url { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
