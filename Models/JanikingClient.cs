using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("janiking_clients")]
    public class JanikingClient
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(50)]
        [Column("customer_code")]
        public string CustomerCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("contract_billing", TypeName = "numeric(12, 2)")]
        public decimal ContractBilling { get; set; } = 0.00m;

        [MaxLength(50)]
        [Column("finders_fee_raw")]
        public string? FindersFeeRaw { get; set; }

        [MaxLength(7)]
        [Column("finders_fee_end_month")]
        public string? FindersFeeEndMonth { get; set; }

        [Column("finders_fee_amount", TypeName = "numeric(12, 2)")]
        public decimal FindersFeeAmount { get; set; } = 0.00m;

        [Column("actual_billing", TypeName = "numeric(12, 2)")]
        public decimal ActualBilling { get; set; } = 0.00m;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Required]
        [MaxLength(7)]
        [Column("last_sync_month")]
        public string LastSyncMonth { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
