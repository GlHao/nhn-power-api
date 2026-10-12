using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NHN.Power.API.DTOs.Payroll;
using NHN.Power.API.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NHN.Power.API.Services
{
    public interface IEmployeePaySlipPdfService
    {
        byte[] GeneratePdf(PaySlipPdfRequestDto request);
        Task<(bool Success, string Message, string? R2Path)> SendPaySlipEmailAsync(SendPaySlipEmailRequestDto request);
    }

    public class EmployeePaySlipPdfService : IEmployeePaySlipPdfService
    {
        private readonly AppSettings _appSettings;
        private readonly ILogger<EmployeePaySlipPdfService> _logger;
        private readonly IStorageService _storageService;

        public EmployeePaySlipPdfService(
            IOptions<AppSettings> appSettings,
            ILogger<EmployeePaySlipPdfService> logger,
            IStorageService storageService)
        {
            _appSettings = appSettings.Value;
            _logger = logger;
            _storageService = storageService;
        }

        public async Task<(bool Success, string Message, string? R2Path)> SendPaySlipEmailAsync(SendPaySlipEmailRequestDto request)
        {
            var pdfReq = request.PdfRequest;
            if (pdfReq == null || pdfReq.Preview == null || pdfReq.Business == null)
            {
                return (false, "Invalid request payload.", null);
            }

            string rawEmails = !string.IsNullOrWhiteSpace(request.TargetEmails)
                ? request.TargetEmails
                : (pdfReq.Preview.EmployeeEmail ?? string.Empty);

            var emailList = rawEmails
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e) && e.Contains("@"))
                .Distinct()
                .ToList();

            if (emailList.Count == 0)
            {
                return (false, "No valid email address provided for employee.", null);
            }

            string primaryEmail = emailList[0];
            var ccEmails = emailList.Skip(1).ToList();

            byte[] pdfBytes = GeneratePdf(pdfReq);

            if (string.IsNullOrEmpty(_appSettings.SmtpHost) || _appSettings.SmtpPort == 0)
            {
                return (false, "SMTP server is not configured in settings.", null);
            }

            string paySlipNo = string.IsNullOrWhiteSpace(pdfReq.PaySlipNo) ? "PS-DRAFT" : pdfReq.PaySlipNo;
            string employeeName = pdfReq.Preview.EmployeeName;
            string periodStart = pdfReq.Preview.PeriodStart.ToString("d MMM yyyy");
            string periodEnd = pdfReq.Preview.PeriodEnd.ToString("d MMM yyyy");
            string totalPay = $"${pdfReq.Preview.TotalPayExGst:F2}";
            string brandName = pdfReq.Business.Name;

            string emailSubject = $"Pay Slip {paySlipNo} ({periodStart} - {periodEnd}) from {brandName}";
            string emailBodyHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; color: #333; line-height: 1.6; background-color: #f4f6f9; margin: 0; padding: 20px; }}
        .card {{ max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 10px rgba(0,0,0,0.05); }}
        .header {{ background: linear-gradient(135deg, #1e3a8a 0%, #3b82f6 100%); color: #ffffff; padding: 25px; text-align: center; }}
        .header h1 {{ margin: 0; font-size: 24px; font-weight: 700; letter-spacing: 0.5px; }}
        .header p {{ margin: 5px 0 0 0; opacity: 0.9; font-size: 14px; }}
        .content {{ padding: 30px; }}
        .greeting {{ font-size: 16px; font-weight: 600; color: #1e293b; margin-bottom: 15px; }}
        .details-box {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 20px; margin: 20px 0; }}
        .details-row {{ display: flex; justify-content: space-between; margin-bottom: 10px; font-size: 14px; }}
        .details-row:last-child {{ margin-bottom: 0; }}
        .label {{ color: #64748b; font-weight: 500; }}
        .value {{ color: #0f172a; font-weight: 600; }}
        .total-row {{ border-top: 2px dashed #cbd5e1; padding-top: 10px; margin-top: 10px; font-size: 16px; }}
        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #94a3b8; border-top: 1px solid #f1f5f9; }}
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""header"">
            <h1>EMPLOYEE PAY SLIP</h1>
            <p>{brandName}</p>
        </div>
        <div class=""content"">
            <div class=""greeting"">Dear {WebUtility.HtmlEncode(employeeName)},</div>
            <p>Please find attached your Pay Slip for the period <strong>{periodStart}</strong> to <strong>{periodEnd}</strong>.</p>
            
            <div class=""details-box"">
                <div class=""details-row"">
                    <span class=""label"">Pay Slip No:</span>
                    <span class=""value"">{WebUtility.HtmlEncode(paySlipNo)}</span>
                </div>
                <div class=""details-row"">
                    <span class=""label"">Pay Period:</span>
                    <span class=""value"">{periodStart} – {periodEnd}</span>
                </div>
                <div class=""details-row"">
                    <span class=""label"">Total Services:</span>
                    <span class=""value"">{pdfReq.Preview.TotalServices} shifts</span>
                </div>
                <div class=""details-row total-row"">
                    <span class=""label"">Total Net Pay:</span>
                    <span class=""value"" style=""color: #2563eb;"">{totalPay}</span>
                </div>
            </div>

            <p>If you have any questions regarding your pay slip, please reply directly to this email.</p>
        </div>
        <div class=""footer"">
            &copy; {DateTime.UtcNow.Year} {brandName}. All rights reserved.
        </div>
    </div>
</body>
</html>";

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

                string pdfFileName = $"PaySlip_{paySlipNo}.pdf";
                using var pdfStream = new MemoryStream(pdfBytes);
                mailMessage.Attachments.Add(new Attachment(pdfStream, pdfFileName, "application/pdf"));

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Successfully sent Pay Slip {PaySlipNo} to {PrimaryEmail}", paySlipNo, primaryEmail);

                string safeEmpName = string.Concat(employeeName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                string yearStr = DateTime.UtcNow.Year.ToString();
                string r2Key = $"payslips/{yearStr}/{safeEmpName}/{paySlipNo}.pdf";

                bool uploaded = await _storageService.UploadFileAsync(r2Key, pdfBytes, "application/pdf");
                if (uploaded)
                {
                    _logger.LogInformation("Successfully backed up pay slip {PaySlipNo} to R2 at {Key}", paySlipNo, r2Key);
                }

                return (true, $"Pay slip sent successfully to {primaryEmail}", r2Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email for pay slip {PaySlipNo}", paySlipNo);
                return (false, $"Failed to send email: {ex.Message}", null);
            }
        }

        private static bool _fontRegistered = false;
        private static readonly object _fontLock = new object();

        private static void EnsureChineseFontRegistered()
        {
            if (_fontRegistered) return;
            lock (_fontLock)
            {
                if (_fontRegistered) return;
                try
                {
                    string[] possibleFontPaths = {
                        @"C:\Windows\Fonts\msyh.ttc",
                        @"C:\Windows\Fonts\msyh.ttf",
                        @"C:\Windows\Fonts\simhei.ttf",
                        @"C:\Windows\Fonts\simsun.ttc",
                        @"/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc",
                        @"/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
                        @"/usr/share/fonts/truetype/wqy/wqy-microhei.ttc"
                    };

                    foreach (var path in possibleFontPaths)
                    {
                        if (File.Exists(path))
                        {
                            using var fs = File.OpenRead(path);
                            QuestPDF.Drawing.FontManager.RegisterFont(fs);
                            break;
                        }
                    }
                }
                catch
                {
                    // Ignore font registration error and fallback to standard font
                }
                finally
                {
                    _fontRegistered = true;
                }
            }
        }

        public byte[] GeneratePdf(PaySlipPdfRequestDto request)
        {
            EnsureChineseFontRegistered();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30, Unit.Point);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Microsoft YaHei", "PingFang SC", "SimHei", "Noto Sans CJK SC", "Arial"));

                    page.Header().Element(c => ComposeHeader(c, request));
                    page.Content().Element(c => ComposeContent(c, request));
                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, PaySlipPdfRequestDto request)
        {
            var b = request.Business;
            var preview = request.Preview;

            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(b.Name).FontSize(18).Bold().FontColor("#1e3a8a");
                    if (!string.IsNullOrWhiteSpace(b.Address))
                        col.Item().Text(b.Address).FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrWhiteSpace(b.Abn))
                        col.Item().Text($"ABN: {b.Abn}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrWhiteSpace(b.Mobile))
                        col.Item().Text($"Phone: {b.Mobile}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrWhiteSpace(b.Email))
                        col.Item().Text($"Email: {b.Email}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("PAY SLIP").FontSize(22).Bold().FontColor("#1e3a8a");
                    col.Item().Text($"Pay Slip No: {request.PaySlipNo}").FontSize(10).Bold();
                    col.Item().Text($"Issue Date: {request.IssueDate}").FontSize(9);
                    col.Item().Text($"Period: {preview.PeriodStart:d MMM yyyy} - {preview.PeriodEnd:d MMM yyyy}").FontSize(9).Bold().FontColor("#2563eb");
                });
            });
        }

        private void ComposeContent(IContainer container, PaySlipPdfRequestDto request)
        {
            var preview = request.Preview;

            container.PaddingVertical(15).Column(col =>
            {
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                col.Item().PaddingVertical(10).Row(row =>
                {
                    row.RelativeItem().Column(empCol =>
                    {
                        empCol.Item().Text("EMPLOYEE DETAILS").FontSize(10).Bold().FontColor("#1e3a8a");
                        empCol.Item().Text(preview.EmployeeName).FontSize(12).Bold();
                        if (!string.IsNullOrWhiteSpace(preview.EmployeePhone))
                            empCol.Item().Text($"Phone: {preview.EmployeePhone}").FontSize(9);
                        if (!string.IsNullOrWhiteSpace(preview.EmployeeEmail))
                            empCol.Item().Text($"Email: {preview.EmployeeEmail}").FontSize(9);
                    });

                    row.RelativeItem().AlignRight().Column(sumCol =>
                    {
                        sumCol.Item().Background("#f8fafc").Padding(10).Border(1).BorderColor("#e2e8f0").Column(inner =>
                        {
                            inner.Item().Text("PAYMENT SUMMARY").FontSize(9).Bold().FontColor("#64748b");
                            inner.Item().Text($"Total Shifts: {preview.TotalServices}").FontSize(10);
                            inner.Item().Text($"Total Net Pay: ${preview.TotalPayExGst:F2}").FontSize(14).Bold().FontColor("#2563eb");
                        });
                    });
                });

                col.Item().PaddingTop(10).Text("SERVICE SHIFT BREAKDOWN").FontSize(11).Bold().FontColor("#1e3a8a");

                col.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(70);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.ConstantColumn(80);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background("#1e3a8a").Padding(5).Text("Date").Bold().FontColor(Colors.White);
                        header.Cell().Background("#1e3a8a").Padding(5).Text("Day").Bold().FontColor(Colors.White);
                        header.Cell().Background("#1e3a8a").Padding(5).Text("Site Name").Bold().FontColor(Colors.White);
                        header.Cell().Background("#1e3a8a").Padding(5).Text("Note").Bold().FontColor(Colors.White);
                        header.Cell().Background("#1e3a8a").Padding(5).AlignRight().Text("Pay (Ex GST)").Bold().FontColor(Colors.White);
                    });

                    string[] dayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

                    for (int i = 0; i < preview.Items.Count; i++)
                    {
                        var item = preview.Items[i];
                        string bgColor = i % 2 == 0 ? "#ffffff" : "#f8fafc";
                        string dayStr = item.DayOfWeek >= 0 && item.DayOfWeek < 7 ? dayNames[item.DayOfWeek] : "";

                        table.Cell().Background(bgColor).Padding(5).Text(item.ServiceDate.ToString("dd/MM/yyyy"));
                        table.Cell().Background(bgColor).Padding(5).Text(dayStr);
                        table.Cell().Background(bgColor).Padding(5).Text(item.SiteName).Bold();
                        table.Cell().Background(bgColor).Padding(5).Text(item.Note ?? item.Description ?? "-").FontSize(9);
                        table.Cell().Background(bgColor).Padding(5).AlignRight().Text($"${item.PayExGst:F2}").Bold();
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Row(sumRow =>
                {
                    sumRow.AutoItem().Text("Total Net Pay: ").FontSize(12).Bold();
                    sumRow.AutoItem().Text($"${preview.TotalPayExGst:F2}").FontSize(14).Bold().FontColor("#2563eb");
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Text("Thank you for your dedicated service with NHN Power.").FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        }
    }
}
