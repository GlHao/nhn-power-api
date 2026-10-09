using System;

namespace NHN.Power.API.DTOs.Receipt
{
    public class ReceiptResponseDto
    {
        public Guid Id { get; set; }
        public string? MerchantName { get; set; }
        public DateTime? TransactionDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal GstAmount { get; set; }
        public string R2Key { get; set; } = string.Empty;
        public string? R2Url { get; set; }
        public string Status { get; set; } = "pending";
        public Guid? MatchedTransactionId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class UpdateReceiptDto
    {
        public string? MerchantName { get; set; }
        public DateTime? TransactionDate { get; set; }
        public decimal? TotalAmount { get; set; }
        public decimal? GstAmount { get; set; }
        public string? Status { get; set; }
    }
}
