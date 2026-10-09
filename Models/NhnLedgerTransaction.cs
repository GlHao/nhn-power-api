using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("nhn_ledger_transactions")]
    public class NhnLedgerTransaction
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("quarter_id")]
        public Guid QuarterId { get; set; }

        [Column("transaction_date", TypeName = "date")]
        public DateTime TransactionDate { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("gst_amount")]
        public decimal GstAmount { get; set; }

        [Column("bank_description")]
        public string BankDescription { get; set; } = string.Empty;

        [Column("balance")]
        public decimal? Balance { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("status")]
        public string Status { get; set; } = "uncategorized"; // uncategorized | rule_matched | receipt_matched | completed

        [Column("matched_receipt_id")]
        public Guid? MatchedReceiptId { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [ForeignKey("QuarterId")]
        public NhnLedgerQuarter? Quarter { get; set; }

        [ForeignKey("MatchedReceiptId")]
        public NhnReceipt? MatchedReceipt { get; set; }
    }
}
