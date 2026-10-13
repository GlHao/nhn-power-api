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

        public async Task<IEnumerable<LedgerBusinessDto>> GetActiveBusinessesAsync()
        {
            var businesses = await _ledgerRepository.GetActiveBusinessesAsync();
            return businesses.Select(b => new LedgerBusinessDto
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                Description = b.Description,
                SortOrder = b.SortOrder
            });
        }

        public async Task<ImportHistoryResultDto> ImportHistoricalExcelAsync(Stream fileStream, string? business = null)
        {
            business = string.IsNullOrWhiteSpace(business) ? "NHN POWER" : business.Trim();
            var result = new ImportHistoryResultDto();

            // 1. Identify sheets and find the sheet that contains transaction descriptions
            var sheetNames = fileStream.GetSheetNames()?.ToList() ?? new List<string>();
            List<object> rows = new List<object>();

            // Prioritize sheets named 'CSVData', 'Transactions', 'Ledger', or any sheet having actual data
            string? targetSheet = sheetNames.FirstOrDefault(s => 
                s.Equals("CSVData", StringComparison.OrdinalIgnoreCase) || 
                s.Equals("Transactions", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Data", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(targetSheet))
            {
                fileStream.Position = 0;
                rows = fileStream.Query(sheetName: targetSheet, useHeaderRow: true).ToList();
            }
            else
            {
                // Iterate sheets to find the one with BANK DESCRIPTION / 说明
                foreach (var sheet in sheetNames)
                {
                    fileStream.Position = 0;
                    var candidateRows = fileStream.Query(sheetName: sheet, useHeaderRow: true).ToList();
                    if (candidateRows.Any())
                    {
                        var firstRow = candidateRows.First() as IDictionary<string, object>;
                        if (firstRow != null && firstRow.Keys.Any(k => k.Contains("DESCRIPTION", StringComparison.OrdinalIgnoreCase) || k.Contains("说明", StringComparison.OrdinalIgnoreCase)))
                        {
                            rows = candidateRows;
                            break;
                        }
                    }
                }

                // Fallback to default query if no specific sheet matched
                if (!rows.Any())
                {
                    fileStream.Position = 0;
                    rows = fileStream.Query(useHeaderRow: true).ToList();
                }
            }

            // 2. Preload existing rules into memory dictionary (Case-insensitive)
            var existingRulesList = (await _ledgerRepository.GetAllRulesAsync()).ToList();
            var existingRulesMap = existingRulesList.ToDictionary(r => r.Keyword.Trim().ToUpperInvariant(), r => r);

            var newRulesToAdd = new Dictionary<string, NhnLedgerRule>(StringComparer.OrdinalIgnoreCase);
            var rulesToUpdate = new Dictionary<string, NhnLedgerRule>(StringComparer.OrdinalIgnoreCase);

            // 3. Process rows in memory: both rule extraction and transaction parsing
            var parsedTransactions = new List<(DateTime Date, decimal Amount, decimal Gst, string BankDesc, decimal? Balance, string Note)>();

            foreach (IDictionary<string, object> row in rows)
            {
                result.TotalRowsParsed++;

                string bankDesc = GetColumnValue(row, "BANK DESCRIPTION", "Description", "bank_description");
                string note = GetColumnValue(row, "说明", "Notes", "category_note", "Category");
                string dateStr = GetColumnValue(row, "DATE", "Date", "transaction_date");
                string amountStr = GetColumnValue(row, "AMOUNT", "Amount");
                string gstStr = GetColumnValue(row, "GST", "Gst", "gst_amount");
                string balanceStr = GetColumnValue(row, "BALANCE", "Balance");

                bankDesc = bankDesc?.Trim() ?? string.Empty;
                note = note?.Trim() ?? string.Empty;

                // Extract rule if bankDesc and note are present
                if (!string.IsNullOrWhiteSpace(bankDesc) && !string.IsNullOrWhiteSpace(note))
                {
                    string keyword = ExtractCoreKeyword(bankDesc);
                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        string keyUpper = keyword.ToUpperInvariant();

                        if (existingRulesMap.TryGetValue(keyUpper, out var existingRule))
                        {
                            if (existingRule.CategoryNote != note)
                            {
                                existingRule.CategoryNote = note;
                                rulesToUpdate[keyUpper] = existingRule;
                            }
                        }
                        else if (newRulesToAdd.TryGetValue(keyUpper, out var pendingNewRule))
                        {
                            pendingNewRule.CategoryNote = note;
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
                            newRulesToAdd[keyUpper] = newRule;
                        }
                    }
                }

                // If row has valid date and amount, collect for Quarter ledger creation
                if (!string.IsNullOrWhiteSpace(dateStr) && !string.IsNullOrWhiteSpace(amountStr))
                {
                    if (TryParseDate(dateStr, out DateTime txDate) &&
                        decimal.TryParse(CleanAmountString(amountStr), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal txAmount))
                    {
                        decimal txGst = 0m;
                        if (!string.IsNullOrWhiteSpace(gstStr))
                        {
                            decimal.TryParse(CleanAmountString(gstStr), NumberStyles.Any, CultureInfo.InvariantCulture, out txGst);
                        }

                        // Ensure GST sign matches Amount direction
                        if (txAmount < 0 && txGst > 0) txGst = -txGst;
                        else if (txAmount > 0 && txGst < 0) txGst = Math.Abs(txGst);

                        decimal? txBalance = null;
                        if (!string.IsNullOrWhiteSpace(balanceStr) &&
                            decimal.TryParse(CleanAmountString(balanceStr), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal bal))
                        {
                            txBalance = bal;
                        }

                        parsedTransactions.Add((txDate, txAmount, txGst, bankDesc, txBalance, note));
                    }
                }
            }

            // 4. Batch persist rules to database
            if (newRulesToAdd.Any())
            {
                await _ledgerRepository.AddRulesAsync(newRulesToAdd.Values);
                result.RulesLearned = newRulesToAdd.Count;
            }

            if (rulesToUpdate.Any())
            {
                await _ledgerRepository.UpdateRulesAsync(rulesToUpdate.Values);
                result.RulesUpdated = rulesToUpdate.Count;
            }

            // 5. Automatically create or populate the Quarter ledger card with transactions
            if (parsedTransactions.Any())
            {
                var sampleDate = parsedTransactions.First().Date;
                string yearQuarter = GetYearQuarter(sampleDate);

                var quarter = await _ledgerRepository.GetQuarterByBusinessAndYearQuarterAsync(business, yearQuarter);
                if (quarter == null)
                {
                    quarter = await _ledgerRepository.AddQuarterAsync(new NhnLedgerQuarter
                    {
                        Id = Guid.NewGuid(),
                        Business = business,
                        YearQuarter = yearQuarter,
                        Status = "draft",
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }

                // Clean previous transactions for this quarter to prevent duplicates
                await _ledgerRepository.DeleteTransactionsByQuarterIdAsync(quarter.Id);

                var newTxEntities = parsedTransactions.Select(t => new NhnLedgerTransaction
                {
                    Id = Guid.NewGuid(),
                    QuarterId = quarter.Id,
                    TransactionDate = t.Date,
                    Amount = t.Amount,
                    GstAmount = t.Gst,
                    BankDescription = t.BankDesc,
                    Balance = t.Balance,
                    Notes = string.IsNullOrWhiteSpace(t.Note) ? null : t.Note,
                    Status = !string.IsNullOrWhiteSpace(t.Note) ? "rule_matched" : "uncategorized",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }).ToList();

                await _ledgerRepository.AddTransactionsAsync(newTxEntities);
            }

            // 6. Return updated rules
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

        public async Task<ImportCsvResultDto> ImportBankCsvAsync(Stream fileStream, string? business = null)
        {
            business = string.IsNullOrWhiteSpace(business) ? "NHN POWER" : business.Trim();
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

            // Determine Quarter by grouping all transactions to find the dominant quarter
            string yearQuarter = parsedTxList
                .GroupBy(t => GetYearQuarter(t.Date))
                .OrderByDescending(g => g.Count())
                .First().Key;

            var quarter = await _ledgerRepository.GetQuarterByBusinessAndYearQuarterAsync(business, yearQuarter);
            if (quarter == null)
            {
                quarter = await _ledgerRepository.AddQuarterAsync(new NhnLedgerQuarter
                {
                    Id = Guid.NewGuid(),
                    Business = business,
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
                Business = business,
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

            // Pre-load all pending receipts once to avoid N roundtrips over remote network
            var pendingReceipts = (await _receiptRepository.GetAllAsync("pending"))
                .Where(r => r.TransactionDate.HasValue)
                .ToList();

            if (!pendingReceipts.Any()) return 0;

            int matchCount = 0;
            var matchedReceiptsSet = new HashSet<Guid>();
            var txsToUpdate = new List<NhnLedgerTransaction>();
            var receiptsToUpdate = new List<NhnReceipt>();

            foreach (var tx in transactions)
            {
                if (tx.Status == "completed") continue;

                decimal targetAmount = Math.Abs(tx.Amount);
                var minDate = tx.TransactionDate.Date.AddDays(-3);
                var maxDate = tx.TransactionDate.Date.AddDays(3);

                // Find candidate pending receipts not already matched in this run
                var candidate = pendingReceipts
                    .Where(r => !matchedReceiptsSet.Contains(r.Id)
                                && r.TransactionDate.HasValue
                                && r.TransactionDate.Value.Date >= minDate
                                && r.TransactionDate.Value.Date <= maxDate
                                && Math.Abs(r.TotalAmount - targetAmount) < 0.01m)
                    .OrderBy(r => Math.Abs((tx.TransactionDate - r.TransactionDate!.Value).TotalDays))
                    .FirstOrDefault();

                if (candidate != null)
                {
                    matchedReceiptsSet.Add(candidate.Id);

                    // Update Transaction in memory
                    tx.GstAmount = tx.Amount < 0 ? -Math.Abs(candidate.GstAmount) : Math.Abs(candidate.GstAmount);
                    tx.MatchedReceiptId = candidate.Id;
                    tx.Status = "receipt_matched";
                    txsToUpdate.Add(tx);

                    // Update Receipt in memory
                    candidate.Status = "matched";
                    candidate.MatchedTransactionId = tx.Id;
                    receiptsToUpdate.Add(candidate);

                    matchCount++;
                }
            }

            // Batch save once
            if (txsToUpdate.Any())
            {
                await _ledgerRepository.UpdateTransactionsAsync(txsToUpdate);
            }
            if (receiptsToUpdate.Any())
            {
                await _receiptRepository.UpdateRangeAsync(receiptsToUpdate);
            }

            return matchCount;
        }

        public async Task<IEnumerable<LedgerQuarterSummaryDto>> GetQuartersSummaryAsync(string? business = null)
        {
            var quarters = await _ledgerRepository.GetAllQuartersAsync(business);
            var list = new List<LedgerQuarterSummaryDto>();

            foreach (var q in quarters)
            {
                var txs = (await _ledgerRepository.GetTransactionsByQuarterIdAsync(q.Id)).ToList();

                list.Add(new LedgerQuarterSummaryDto
                {
                    Id = q.Id,
                    Business = q.Business,
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
            string business = quarter?.Business ?? "NHN POWER";

            var txs = await _ledgerRepository.GetTransactionsByQuarterIdAsync(quarterId);
            return txs.Select(t => new LedgerTransactionDto
            {
                Id = t.Id,
                QuarterId = t.QuarterId,
                Business = business,
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
                Business = quarter?.Business ?? "NHN POWER",
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

        public async Task<bool> DeleteTransactionAsync(Guid id)
        {
            var tx = await _ledgerRepository.GetTransactionByIdAsync(id);
            if (tx == null) return false;

            await _ledgerRepository.DeleteTransactionAsync(id);
            return true;
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

        public async Task<bool> DeleteQuarterAsync(Guid quarterId)
        {
            var quarter = await _ledgerRepository.GetQuarterByIdAsync(quarterId);
            if (quarter == null) return false;

            await _ledgerRepository.DeleteQuarterAsync(quarterId);
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
            if (string.IsNullOrWhiteSpace(bankDesc)) return string.Empty;

            // 1. High priority common keywords
            string[] knownEntities = {
                "7-ELEVEN", "PAYROLLER", "EASTLINK", "TOYOTA FINANCE", "POINT PARKING",
                "RACV", "TAX OFFICE", "BPAY", "WOOLWORTHS", "COLES", "BUNNINGS", "OFFICEWORKS",
                "MATE COMMUNICATE", "ROARING BEAR", "I-MED RADIOLOGY", "CALTEX", "AMPOL", "BP ",
                "DIRECTOR", "RENT", "SALARY", "WAGE"
            };

            foreach (var entity in knownEntities)
            {
                if (bankDesc.Contains(entity, StringComparison.OrdinalIgnoreCase))
                {
                    return entity.Trim();
                }
            }

            // 2. Handle Australian banking prefixes: "Fast Transfer From ...", "Transfer to ...", "Direct Debit ..."
            string text = bankDesc.Trim();
            string[] noisePrefixes = {
                "Fast Transfer From ", "Fast Transfer To ", "Transfer from ", "Transfer to ",
                "Direct Debit ", "Direct Credit ", "Internet Transfer ", "Withdrawal "
            };

            foreach (var p in noisePrefixes)
            {
                if (text.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Substring(p.Length).Trim();
                    break;
                }
            }

            // 3. If after removing noise prefix we still have words, take first meaningful 1-2 words
            var tokens = text.Split(new[] { ' ', '/', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                             .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 1 && !t.StartsWith("xx", StringComparison.OrdinalIgnoreCase))
                             .Take(2)
                             .ToList();

            if (tokens.Any())
            {
                return string.Join(" ", tokens);
            }

            return bankDesc.Trim();
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
