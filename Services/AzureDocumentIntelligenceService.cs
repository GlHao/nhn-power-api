using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NHN.Power.API.Models;

namespace NHN.Power.API.Services
{
    public class ReceiptOcrResult
    {
        public string? MerchantName { get; set; }
        public DateTime? TransactionDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal GstAmount { get; set; }
        public string RawJson { get; set; } = string.Empty;
    }

    public interface IAzureDocumentIntelligenceService
    {
        Task<ReceiptOcrResult> AnalyzeReceiptAsync(byte[] fileBytes, string contentType);
    }

    public class AzureDocumentIntelligenceService : IAzureDocumentIntelligenceService
    {
        private readonly ILogger<AzureDocumentIntelligenceService> _logger;
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _defaultModelId;

        public AzureDocumentIntelligenceService(IConfiguration configuration, ILogger<AzureDocumentIntelligenceService> logger)
        {
            _logger = logger;
            _endpoint = configuration["AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT"] 
                        ?? configuration["AppSettings:AzureDocumentIntelligenceEndpoint"] 
                        ?? string.Empty;
            _apiKey = configuration["AZURE_DOCUMENT_INTELLIGENCE_KEY"] 
                      ?? configuration["AppSettings:AzureDocumentIntelligenceKey"] 
                      ?? string.Empty;
            _defaultModelId = configuration["AZURE_DOCUMENT_INTELLIGENCE_RECEIPT_MODEL"] 
                              ?? configuration["AppSettings:AzureDocumentIntelligenceReceiptModel"] 
                              ?? "prebuilt-receipt";
        }

        public async Task<ReceiptOcrResult> AnalyzeReceiptAsync(byte[] fileBytes, string contentType)
        {
            var result = new ReceiptOcrResult();

            if (string.IsNullOrWhiteSpace(_endpoint) || string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("Azure Document Intelligence endpoint or key is missing. Skipping OCR.");
                return result;
            }

            try
            {
                var client = new DocumentIntelligenceClient(new Uri(_endpoint), new AzureKeyCredential(_apiKey));
                var content = BinaryData.FromBytes(fileBytes);

                // Determine models to try: default receipt model, then fallback to invoice model if needed
                string[] modelsToTry = _defaultModelId == "prebuilt-invoice"
                    ? new[] { "prebuilt-invoice", "prebuilt-receipt" }
                    : new[] { _defaultModelId, "prebuilt-invoice" };

                AnalyzeResult? bestAnalyzeResult = null;

                foreach (var model in modelsToTry)
                {
                    try
                    {
                        _logger.LogInformation("Analyzing document using model '{ModelId}'...", model);
                        var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, model, content);
                        var analyzeResult = operation.Value.ToObjectFromJson<AnalyzeResult>();

                        if (analyzeResult?.Documents != null && analyzeResult.Documents.Count > 0)
                        {
                            ExtractFieldsFromDocument(analyzeResult.Documents[0], result);
                            bestAnalyzeResult = analyzeResult;

                            // If we obtained a non-zero amount or a valid merchant name, consider it a success
                            if (result.TotalAmount > 0 || !string.IsNullOrWhiteSpace(result.MerchantName))
                            {
                                _logger.LogInformation("Successfully extracted data with model '{ModelId}': Merchant='{Merchant}', Total={Total}, GST={Gst}",
                                    model, result.MerchantName, result.TotalAmount, result.GstAmount);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Model '{ModelId}' failed during document analysis.", model);
                    }
                }

                // If documents array didn't match, attempt page text / key-value fallback
                if (result.TotalAmount == 0 && bestAnalyzeResult != null)
                {
                    TryFallbackExtraction(bestAnalyzeResult, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error analyzing receipt with Azure Document Intelligence.");
            }

            return result;
        }

        private void ExtractFieldsFromDocument(AnalyzedDocument doc, ReceiptOcrResult result)
        {
            // 1. Merchant / Vendor Name
            string[] merchantKeys = { "MerchantName", "VendorName", "SupplierName", "Merchant", "Vendor" };
            foreach (var key in merchantKeys)
            {
                if (doc.Fields.TryGetValue(key, out var field) && field != null)
                {
                    string? name = field.ValueString ?? field.Content;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        result.MerchantName = name.Trim();
                        break;
                    }
                }
            }

            // 2. Transaction / Invoice Date
            string[] dateKeys = { "TransactionDate", "InvoiceDate", "Date", "BillingDate" };
            foreach (var key in dateKeys)
            {
                if (doc.Fields.TryGetValue(key, out var field) && field != null)
                {
                    if (field.ValueDate.HasValue)
                    {
                        result.TransactionDate = field.ValueDate.Value.DateTime;
                        break;
                    }
                    else if (DateTime.TryParse(field.Content, out var parsedDate))
                    {
                        result.TransactionDate = parsedDate;
                        break;
                    }
                }
            }

            // 3. Total Amount
            string[] totalKeys = { "Total", "InvoiceTotal", "AmountDue", "TotalDue", "BalanceDue" };
            foreach (var key in totalKeys)
            {
                if (doc.Fields.TryGetValue(key, out var field) && field != null)
                {
                    if (field.ValueCurrency != null)
                    {
                        result.TotalAmount = (decimal)field.ValueCurrency.Amount;
                        break;
                    }
                    else if (TryParseAmount(field.Content, out var parsedTotal))
                    {
                        result.TotalAmount = parsedTotal;
                        break;
                    }
                }
            }

            // 4. GST / Tax Amount
            string[] taxKeys = { "TotalTax", "Tax", "TaxAmount", "Gst", "GST", "SalesTax" };
            foreach (var key in taxKeys)
            {
                if (doc.Fields.TryGetValue(key, out var field) && field != null)
                {
                    if (field.ValueCurrency != null)
                    {
                        result.GstAmount = (decimal)field.ValueCurrency.Amount;
                        break;
                    }
                    else if (TryParseAmount(field.Content, out var parsedTax))
                    {
                        result.GstAmount = parsedTax;
                        break;
                    }
                }
            }
        }

        private void TryFallbackExtraction(AnalyzeResult analyzeResult, ReceiptOcrResult result)
        {
            // If KeyValuePairs exist in the analyzed result (e.g., from invoice/general models)
            if (analyzeResult.KeyValuePairs != null)
            {
                foreach (var kvp in analyzeResult.KeyValuePairs)
                {
                    string? key = kvp.Key?.Content?.ToLowerInvariant()?.Trim();
                    string? val = kvp.Value?.Content?.Trim();
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(val)) continue;

                    if (result.TotalAmount == 0 && (key.Contains("total") || key.Contains("amount due") || key.Contains("balance due")))
                    {
                        if (TryParseAmount(val, out var total))
                        {
                            result.TotalAmount = total;
                        }
                    }

                    if (result.GstAmount == 0 && (key.Contains("tax") || key.Contains("gst")))
                    {
                        if (TryParseAmount(val, out var gst))
                        {
                            result.GstAmount = gst;
                        }
                    }
                }
            }
        }

        private static bool TryParseAmount(string? text, out decimal amount)
        {
            amount = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Remove currency symbols, commas, whitespace
            string sanitized = text.Replace("$", "")
                                   .Replace("AUD", "", StringComparison.OrdinalIgnoreCase)
                                   .Replace(",", "")
                                   .Trim();

            // In case of multiple numbers or tokens, try matching first valid float pattern
            var match = System.Text.RegularExpressions.Regex.Match(sanitized, @"\d+(\.\d{1,2})?");
            if (match.Success && decimal.TryParse(match.Value, out var parsed))
            {
                amount = parsed;
                return true;
            }

            return decimal.TryParse(sanitized, out amount);
        }
    }
}
