using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("nhn_receipts")]
    public class NhnReceipt
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("merchant_name")]
        public string? MerchantName { get; set; }

        [Column("transaction_date", TypeName = "date")]
        public DateTime? TransactionDate { get; set; }

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("gst_amount")]
        public decimal GstAmount { get; set; }

        [Column("r2_key")]
        public string R2Key { get; set; } = string.Empty;

        [Column("r2_url")]
        public string? R2Url { get; set; }

        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("matched_transaction_id")]
        public Guid? MatchedTransactionId { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
