using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NHN.Power.API.DTOs.TaxInvoice;
using NHN.Power.API.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NHN.Power.API.Services
{
    public class TaxInvoicePdfService : ITaxInvoicePdfService
    {
        private readonly AppSettings _appSettings;
        private readonly ILogger<TaxInvoicePdfService> _logger;
        private readonly IStorageService _storageService;

        public TaxInvoicePdfService(
            IOptions<AppSettings> appSettings,
            ILogger<TaxInvoicePdfService> logger,
            IStorageService storageService)
        {
            _appSettings = appSettings.Value;
            _logger = logger;
            _storageService = storageService;
        }

        public async Task<(bool Success, string Message, string? R2Path)> SendTaxInvoiceEmailAsync(SendTaxInvoiceEmailRequestDto request)
        {
            var pdfReq = request.PdfRequest;
            if (pdfReq == null || pdfReq.Preview == null || pdfReq.Business == null)
            {
                return (false, "Invalid request payload.", null);
            }

            // 1. 解析邮箱列表（以逗号或分号分割）
            string rawEmails = !string.IsNullOrWhiteSpace(request.TargetEmails) 
                ? request.TargetEmails 
                : (pdfReq.Preview.ClientEmail ?? string.Empty);

            var emailList = rawEmails
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e) && e.Contains("@"))
                .Distinct()
                .ToList();

            if (emailList.Count == 0)
            {
                return (false, "No valid email address provided.", null);
            }

            string primaryEmail = emailList[0];
            var ccEmails = emailList.Skip(1).ToList();

            // 2. 生成对应 PDF 字节数组
            byte[] pdfBytes = GeneratePdf(pdfReq);

            // 3. 准备 SMTP & 精美 HTML 邮件正文
            if (string.IsNullOrEmpty(_appSettings.SmtpHost) || _appSettings.SmtpPort == 0)
            {
                return (false, "SMTP server is not configured in settings.", null);
            }

            string invoiceNo = string.IsNullOrWhiteSpace(pdfReq.InvoiceNo) ? "INV-DRAFT" : pdfReq.InvoiceNo;
            string clientName = pdfReq.Preview.ClientName;
            string periodStart = pdfReq.Preview.PeriodStart.ToString("d MMM yyyy");
            string periodEnd = pdfReq.Preview.PeriodEnd.ToString("d MMM yyyy");
            string dueDate = pdfReq.DueDate;
            string totalAmount = $"${pdfReq.Preview.TotalIncGst:F2}";
            string brandName = pdfReq.Business.Name;

            string emailSubject = $"Tax Invoice {invoiceNo} from {brandName}";
            string emailBodyHtml = $@"
<div style=""font-family: Arial, sans-serif; max-width: 650px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; background-color: #ffffff;"">
    <div style=""background-color: #1e3a8a; color: #ffffff; padding: 24px; text-align: center;"">
        <h1 style=""margin: 0; font-size: 24px; letter-spacing: 0.5px;"">{brandName}</h1>
        <p style=""margin: 5px 0 0 0; opacity: 0.9; font-size: 14px;"">TAX INVOICE</p>
    </div>
    <div style=""padding: 28px; color: #334155; line-height: 1.6;"">
        <p style=""font-size: 16px; margin-top: 0;"">Dear <strong>{clientName}</strong>,</p>
        <p>Please find attached your Tax Invoice <strong>{invoiceNo}</strong> for the service period <strong>{periodStart} to {periodEnd}</strong>.</p>

        <div style=""background-color: #f8fafc; border-left: 4px solid #1e3a8a; padding: 18px; margin: 20px 0; border-radius: 4px;"">
            <table style=""width: 100%; border-collapse: collapse; font-size: 14px;"">
                <tr><td style=""padding: 4px 0; color: #64748b; width: 140px;"">Invoice Number:</td><td style=""padding: 4px 0; font-weight: bold; color: #1e293b;"">{invoiceNo}</td></tr>
                <tr><td style=""padding: 4px 0; color: #64748b;"">Service Period:</td><td style=""padding: 4px 0; font-weight: bold; color: #1e293b;"">{periodStart} - {periodEnd}</td></tr>
                <tr><td style=""padding: 4px 0; color: #64748b;"">Total Due (Inc GST):</td><td style=""padding: 4px 0; font-weight: bold; font-size: 16px; color: #dc2626;"">{totalAmount}</td></tr>
                <tr><td style=""padding: 4px 0; color: #64748b;"">Payment Due Date:</td><td style=""padding: 4px 0; font-weight: bold; color: #1e293b;"">{dueDate}</td></tr>
            </table>
        </div>

        <div style=""background-color: #eff6ff; border: 1px solid #bfdbfe; padding: 16px; border-radius: 6px; margin-bottom: 20px;"">
            <h4 style=""margin: 0 0 8px 0; color: #1e40af; font-size: 14px;"">Payment Methods (Bank Transfer)</h4>
            <p style=""margin: 0; font-size: 13px; color: #1e3a8a;"">
                <strong>Bank Name:</strong> Commonwealth Bank<br/>
                <strong>BSB:</strong> {pdfReq.Business.Bsb}<br/>
                <strong>Account No:</strong> {pdfReq.Business.AccountNumber}<br/>
                <strong>Reference:</strong> {invoiceNo}
            </p>
        </div>

        <p style=""font-size: 13px; color: #64748b;"">If you have any questions regarding this invoice, please feel free to contact us at {pdfReq.Business.Email} or {pdfReq.Business.Mobile}.</p>
    </div>
    <div style=""background-color: #f1f5f9; padding: 16px 24px; text-align: center; color: #64748b; font-size: 12px;"">
        &copy; {DateTime.UtcNow.Year} {brandName}. All rights reserved.
    </div>
</div>";

            try
            {
                using var client = new SmtpClient(_appSettings.SmtpHost, _appSettings.SmtpPort)
                {
                    Credentials = new NetworkCredential(_appSettings.SmtpUsername, _appSettings.SmtpPassword),
                    EnableSsl = true
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(_appSettings.SmtpFromEmail, brandName),
                    Subject = emailSubject,
                    Body = emailBodyHtml,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(primaryEmail);
                foreach (var cc in ccEmails)
                {
                    mailMessage.CC.Add(cc);
                }

                // 关联 PDF 附件
                string pdfFileName = $"TaxInvoice_{invoiceNo}_{pdfReq.PdfType}.pdf";
                using var pdfStream = new MemoryStream(pdfBytes);
                mailMessage.Attachments.Add(new Attachment(pdfStream, pdfFileName, "application/pdf"));

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Successfully sent Tax Invoice {InvoiceNo} to {PrimaryEmail} (CC: {CcCount})", invoiceNo, primaryEmail, ccEmails.Count);

                // 4. 发送成功后，备份上传至 Cloudflare R2
                string safeClientName = string.Concat(clientName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                string yearStr = DateTime.UtcNow.Year.ToString();
                string r2Key = $"invoices/{yearStr}/{safeClientName}/{invoiceNo}_{pdfReq.PdfType}.pdf";

                bool uploaded = await _storageService.UploadFileAsync(r2Key, pdfBytes, "application/pdf");
                if (uploaded)
                {
                    _logger.LogInformation("Successfully backed up invoice {InvoiceNo} to Cloudflare R2 at {Key}", invoiceNo, r2Key);
                }
                else
                {
                    _logger.LogWarning("Failed to backup invoice {InvoiceNo} to R2 bucket", invoiceNo);
                }

                return (true, $"Invoice sent successfully to {primaryEmail}", r2Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email for invoice {InvoiceNo}", invoiceNo);
                return (false, $"Failed to send email: {ex.Message}", null);
            }
        }
        public byte[] GeneratePdf(TaxInvoicePdfRequestDto request)
        {
            var document = Document.Create(container =>
            {
                if (request.PdfType != "DetailsOnly")
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(30, Unit.Point);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                        page.Header().Element(c => ComposeHeader(c, request));
                        page.Content().Element(c => ComposeContent(c, request));
                        page.Footer().Element(ComposeFooter);
                    });
                }
                else
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(30, Unit.Point);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                        page.Header().Element(c => ComposeHeader(c, request));
                        page.Content().Element(c => ComposeDetailsOnlyContent(c, request));
                        page.Footer().Element(ComposeFooter);
                    });
                }

                if (request.PdfType == "Full")
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(30, Unit.Point);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                        page.Header().Element(c => ComposeAttachmentHeader(c, request));
                        page.Content().Element(c => ComposeBreakdown(c, request));
                        page.Footer().Element(ComposeFooter);
                    });
                }
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, TaxInvoicePdfRequestDto request)
        {
            container.Row(row =>
            {
                row.RelativeItem().Row(logoRow =>
                {
                    var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.png");
                    if (File.Exists(logoPath))
                    {
                        logoRow.ConstantItem(80).Image(logoPath).FitArea();
                    }

                    logoRow.RelativeItem().PaddingLeft(10).Column(col =>
                    {
                        col.Item().Text(request.Business.Name).SemiBold().FontSize(12).FontColor(Colors.Blue.Darken2);
                        col.Item().Text($"ABN: {request.Business.Abn}").FontSize(9);
                        col.Item().PaddingTop(5).Text(request.Business.Address).FontSize(9);
                        col.Item().Text(request.Business.Mobile).FontSize(9);
                        col.Item().Text(request.Business.Email).FontSize(9);
                    });
                });

                row.ConstantItem(200).Column(col =>
                {
                    col.Item().Text("TAX INVOICE").Bold().FontSize(20).FontColor(Colors.Blue.Darken4).AlignRight();
                    
                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Cell().Text("Invoice No:").SemiBold().FontColor(Colors.Red.Medium);
                        table.Cell().Text(request.InvoiceNo).FontColor(Colors.Red.Medium).AlignRight();

                        table.Cell().Text("Issue Date:").SemiBold();
                        table.Cell().Text(request.IssueDate).AlignRight();

                        table.Cell().Text("Due Date:").SemiBold();
                        table.Cell().Text(request.DueDate).AlignRight();

                        table.Cell().Text("Payment Terms:").SemiBold();
                        table.Cell().Text("14 Days").AlignRight();
                    });
                });
            });
        }

        private void ComposeContent(IContainer container, TaxInvoicePdfRequestDto request)
        {
            container.PaddingTop(20).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("BILL TO").SemiBold().FontColor(Colors.Red.Medium).FontSize(10);
                        col.Item().PaddingTop(5).Background(Colors.Grey.Lighten4).Padding(10).Column(inner =>
                        {
                            inner.Item().Text(request.Preview.ClientName).SemiBold();
                            if (!string.IsNullOrEmpty(request.Preview.ClientAbn))
                                inner.Item().Text($"ABN: {request.Preview.ClientAbn}");
                            if (!string.IsNullOrEmpty(request.Preview.ClientEmail))
                                inner.Item().Text(request.Preview.ClientEmail);
                        });

                        col.Item().PaddingTop(10).Row(summaryRow =>
                        {
                            var uniqueSites = request.Preview.Items.Select(i => i.SiteName).Distinct().Count();
                            
                            summaryRow.RelativeItem().Background(Colors.Grey.Lighten4).Padding(5).Column(inner =>
                            {
                                inner.Item().Text($"{uniqueSites}").SemiBold().FontSize(12);
                                inner.Item().Text("SITES").FontSize(8).FontColor(Colors.Grey.Darken2);
                            });

                            summaryRow.ConstantItem(10);

                            summaryRow.RelativeItem(2).Background(Colors.Grey.Lighten4).Padding(5).Column(inner =>
                            {
                                var pStart = request.Preview.PeriodStart.ToString("d MMM yyyy");
                                var pEnd = request.Preview.PeriodEnd.ToString("d MMM yyyy");
                                inner.Item().Text($"{pStart} - {pEnd}").SemiBold().FontSize(10);
                                inner.Item().Text("SERVICE PERIOD").FontSize(8).FontColor(Colors.Grey.Darken2);
                            });
                        });
                    });

                    row.ConstantItem(20);

                    row.ConstantItem(200).Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                    {
                        col.Item().Background(Colors.Blue.Darken4).Padding(8).Text("INVOICE TOTAL").FontColor(Colors.White).SemiBold().AlignCenter();
                        
                        col.Item().Padding(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Cell().PaddingBottom(5).Text("SUBTOTAL");
                            table.Cell().PaddingBottom(5).Text($"${request.Preview.SubtotalExGst:F2}").AlignRight();

                            table.Cell().PaddingBottom(5).Text("GST (10%)");
                            table.Cell().PaddingBottom(5).Text($"${request.Preview.GstAmount:F2}").AlignRight();

                            table.Cell().PaddingTop(10).Text("TOTAL").SemiBold().FontColor(Colors.Red.Medium).FontSize(12);
                            table.Cell().PaddingTop(10).Text($"${request.Preview.TotalIncGst:F2}").SemiBold().FontColor(Colors.Red.Medium).FontSize(14).AlignRight();
                        });
                    });
                });

                column.Item().PaddingTop(20).Text("Thank you for your business.    See Attachment A for breakdown ->").FontSize(9).FontColor(Colors.Grey.Darken3);

                column.Item().PaddingTop(10).Background(Colors.Grey.Lighten4).Padding(10).Row(row =>
                {
                    row.ConstantItem(100).Text("PAYMENT DETAILS").SemiBold().FontColor(Colors.Red.Medium);
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Bank Name").FontSize(8).FontColor(Colors.Grey.Darken2);
                        col.Item().Text("Commonwealth Bank");
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("BSB").FontSize(8).FontColor(Colors.Grey.Darken2);
                        col.Item().Text(request.Business.Bsb);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Account No.").FontSize(8).FontColor(Colors.Grey.Darken2);
                        col.Item().Text(request.Business.AccountNumber);
                    });
                    row.RelativeItem(1.5f).Column(col =>
                    {
                        col.Item().Text("Reference / Description").FontSize(8).FontColor(Colors.Grey.Darken2);
                        col.Item().Text(request.InvoiceNo);
                    });
                });

                column.Item().PaddingTop(5).Text("Please use the invoice number as payment reference.").FontSize(8).FontColor(Colors.Grey.Darken1).AlignCenter();

                if (request.PdfType == "Full")
                {
                    column.Item().PaddingTop(30).Element(c => ComposeSiteSummary(c, request));
                }
            });
        }

        private void ComposeDetailsOnlyContent(IContainer container, TaxInvoicePdfRequestDto request)
        {
            container.PaddingTop(20).Column(column =>
            {
                column.Item().Element(c => ComposeSiteSummary(c, request));
                column.Item().PaddingTop(30).Text("SERVICE BREAKDOWN").Bold().FontSize(16).FontColor(Colors.Blue.Darken4);
                column.Item().PaddingTop(10).Element(c => ComposeBreakdown(c, request));
            });
        }

        private void ComposeSiteSummary(IContainer container, TaxInvoicePdfRequestDto request)
        {
            container.Column(column =>
            {
                column.Item().Text("SITE SUMMARY").SemiBold().FontSize(12).FontColor(Colors.Blue.Darken4);
                column.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Blue.Darken4).Padding(5).Text("SITE NAME / BILLING RULE").FontColor(Colors.White).SemiBold();
                        header.Cell().Background(Colors.Blue.Darken4).Padding(5).Text("SERVICES").FontColor(Colors.White).SemiBold().AlignCenter();
                        header.Cell().Background(Colors.Blue.Darken4).Padding(5).Text("TOTAL (EX GST)").FontColor(Colors.White).SemiBold().AlignRight();
                    });

                    var siteGroups = request.Preview.Items
                        .GroupBy(i => i.SiteName)
                        .OrderBy(g => g.Key)
                        .ToList();

                    foreach (var group in siteGroups)
                    {
                        var totalAmount = group.Sum(i => i.FinalAmountExGst);
                        var count = group.Count();

                        table.Cell().Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(group.Key).SemiBold();
                        table.Cell().Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"{count}").SemiBold().AlignCenter();
                        table.Cell().Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"${totalAmount:F2}").SemiBold().AlignRight();

                        var ruleGroups = group.GroupBy(i => new { 
                                Desc = string.IsNullOrWhiteSpace(i.Description) ? "Standard Service" : i.Description, 
                                Rate = i.FinalAmountExGst 
                            })
                            .OrderBy(rg => rg.Key.Desc)
                            .ThenBy(rg => rg.Key.Rate)
                            .ToList();

                        foreach (var rg in ruleGroups)
                        {
                            var ruleCount = rg.Count();
                            var ruleTotal = rg.Sum(i => i.FinalAmountExGst);
                            var rateText = rg.Key.Rate > 0 ? $" (@ ${rg.Key.Rate:F2})" : "";
                            var label = $"  - {rg.Key.Desc}{rateText}";

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(5).PaddingLeft(15).Text(label).FontSize(9).FontColor(Colors.Grey.Darken3);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(5).Text($"{ruleCount}").FontSize(9).FontColor(Colors.Grey.Darken3).AlignCenter();
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(5).Text($"${ruleTotal:F2}").FontSize(9).FontColor(Colors.Grey.Darken3).AlignRight();
                        }
                    }
                });
            });
        }

        private void ComposeAttachmentHeader(IContainer container, TaxInvoicePdfRequestDto request)
        {
            container.Row(row =>
            {
                var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.png");
                if (File.Exists(logoPath))
                {
                    row.ConstantItem(80).Image(logoPath).FitArea();
                }

                row.RelativeItem().PaddingLeft(10).Column(col =>
                {
                    col.Item().Text("SERVICE BREAKDOWN").Bold().FontSize(16).FontColor(Colors.Blue.Darken4);
                    col.Item().Text("Attachment A").SemiBold().FontSize(10);
                });

                row.ConstantItem(200).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Cell().Text("Invoice No:").SemiBold();
                    table.Cell().Text(request.InvoiceNo).FontColor(Colors.Red.Medium).AlignRight();

                    table.Cell().Text("Issue Date:").SemiBold();
                    table.Cell().Text(request.IssueDate).AlignRight();
                });
            });
        }

        private void ComposeBreakdown(IContainer container, TaxInvoicePdfRequestDto request)
        {
            container.PaddingTop(20).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(80);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Blue.Darken4).Padding(5).Text("DATE").FontColor(Colors.White).SemiBold();
                    header.Cell().Background(Colors.Blue.Darken4).Padding(5).Text("SITE NAME").FontColor(Colors.White).SemiBold();
                    header.Cell().Background(Colors.Blue.Darken4).Padding(5).Text("DESCRIPTION").FontColor(Colors.White).SemiBold();
                    header.Cell().Background(Colors.Blue.Darken4).Padding(5).Text("NOTE").FontColor(Colors.White).SemiBold();
                    header.Cell().Background(Colors.Blue.Darken4).Padding(5).Text("TOTAL (AUD)").FontColor(Colors.White).SemiBold().AlignRight();
                });

                var sortedItems = request.Preview.Items
                    .OrderBy(i => i.ServiceDate)
                    .ThenBy(i => i.SiteName)
                    .ToList();

                string? lastDateString = null;
                bool isAlternateColor = false;

                foreach (var item in sortedItems)
                {
                    var dateObj = item.ServiceDate;
                    var currentDateString = dateObj.ToString("d/M/yyyy");

                    if (lastDateString != null && currentDateString != lastDateString)
                    {
                        isAlternateColor = !isAlternateColor;
                    }

                    var bgColor = isAlternateColor ? Colors.Grey.Lighten4 : Colors.White;

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(currentDateString);
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(item.SiteName ?? "-");
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(item.Description ?? "-");
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(item.Note ?? "-");
                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"${item.FinalAmountExGst:F2}").AlignRight();

                    lastDateString = currentDateString;
                }
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(x =>
            {
                x.Span("Page ");
                x.CurrentPageNumber();
                x.Span(" of ");
                x.TotalPages();
            });
        }
    }
}
