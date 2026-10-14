using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NHN.Power.API.DTOs.Ledger;

namespace NHN.Power.API.Services
{
    public interface ILedgerService
    {
        Task<IEnumerable<LedgerBusinessDto>> GetActiveBusinessesAsync();
        Task<ImportHistoryResultDto> ImportHistoricalExcelAsync(Stream fileStream, string? business = null);
        Task<ImportCsvResultDto> ImportBankCsvAsync(Stream fileStream, string? business = null);
        Task<int> AutoMatchReceiptsForQuarterAsync(Guid quarterId);
        Task<IEnumerable<LedgerQuarterSummaryDto>> GetQuartersSummaryAsync(string? business = null);
        Task<IEnumerable<LedgerTransactionDto>> GetQuarterTransactionsAsync(Guid quarterId);
        Task<LedgerTransactionDto?> UpdateTransactionAsync(Guid id, UpdateLedgerTransactionDto dto);
        Task<bool> DeleteTransactionAsync(Guid id);
        Task<bool> FinalizeQuarterAsync(Guid quarterId);
        Task<bool> DeleteQuarterAsync(Guid quarterId);
        Task<byte[]> ExportQuarterExcelAsync(Guid quarterId);

        Task<IEnumerable<LedgerRuleDto>> GetAllRulesAsync();
        Task<LedgerRuleDto> CreateOrUpdateRuleAsync(CreateLedgerRuleDto dto);
        Task<LedgerRuleDto> UpdateRuleAsync(Guid id, CreateLedgerRuleDto dto);
        Task<bool> DeleteRuleAsync(Guid id);
    }
}
