using System;
using System.Collections.Generic;
using NHN.Power.API.DTOs.TaxInvoice;

namespace NHN.Power.API.DTOs.Payroll
{
    public class EmployeePayrollPreviewRequestDto
    {
        public Guid EmployeeId { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }

    public class EmployeePayrollPreviewItemDto
    {
        public Guid SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public DateTime ServiceDate { get; set; }
        public int DayOfWeek { get; set; }
        public string? Description { get; set; }
        public decimal PayExGst { get; set; }
        public string? Note { get; set; }
    }

    public class EmployeePayrollPreviewDto
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string? EmployeePhone { get; set; }
        public string? EmployeeEmail { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public List<EmployeePayrollPreviewItemDto> Items { get; set; } = new();
        public int TotalServices { get; set; }
        public decimal TotalPayExGst { get; set; }
    }

    public class PaySlipPdfRequestDto
    {
        public string PaySlipNo { get; set; } = string.Empty;
        public string IssueDate { get; set; } = string.Empty;
        public EmployeePayrollPreviewDto Preview { get; set; } = null!;
        public BusinessEntityDto Business { get; set; } = null!;
    }

    public class SendPaySlipEmailRequestDto
    {
        public PaySlipPdfRequestDto PdfRequest { get; set; } = null!;
        public string? TargetEmails { get; set; }
    }

    public class SendTransferSmsRequestDto
    {
        public Guid EmployeeId { get; set; }
        public decimal Amount { get; set; }
        public string PeriodDescription { get; set; } = string.Empty;
        public string? CustomPhone { get; set; }
    }
}
