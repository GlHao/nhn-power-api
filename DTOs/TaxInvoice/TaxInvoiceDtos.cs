using System;
using System.Collections.Generic;

namespace NHN.Power.API.DTOs.TaxInvoice
{
    public class BusinessEntityDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Abn { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Bsb { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
    }

    public class TaxInvoicePreviewRequestDto
    {
        public Guid ClientId { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }

    public class TaxInvoicePreviewItemDto
    {
        public Guid? SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public DateTime ServiceDate { get; set; }
        public int DayOfWeek { get; set; }
        public string? Description { get; set; }
        public decimal BaseAmountExGst { get; set; }
        public decimal ExtraAmountExGst { get; set; }
        public decimal FinalAmountExGst { get; set; }
        public decimal GstAmount { get; set; }
        public decimal FinalAmountIncGst { get; set; }
        public string? Note { get; set; }
        public string Status { get; set; } = "normal";
    }

    public class TaxInvoicePreviewDto
    {
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string? ClientAbn { get; set; }
        public string? ClientEmail { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public List<TaxInvoicePreviewItemDto> Items { get; set; } = new();
        public decimal SubtotalExGst { get; set; }
        public decimal GstAmount { get; set; }
        public decimal TotalIncGst { get; set; }
    }

    public class TaxInvoicePdfRequestDto
    {
        public string InvoiceNo { get; set; } = string.Empty;
        public string IssueDate { get; set; } = string.Empty;
        public string DueDate { get; set; } = string.Empty;
        public TaxInvoicePreviewDto Preview { get; set; } = null!;
        public BusinessEntityDto Business { get; set; } = null!;
        public string PdfType { get; set; } = "Full"; // "Full", "Simple", or "DetailsOnly"
    }

    public class SendTaxInvoiceEmailRequestDto
    {
        public TaxInvoicePdfRequestDto PdfRequest { get; set; } = null!;
        public string? TargetEmails { get; set; } // 如果非空，优先使用此字段（支持逗号分隔）；如果为空，使用 Preview.ClientEmail
    }
}
