using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NHN.Power.API.Models;

namespace NHN.Power.API.Repositories
{
    public interface ILedgerRepository
    {
        // Quarters
        Task<IEnumerable<NhnLedgerQuarter>> GetAllQuartersAsync();
        Task<NhnLedgerQuarter?> GetQuarterByIdAsync(Guid id);
        Task<NhnLedgerQuarter?> GetQuarterByYearQuarterAsync(string yearQuarter);
        Task<NhnLedgerQuarter> AddQuarterAsync(NhnLedgerQuarter quarter);
        Task UpdateQuarterAsync(NhnLedgerQuarter quarter);

        // Transactions
        Task<IEnumerable<NhnLedgerTransaction>> GetTransactionsByQuarterIdAsync(Guid quarterId);
        Task<NhnLedgerTransaction?> GetTransactionByIdAsync(Guid id);
        Task AddTransactionsAsync(IEnumerable<NhnLedgerTransaction> transactions);
        Task UpdateTransactionAsync(NhnLedgerTransaction transaction);
        Task DeleteTransactionsByQuarterIdAsync(Guid quarterId);

        // Rules
        Task<IEnumerable<NhnLedgerRule>> GetAllRulesAsync();
        Task<NhnLedgerRule?> GetRuleByKeywordAsync(string keyword);
        Task<NhnLedgerRule> AddRuleAsync(NhnLedgerRule rule);
        Task UpdateRuleAsync(NhnLedgerRule rule);
        Task DeleteRuleAsync(Guid id);
    }
}
