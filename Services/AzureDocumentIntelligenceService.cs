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
        private readonly string _modelId;

        public AzureDocumentIntelligenceService(IConfiguration configuration, ILogger<AzureDocumentIntelligenceService> logger)
        {
            _logger = logger;
            _endpoint = configuration["AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT"] 
                        ?? configuration["AppSettings:AzureDocumentIntelligenceEndpoint"] 
                        ?? string.Empty;
            _apiKey = configuration["AZURE_DOCUMENT_INTELLIGENCE_KEY"] 
                     ?? configuration["AppSettings:AzureDocumentIntelligenceKey"] 
                     ?? string.Empty;
            _modelId = configuration["AZURE_DOCUMENT_INTELLIGENCE_RECEIPT_MODEL"] 
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

                var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, _modelId, content);
                AnalyzeResult? analyzeResult = operation.Value.ToObjectFromJson<AnalyzeResult>();

                if (analyzeResult?.Documents != null && analyzeResult.Documents.Count > 0)
                {
                    var doc = analyzeResult.Documents.First();

                    // 1. Merchant Name
                    if (doc.Fields.TryGetValue("MerchantName", out var merchantField) && merchantField != null)
                    {
                        result.MerchantName = merchantField.ValueString ?? merchantField.Content;
                    }

                    // 2. Transaction Date
                    if (doc.Fields.TryGetValue("TransactionDate", out var dateField) && dateField != null)
                    {
                        if (dateField.ValueDate.HasValue)
                        {
                            result.TransactionDate = dateField.ValueDate.Value.DateTime;
                        }
                        else if (DateTime.TryParse(dateField.Content, out var parsedDate))
                        {
                            result.TransactionDate = parsedDate;
                        }
                    }

                    // 3. Total Amount
                    if (doc.Fields.TryGetValue("Total", out var totalField) && totalField != null)
                    {
                        if (totalField.ValueCurrency != null)
                        {
                            result.TotalAmount = (decimal)totalField.ValueCurrency.Amount;
                        }
                        else if (decimal.TryParse(totalField.Content?.Replace("$", "").Trim(), out var parsedTotal))
                        {
                            result.TotalAmount = parsedTotal;
                        }
                    }

                    // 4. GST / Tax Amount
                    if (doc.Fields.TryGetValue("TotalTax", out var taxField) && taxField != null)
                    {
                        if (taxField.ValueCurrency != null)
                        {
                            result.GstAmount = (decimal)taxField.ValueCurrency.Amount;
                        }
                        else if (decimal.TryParse(taxField.Content?.Replace("$", "").Trim(), out var parsedTax))
                        {
                            result.GstAmount = parsedTax;
                        }
                    }
                    else if (doc.Fields.TryGetValue("Tax", out var altTaxField) && altTaxField != null)
                    {
                        if (altTaxField.ValueCurrency != null)
                        {
                            result.GstAmount = (decimal)altTaxField.ValueCurrency.Amount;
                        }
                        else if (decimal.TryParse(altTaxField.Content?.Replace("$", "").Trim(), out var parsedAltTax))
                        {
                            result.GstAmount = parsedAltTax;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing receipt with Azure Document Intelligence");
            }

            return result;
        }
    }
}
