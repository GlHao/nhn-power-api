using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("adhoc_tasks")]
    public class AdhocTask
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("amount", TypeName = "numeric(12, 2)")]
        public decimal Amount { get; set; } = 0.00m;

        [Required]
        [Column("address")]
        public string Address { get; set; } = string.Empty;

        [Column("task_date", TypeName = "date")]
        public DateTime TaskDate { get; set; }

        [MaxLength(50)]
        [Column("task_time")]
        public string? TaskTime { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("is_invoiced")]
        public bool IsInvoiced { get; set; } = false;

        [Column("invoiced_at")]
        public DateTimeOffset? InvoicedAt { get; set; }

        [Column("tax_invoice_id")]
        public Guid? TaxInvoiceId { get; set; }

        public ICollection<TaskPhoto> Photos { get; set; } = new List<TaskPhoto>();
    }
}
