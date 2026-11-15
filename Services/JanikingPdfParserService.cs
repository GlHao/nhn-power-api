using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs.Janiking;
using UglyToad.PdfPig;

namespace NHN.Power.API.Services
{
    public interface IJanikingPdfParserService
    {
        Task<JanikingParsedData> ParsePdfAsync(Stream pdfStream);
        Task<JanikingParsedData> ParsePdfAsync(byte[] pdfBytes);
    }

    public class JanikingPdfParserService : IJanikingPdfParserService
    {
        private readonly ILogger<JanikingPdfParserService> _logger;

        public JanikingPdfParserService(ILogger<JanikingPdfParserService> logger)
        {
            _logger = logger;
        }

        public async Task<JanikingParsedData> ParsePdfAsync(Stream pdfStream)
        {
            using var ms = new MemoryStream();
            await pdfStream.CopyToAsync(ms);
            return await ParsePdfAsync(ms.ToArray());
        }

        public Task<JanikingParsedData> ParsePdfAsync(byte[] pdfBytes)
        {
            var result = new JanikingParsedData();

            try
            {
                using var stream = new MemoryStream(pdfBytes);
                using var doc = PdfDocument.Open(stream);

                if (doc.NumberOfPages == 0)
                {
                    throw new InvalidOperationException("PDF document contains no pages.");
                }

                // 1. Parse Page 1: Month, Due to Franchisee, Total Contract Billing
                var p1Text = doc.GetPage(1).Text;
                DateTime reportDate = DateTime.MinValue;

                var monthMatch = Regex.Match(p1Text, @"Business\s*for\s*the\s*month\s*of\s*([A-Za-z]+)\s*(\d{4})", RegexOptions.IgnoreCase);
                if (monthMatch.Success)
                {
                    string monthStr = monthMatch.Groups[1].Value;
                    string yearStr = monthMatch.Groups[2].Value;
                    if (DateTime.TryParseExact($"{monthStr} {yearStr}", "MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out reportDate))
                    {
                        result.ReportMonth = reportDate.ToString("yyyy-MM");
                    }
                }

                if (string.IsNullOrEmpty(result.ReportMonth))
                {
                    throw new InvalidOperationException("Could not extract report month from Jani-King PDF.");
                }

                var dueMatch = Regex.Match(p1Text, @"Due\s*to\s*Franchisee\s*\$?([\d,]+\.\d{2})", RegexOptions.IgnoreCase);
                if (dueMatch.Success && decimal.TryParse(dueMatch.Groups[1].Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var due))
                {
                    result.DueToFranchisee = due;
                }

                var contractTotalMatch = Regex.Match(p1Text, @"Total\s*Contract\s*Billing\s*\$?([\d,]+\.\d{2})", RegexOptions.IgnoreCase);
                if (contractTotalMatch.Success && decimal.TryParse(contractTotalMatch.Groups[1].Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var tcb))
                {
                    result.TotalContractBilling = tcb;
                }

                // 2. Parse Page 3: Charge Backs
                if (doc.NumberOfPages >= 3)
                {
                    var p3Text = doc.GetPage(3).Text;
                    int cbIdx = p3Text.IndexOf("ChargeBacks", StringComparison.OrdinalIgnoreCase);
                    if (cbIdx >= 0)
                    {
                        int endIdx = p3Text.IndexOf("TotalChargeBacks", cbIdx, StringComparison.OrdinalIgnoreCase);
                        if (endIdx > cbIdx)
                        {
                            string cbSection = p3Text.Substring(cbIdx, endIdx - cbIdx);
                            var cbMatches = Regex.Matches(cbSection, @"(\d{5,8})\s*(\d{2}-[A-Za-z]{3}-\d{4})\s*(.+?)\s*\$?([\d,]+\.\d{2})\s*\$?([\d,]+\.\d{2})\s*\$?([\d,]+\.\d{2})");
                            foreach (Match m in cbMatches)
                            {
                                var cb = new ParsedJanikingChargeBack
                                {
                                    ReportMonth = result.ReportMonth,
                                    InvoiceNumber = m.Groups[1].Value,
                                    TotalAmount = decimal.Parse(m.Groups[4].Value.Replace(",", ""), CultureInfo.InvariantCulture),
                                    PaymentsAmount = decimal.Parse(m.Groups[5].Value.Replace(",", ""), CultureInfo.InvariantCulture),
                                    ChargeBackAmount = decimal.Parse(m.Groups[6].Value.Replace(",", ""), CultureInfo.InvariantCulture)
                                };

                                if (DateTime.TryParseExact(m.Groups[2].Value, "dd-MMM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var idt))
                                {
                                    cb.InvoiceDate = DateTime.SpecifyKind(idt, DateTimeKind.Utc);
                                }

                                string rawCustDesc = m.Groups[3].Value.Trim();
                                int dIdx = rawCustDesc.IndexOf("Date:", StringComparison.OrdinalIgnoreCase);
                                if (dIdx > 0)
                                {
                                    cb.CustomerName = rawCustDesc.Substring(0, dIdx).Trim();
                                    cb.Description = rawCustDesc.Substring(dIdx).Trim();
                                }
                                else
                                {
                                    cb.CustomerName = rawCustDesc;
                                }

                                result.ChargeBacks.Add(cb);
                                result.ChargeBackAmount += cb.ChargeBackAmount;
                            }
                        }
                    }
                }

                result.ExpectedAmount = result.DueToFranchisee + result.ChargeBackAmount;

                // 3. Parse Page 2: Customer Account Totals
                if (doc.NumberOfPages >= 2)
                {
                    var p2Text = doc.GetPage(2).Text;
                    int catIdx = p2Text.IndexOf("CustomerAccountTotals", StringComparison.OrdinalIgnoreCase);
                    if (catIdx >= 0)
                    {
                        int endIdx = p2Text.IndexOf("FranchiseActualAccountTotals", catIdx, StringComparison.OrdinalIgnoreCase);
                        if (endIdx > catIdx)
                        {
                            string catSection = p2Text.Substring(catIdx, endIdx - catIdx);

                            // Match each customer block starting with customer code (30CL...)
                            var matches = Regex.Matches(catSection, @"(30CL[A-Za-z0-9]+)\s+((?:(?!(?:30CL[A-Za-z0-9]+)).)+)", RegexOptions.Singleline);
                            foreach (Match m in matches)
                            {
                                string code = m.Groups[1].Value.Trim();
                                string rest = m.Groups[2].Value.Trim();

                                int firstDollarIdx = rest.IndexOf('$');
                                if (firstDollarIdx < 0) continue;

                                string custName = rest.Substring(0, firstDollarIdx).Trim();
                                string valuesPart = rest.Substring(firstDollarIdx);

                                var firstAmountMatch = Regex.Match(valuesPart, @"^\$?([\d,]+\.\d{2})");
                                decimal contractBilling = 0;
                                if (firstAmountMatch.Success)
                                {
                                    contractBilling = decimal.Parse(firstAmountMatch.Groups[1].Value.Replace(",", ""), CultureInfo.InvariantCulture);
                                }

                                string? ffRaw = null;
                                string? ffEndMonth = null;
                                decimal ffAmount = 0;

                                var ffMatch = Regex.Match(valuesPart, @"(\d{1,2})\s*of\s*(\d{1,2})\s*MP", RegexOptions.IgnoreCase);
                                if (ffMatch.Success)
                                {
                                    int curP = int.Parse(ffMatch.Groups[1].Value);
                                    int totP = int.Parse(ffMatch.Groups[2].Value);
                                    ffRaw = $"{curP:D2} of {totP:D2}MP";

                                    if (totP > curP && reportDate != DateTime.MinValue)
                                    {
                                        int remMonths = totP - curP;
                                        var endDate = reportDate.AddMonths(remMonths);
                                        ffEndMonth = endDate.ToString("MM/yyyy");
                                    }

                                    int mpIdx = valuesPart.IndexOf(ffMatch.Value, StringComparison.OrdinalIgnoreCase);
                                    if (mpIdx >= 0)
                                    {
                                        string afterMp = valuesPart.Substring(mpIdx + ffMatch.Length);
                                        var ffAmtMatch = Regex.Match(afterMp, @"\$?([\d,]+\.\d{2})");
                                        if (ffAmtMatch.Success)
                                        {
                                            ffAmount = decimal.Parse(ffAmtMatch.Groups[1].Value.Replace(",", ""), CultureInfo.InvariantCulture);
                                        }
                                    }
                                }

                                decimal actualBilling = Math.Round(contractBilling * 0.86m - ffAmount, 2);

                                result.Clients.Add(new ParsedJanikingClient
                                {
                                    CustomerCode = code,
                                    Name = custName,
                                    ContractBilling = contractBilling,
                                    FindersFeeRaw = ffRaw,
                                    FindersFeeEndMonth = ffEndMonth,
                                    FindersFeeAmount = ffAmount,
                                    ActualBilling = actualBilling
                                });
                            }
                        }
                    }
                }

                _logger.LogInformation("Successfully parsed Jani-King PDF for {Month}. Clients: {Clients}, ChargeBacks: {ChargeBacks}, Due: {Due}",
                    result.ReportMonth, result.Clients.Count, result.ChargeBacks.Count, result.DueToFranchisee);

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Jani-King PDF");
                throw;
            }
        }
    }
}
