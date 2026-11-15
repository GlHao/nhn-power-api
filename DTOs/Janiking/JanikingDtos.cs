using System;
using System.Collections.Generic;

namespace NHN.Power.API.DTOs.Janiking
{
    public class JanikingClientDto
    {
        public Guid Id { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal ContractBilling { get; set; }
        public string? FindersFeeRaw { get; set; }
        public string? FindersFeeEndMonth { get; set; }
        public decimal FindersFeeAmount { get; set; }
        public decimal ActualBilling { get; set; }
        public bool IsActive { get; set; }
        public string LastSyncMonth { get; set; } = string.Empty;
    }

    public class JanikingClientsSummaryDto
    {
        public List<JanikingClientDto> Clients { get; set; } = new();
        public decimal TotalContractBilling { get; set; }
        public decimal TotalFindersFee { get; set; }
        public decimal TotalActualBilling { get; set; }
        public int ActiveClientCount { get; set; }
        public string? LatestReportMonth { get; set; }
    }

    public class JanikingChargeBackDto
    {
        public Guid Id { get; set; }
        public string ReportMonth { get; set; } = string.Empty;
        public string? InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaymentsAmount { get; set; }
        public decimal ChargeBackAmount { get; set; }
    }

    public class JanikingReportDto
    {
        public Guid Id { get; set; }
        public string ReportMonth { get; set; } = string.Empty;
        public decimal DueToFranchisee { get; set; }
        public decimal ChargeBackAmount { get; set; }
        public decimal ExpectedAmount { get; set; }
        public decimal TotalContractBilling { get; set; }
        public string R2Key { get; set; } = string.Empty;
        public string? R2Url { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class JanikingUploadResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ReportMonth { get; set; } = string.Empty;
        public decimal DueToFranchisee { get; set; }
        public decimal ChargeBackAmount { get; set; }
        public decimal ExpectedAmount { get; set; }
        public decimal TotalContractBilling { get; set; }
        public int ClientsCount { get; set; }
        public int ChargeBacksCount { get; set; }
        public bool IsNewerMonth { get; set; }
    }

    public class JanikingParsedData
    {
        public string ReportMonth { get; set; } = string.Empty; // "YYYY-MM"
        public decimal DueToFranchisee { get; set; }
        public decimal ChargeBackAmount { get; set; }
        public decimal ExpectedAmount { get; set; }
        public decimal TotalContractBilling { get; set; }
        public List<ParsedJanikingClient> Clients { get; set; } = new();
        public List<ParsedJanikingChargeBack> ChargeBacks { get; set; } = new();
    }

    public class ParsedJanikingClient
    {
        public string CustomerCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal ContractBilling { get; set; }
        public string? FindersFeeRaw { get; set; }
        public string? FindersFeeEndMonth { get; set; }
        public decimal FindersFeeAmount { get; set; }
        public decimal ActualBilling { get; set; }
    }

    public class ParsedJanikingChargeBack
    {
        public string ReportMonth { get; set; } = string.Empty;
        public string? InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaymentsAmount { get; set; }
        public decimal ChargeBackAmount { get; set; }
    }
}
