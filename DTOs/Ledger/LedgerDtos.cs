using System;
using System.Collections.Generic;

namespace NHN.Power.API.DTOs.Ledger
{
    public class LedgerQuarterSummaryDto
    {
        public Guid Id { get; set; }
        public string YearQuarter { get; set; } = string.Empty;
        public string Status { get; set; } = "draft"; // draft | completed
        public int TotalTransactions { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalGst { get; set; }
        public int UncategorizedCount { get; set; }
        public int RuleMatchedCount { get; set; }
        public int ReceiptMatchedCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class LedgerTransactionDto
    {
        public Guid Id { get; set; }
        public Guid QuarterId { get; set; }
        public string YearQuarter { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public decimal Amount { get; set; }
        public decimal GstAmount { get; set; }
        public string BankDescription { get; set; } = string.Empty;
        public decimal? Balance { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = "uncategorized"; // uncategorized | rule_matched | receipt_matched | completed
        public Guid? MatchedReceiptId { get; set; }
        public string? MatchedReceiptMerchant { get; set; }
        public string? MatchedReceiptUrl { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class UpdateLedgerTransactionDto
    {
        public string? Notes { get; set; }
        public decimal? GstAmount { get; set; }
        public string? Status { get; set; }
        public Guid? MatchedReceiptId { get; set; }
    }

    public class LedgerRuleDto
    {
        public Guid Id { get; set; }
        public string Keyword { get; set; } = string.Empty;
        public string CategoryNote { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CreateLedgerRuleDto
    {
        public string Keyword { get; set; } = string.Empty;
        public string CategoryNote { get; set; } = string.Empty;
    }

    public class ImportHistoryResultDto
    {
        public int TotalRowsParsed { get; set; }
        public int RulesLearned { get; set; }
        public int RulesUpdated { get; set; }
        public List<LedgerRuleDto> Rules { get; set; } = new();
    }

    public class ImportCsvResultDto
    {
        public string YearQuarter { get; set; } = string.Empty;
        public Guid QuarterId { get; set; }
        public int TotalTransactions { get; set; }
        public int RuleMatchedCount { get; set; }
        public int ReceiptMatchedCount { get; set; }
        public int UncategorizedCount { get; set; }
    }
}
