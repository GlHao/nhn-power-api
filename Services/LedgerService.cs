using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using NHN.Power.API.DTOs.Ledger;
using NHN.Power.API.Models;
using NHN.Power.API.Repositories;

namespace NHN.Power.API.Services
{
    public class LedgerService : ILedgerService
    {
        private readonly ILedgerRepository _ledgerRepository;
        private readonly IReceiptRepository _receiptRepository;
        private readonly IStorageService _storageService;
        private readonly ILogger<LedgerService> _logger;

        public LedgerService(
            ILedgerRepository ledgerRepository,
            IReceiptRepository receiptRepository,
            IStorageService storageService,
            ILogger<LedgerService> logger)
        {
            _ledgerRepository = ledgerRepository;
            _receiptRepository = receiptRepository;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<ImportHistoryResultDto> ImportHistoricalExcelAsync(Stream fileStream)
        {
            var result = new ImportHistoryResultDto();
            var rows = fileStream.Query(useHeaderRow: true).ToList();

            int rulesLearned = 0;
            int rulesUpdated = 0;

            foreach (IDictionary<string, object> row in rows)
            {
                result.TotalRowsParsed++;

                string bankDesc = GetColumnValue(row, "BANK DESCRIPTION", "Description", "bank_description");
                string note = GetColumnValue(row, "说明", "Notes", "category_note", "Category");

                if (string.IsNullOrWhiteSpace(bankDesc) || string.IsNullOrWhiteSpace(note))
                    continue;

                bankDesc = bankDesc.Trim();
                note = note.Trim();

                // Extract core keyword from Bank Description
                string keyword = ExtractCoreKeyword(bankDesc);
                if (string.IsNullOrWhiteSpace(keyword)) continue;

                var existingRule = await _ledgerRepository.GetRuleByKeywordAsync(keyword);
                if (existingRule != null)
                {
                    existingRule.CategoryNote = note;
                    await _ledgerRepository.UpdateRuleAsync(existingRule);
                    rulesUpdated++;
                }
                else
                {
                    var newRule = new NhnLedgerRule
                    {
                        Id = Guid.NewGuid(),
                        Keyword = keyword,
                        CategoryNote = note,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    await _ledgerRepository.AddRuleAsync(newRule);
                    rulesLearned++;
                }
            }

            result.RulesLearned = rulesLearned;
            result.RulesUpdated = rulesUpdated;

            var allRules = await _ledgerRepository.GetAllRulesAsync();
            result.Rules = allRules.Select(r => new LedgerRuleDto
            {
                Id = r.Id,
                Keyword = r.Keyword,
                CategoryNote = r.CategoryNote,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return result;
        }

        public async Task<ImportCsvResultDto> ImportBankCsvAsync(Stream fileStream)
        {
            using var reader = new StreamReader(fileStream, Encoding.UTF8);
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line.Trim());
                }
            }

            var parsedTxList = new List<(DateTime Date, decimal Amount, string Description, decimal? Balance)>();

            foreach (var line in lines)
            {
                var parts = ParseCsvLine(line);
                if (parts.Count < 3) continue;

                // Check header
                if (parts[0].Equals("Date", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("DATE", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!TryParseDate(parts[0], out DateTime txDate)) continue;
                if (!decimal.TryParse(CleanAmountString(parts[1]), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount)) continue;

                string description = parts[2].Trim();
                decimal? balance = null;
                if (parts.Count >= 4 && decimal.TryParse(CleanAmountString(parts[3]), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal bal))
                {
                    balance = bal;
                }

                parsedTxList.Add((txDate, amount, description, balance));
            }

            if (!parsedTxList.Any())
            {
                throw new ArgumentException("No valid transaction rows found in CSV.");
            }

            // Determine Quarter from the first transaction
            var sampleDate = parsedTxList.First().Date;
            string yearQuarter = GetYearQuarter(sampleDate);

            var quarter = await _ledgerRepository.GetQuarterByYearQuarterAsync(yearQuarter);
            if (quarter == null)
            {
                quarter = await _ledgerRepository.AddQuarterAsync(new NhnLedgerQuarter
                {
                    Id = Guid.NewGuid(),
                    YearQuarter = yearQuarter,
                    Status = "draft",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }

            // Clean previous draft transactions for this quarter
            await _ledgerRepository.DeleteTransactionsByQuarterIdAsync(quarter.Id);

            // Fetch rules for keyword matching (sorted by Keyword.Length DESC)
            var rules = (await _ledgerRepository.GetAllRulesAsync())
                .OrderByDescending(r => r.Keyword.Length)
                .ToList();

            var newTransactions = new List<NhnLedgerTransaction>();

            int ruleMatchedCount = 0;
            int uncategorizedCount = 0;

            foreach (var item in parsedTxList)
            {
                var tx = new NhnLedgerTransaction
                {
                    Id = Guid.NewGuid(),
                    QuarterId = quarter.Id,
                    TransactionDate = item.Date,
                    Amount = item.Amount,
                    GstAmount = 0.00m,
                    BankDescription = item.Description,
                    Balance = item.Balance,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                // Apply Rule Engine
                var matchedRule = rules.FirstOrDefault(r => item.Description.Contains(r.Keyword, StringComparison.OrdinalIgnoreCase));
                if (matchedRule != null)
                {
                    tx.Notes = matchedRule.CategoryNote;
                    tx.Status = "rule_matched";
                    ruleMatchedCount++;
                }
                else
                {
                    tx.Notes = null;
                    tx.Status = "uncategorized";
                    uncategorizedCount++;
                }

                newTransactions.Add(tx);
            }

            await _ledgerRepository.AddTransactionsAsync(newTransactions);

            // Run Receipt Auto-Matching
            int receiptMatchedCount = await AutoMatchReceiptsForQuarterAsync(quarter.Id);

            return new ImportCsvResultDto
            {
                YearQuarter = yearQuarter,
                QuarterId = quarter.Id,
                TotalTransactions = newTransactions.Count,
                RuleMatchedCount = ruleMatchedCount,
                ReceiptMatchedCount = receiptMatchedCount,
                UncategorizedCount = uncategorizedCount - receiptMatchedCount
            };
        }

        public async Task<int> AutoMatchReceiptsForQuarterAsync(Guid quarterId)
        {
            var transactions = (await _ledgerRepository.GetTransactionsByQuarterIdAsync(quarterId)).ToList();
            if (!transactions.Any()) return 0;

            int matchCount = 0;

            foreach (var tx in transactions)
            {
                if (tx.Status == "completed") continue;

                // Match condition: Amount matches Math.Abs(tx.Amount) and TransactionDate within ±3 days
                decimal targetAmount = Math.Abs(tx.Amount);
                DateTime startDate = tx.TransactionDate.AddDays(-3);
                DateTime endDate = tx.TransactionDate.AddDays(3);

                var pendingReceipts = (await _receiptRepository.GetPendingReceiptsForMatchingAsync(startDate, endDate, targetAmount))
                    .OrderBy(r => Math.Abs((tx.TransactionDate - (r.TransactionDate ?? tx.TransactionDate)).TotalDays))
                    .ToList();

                if (pendingReceipts.Any())
                {
                    var matchedReceipt = pendingReceipts.First();

                    // Update Transaction
                    tx.GstAmount = matchedReceipt.GstAmount;
                    tx.MatchedReceiptId = matchedReceipt.Id;
                    tx.Status = "receipt_matched";
                    await _ledgerRepository.UpdateTransactionAsync(tx);

                    // Update Receipt
                    matchedReceipt.Status = "matched";
                    matchedReceipt.MatchedTransactionId = tx.Id;
                    await _receiptRepository.UpdateAsync(matchedReceipt);

                    matchCount++;
                }
            }

            return matchCount;
        }

        public async Task<IEnumerable<LedgerQuarterSummaryDto>> GetQuartersSummaryAsync()
        {
            var quarters = await _ledgerRepository.GetAllQuartersAsync();
            var list = new List<LedgerQuarterSummaryDto>();

            foreach (var q in quarters)
            {
                var txs = (await _ledgerRepository.GetTransactionsByQuarterIdAsync(q.Id)).ToList();

                list.Add(new LedgerQuarterSummaryDto
                {
                    Id = q.Id,
                    YearQuarter = q.YearQuarter,
                    Status = q.Status,
                    TotalTransactions = txs.Count,
                    TotalAmount = txs.Sum(t => t.Amount),
                    TotalGst = txs.Sum(t => t.GstAmount),
                    UncategorizedCount = txs.Count(t => t.Status == "uncategorized"),
                    RuleMatchedCount = txs.Count(t => t.Status == "rule_matched"),
                    ReceiptMatchedCount = txs.Count(t => t.Status == "receipt_matched"),
                    CreatedAt = q.CreatedAt,
                    UpdatedAt = q.UpdatedAt
                });
            }

            return list;
        }

        public async Task<IEnumerable<LedgerTransactionDto>> GetQuarterTransactionsAsync(Guid quarterId)
        {
            var quarter = await _ledgerRepository.GetQuarterByIdAsync(quarterId);
            string yearQuarter = quarter?.YearQuarter ?? string.Empty;

            var txs = await _ledgerRepository.GetTransactionsByQuarterIdAsync(quarterId);
            return txs.Select(t => new LedgerTransactionDto
            {
                Id = t.Id,
                QuarterId = t.QuarterId,
                YearQuarter = yearQuarter,
                TransactionDate = t.TransactionDate,
                Amount = t.Amount,
                GstAmount = t.GstAmount,
                BankDescription = t.BankDescription,
                Balance = t.Balance,
                Notes = t.Notes,
                Status = t.Status,
                MatchedReceiptId = t.MatchedReceiptId,
                MatchedReceiptMerchant = t.MatchedReceipt?.MerchantName,
                MatchedReceiptUrl = t.MatchedReceipt?.R2Url,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            });
        }

        public async Task<LedgerTransactionDto?> UpdateTransactionAsync(Guid id, UpdateLedgerTransactionDto dto)
        {
            var tx = await _ledgerRepository.GetTransactionByIdAsync(id);
            if (tx == null) return null;

            if (dto.Notes != null) tx.Notes = dto.Notes;
            if (dto.GstAmount.HasValue) tx.GstAmount = dto.GstAmount.Value;
            if (dto.MatchedReceiptId.HasValue) tx.MatchedReceiptId = dto.MatchedReceiptId.Value;
            if (dto.Status != null) tx.Status = dto.Status;

            await _ledgerRepository.UpdateTransactionAsync(tx);

            var quarter = await _ledgerRepository.GetQuarterByIdAsync(tx.QuarterId);
            return new LedgerTransactionDto
            {
                Id = tx.Id,
                QuarterId = tx.QuarterId,
                YearQuarter = quarter?.YearQuarter ?? string.Empty,
                TransactionDate = tx.TransactionDate,
                Amount = tx.Amount,
                GstAmount = tx.GstAmount,
                BankDescription = tx.BankDescription,
                Balance = tx.Balance,
                Notes = tx.Notes,
                Status = tx.Status,
                MatchedReceiptId = tx.MatchedReceiptId,
                MatchedReceiptMerchant = tx.MatchedReceipt?.MerchantName,
                MatchedReceiptUrl = tx.MatchedReceipt?.R2Url,
                CreatedAt = tx.CreatedAt,
                UpdatedAt = tx.UpdatedAt
            };
        }

        public async Task<bool> FinalizeQuarterAsync(Guid quarterId)
        {
            var quarter = await _ledgerRepository.GetQuarterByIdAsync(quarterId);
            if (quarter == null) return false;

            quarter.Status = "completed";
            await _ledgerRepository.UpdateQuarterAsync(quarter);

            var txs = await _ledgerRepository.GetTransactionsByQuarterIdAsync(quarterId);
            foreach (var tx in txs)
            {
                tx.Status = "completed";
                await _ledgerRepository.UpdateTransactionAsync(tx);
            }

            return true;
        }

        public async Task<byte[]> ExportQuarterExcelAsync(Guid quarterId)
        {
            var txs = await _ledgerRepository.GetTransactionsByQuarterIdAsync(quarterId);

            var exportData = txs.Select(t => new
            {
                DATE = t.TransactionDate.ToString("dd/MM/yyyy"),
                AMOUNT = t.Amount,
                GST = t.GstAmount,
                BANK_DESCRIPTION = t.BankDescription,
                BALANCE = t.Balance.HasValue ? t.Balance.Value.ToString("F2") : "",
                说明 = t.Notes ?? ""
            }).ToList();

            using var memoryStream = new MemoryStream();
            await memoryStream.SaveAsAsync(exportData);
            return memoryStream.ToArray();
        }

        public async Task<IEnumerable<LedgerRuleDto>> GetAllRulesAsync()
        {
            var rules = await _ledgerRepository.GetAllRulesAsync();
            return rules.Select(r => new LedgerRuleDto
            {
                Id = r.Id,
                Keyword = r.Keyword,
                CategoryNote = r.CategoryNote,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            });
        }

        public async Task<LedgerRuleDto> CreateOrUpdateRuleAsync(CreateLedgerRuleDto dto)
        {
            string keyword = dto.Keyword.Trim();
            string categoryNote = dto.CategoryNote.Trim();

            var existing = await _ledgerRepository.GetRuleByKeywordAsync(keyword);
            if (existing != null)
            {
                existing.CategoryNote = categoryNote;
                await _ledgerRepository.UpdateRuleAsync(existing);
                return new LedgerRuleDto
                {
                    Id = existing.Id,
                    Keyword = existing.Keyword,
                    CategoryNote = existing.CategoryNote,
                    CreatedAt = existing.CreatedAt,
                    UpdatedAt = existing.UpdatedAt
                };
            }

            var rule = new NhnLedgerRule
            {
                Id = Guid.NewGuid(),
                Keyword = keyword,
                CategoryNote = categoryNote,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _ledgerRepository.AddRuleAsync(rule);
            return new LedgerRuleDto
            {
                Id = rule.Id,
                Keyword = rule.Keyword,
                CategoryNote = rule.CategoryNote,
                CreatedAt = rule.CreatedAt,
                UpdatedAt = rule.UpdatedAt
            };
        }

        public async Task<bool> DeleteRuleAsync(Guid id)
        {
            await _ledgerRepository.DeleteRuleAsync(id);
            return true;
        }

        #region Helpers
        private string GetColumnValue(IDictionary<string, object> row, params string[] possibleKeys)
        {
            foreach (var key in possibleKeys)
            {
                var matchedKey = row.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (matchedKey != null && row[matchedKey] != null)
                {
                    return row[matchedKey].ToString() ?? string.Empty;
                }
            }
            return string.Empty;
        }

        private string ExtractCoreKeyword(string bankDesc)
        {
            // Clean up merchant keyword from bank description
            // Examples: "7-ELEVEN 1283 FRANKSTON AU Card xx1241..." -> "7-ELEVEN"
            // "PAYROLLER.COM.AU" -> "PAYROLLER"
            // "TOYOTA FINANCE 13886075" -> "TOYOTA FINANCE"
            // "Transfer to xx8309 CommBank app director" -> "director"
            if (bankDesc.Contains("director", StringComparison.OrdinalIgnoreCase)) return "director";
            if (bankDesc.Contains("RENT", StringComparison.OrdinalIgnoreCase)) return "RENT";
            if (bankDesc.Contains("PAYROLLER", StringComparison.OrdinalIgnoreCase)) return "PAYROLLER";
            if (bankDesc.Contains("EASTLINK", StringComparison.OrdinalIgnoreCase)) return "EASTLINK";
            if (bankDesc.Contains("TOYOTA FINANCE", StringComparison.OrdinalIgnoreCase)) return "TOYOTA FINANCE";
            if (bankDesc.Contains("POINT PARKING", StringComparison.OrdinalIgnoreCase)) return "POINT PARKING";
            if (bankDesc.Contains("RACV", StringComparison.OrdinalIgnoreCase)) return "RACV";
            if (bankDesc.Contains("TAX OFFICE", StringComparison.OrdinalIgnoreCase)) return "TAX OFFICE";

            var firstToken = bankDesc.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return firstToken ?? bankDesc;
        }

        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result;
        }

        private bool TryParseDate(string input, out DateTime date)
        {
            input = input.Trim().Replace("\"", "");
            string[] formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy" };
            return DateTime.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                   || DateTime.TryParse(input, out date);
        }

        private string CleanAmountString(string amountStr)
        {
            return amountStr.Trim().Replace("\"", "").Replace("+", "").Replace("$", "").Replace(",", "");
        }

        private string GetYearQuarter(DateTime date)
        {
            int quarter = (date.Month - 1) / 3 + 1;
            return $"{date.Year}Q{quarter}";
        }
        #endregion
    }
}
