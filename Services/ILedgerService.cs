using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NHN.Power.API.DTOs.Ledger;

namespace NHN.Power.API.Services
{
    public interface ILedgerService
    {
        Task<ImportHistoryResultDto> ImportHistoricalExcelAsync(Stream fileStream);
        Task<ImportCsvResultDto> ImportBankCsvAsync(Stream fileStream);
        Task<int> AutoMatchReceiptsForQuarterAsync(Guid quarterId);
        Task<IEnumerable<LedgerQuarterSummaryDto>> GetQuartersSummaryAsync();
        Task<IEnumerable<LedgerTransactionDto>> GetQuarterTransactionsAsync(Guid quarterId);
        Task<LedgerTransactionDto?> UpdateTransactionAsync(Guid id, UpdateLedgerTransactionDto dto);
        Task<bool> FinalizeQuarterAsync(Guid quarterId);
        Task<byte[]> ExportQuarterExcelAsync(Guid quarterId);

        Task<IEnumerable<LedgerRuleDto>> GetAllRulesAsync();
        Task<LedgerRuleDto> CreateOrUpdateRuleAsync(CreateLedgerRuleDto dto);
        Task<bool> DeleteRuleAsync(Guid id);
    }
}
