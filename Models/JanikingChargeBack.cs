using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("janiking_charge_backs")]
    public class JanikingChargeBack
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(7)]
        [Column("report_month")]
        public string ReportMonth { get; set; } = string.Empty;

        [MaxLength(50)]
        [Column("invoice_number")]
        public string? InvoiceNumber { get; set; }

        [Column("invoice_date", TypeName = "date")]
        public DateTime? InvoiceDate { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("customer_name")]
        public string CustomerName { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("total_amount", TypeName = "numeric(12, 2)")]
        public decimal TotalAmount { get; set; } = 0.00m;

        [Column("payments_amount", TypeName = "numeric(12, 2)")]
        public decimal PaymentsAmount { get; set; } = 0.00m;

        [Column("charge_back_amount", TypeName = "numeric(12, 2)")]
        public decimal ChargeBackAmount { get; set; } = 0.00m;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
