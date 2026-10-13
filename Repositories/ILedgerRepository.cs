using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NHN.Power.API.Models;

namespace NHN.Power.API.Repositories
{
    public interface ILedgerRepository
    {
        // Businesses
        Task<IEnumerable<NhnBusiness>> GetActiveBusinessesAsync();

        // Quarters
        Task<IEnumerable<NhnLedgerQuarter>> GetAllQuartersAsync(string? business = null);
        Task<NhnLedgerQuarter?> GetQuarterByIdAsync(Guid id);
        Task<NhnLedgerQuarter?> GetQuarterByYearQuarterAsync(string yearQuarter);
        Task<NhnLedgerQuarter?> GetQuarterByBusinessAndYearQuarterAsync(string business, string yearQuarter);
        Task<NhnLedgerQuarter> AddQuarterAsync(NhnLedgerQuarter quarter);
        Task UpdateQuarterAsync(NhnLedgerQuarter quarter);
        Task DeleteQuarterAsync(Guid id);

        // Transactions
        Task<IEnumerable<NhnLedgerTransaction>> GetTransactionsByQuarterIdAsync(Guid quarterId);
        Task<NhnLedgerTransaction?> GetTransactionByIdAsync(Guid id);
        Task AddTransactionsAsync(IEnumerable<NhnLedgerTransaction> transactions);
        Task UpdateTransactionAsync(NhnLedgerTransaction transaction);
        Task UpdateTransactionsAsync(IEnumerable<NhnLedgerTransaction> transactions);
        Task DeleteTransactionAsync(Guid id);
        Task DeleteTransactionsByQuarterIdAsync(Guid quarterId);

        // Rules
        Task<IEnumerable<NhnLedgerRule>> GetAllRulesAsync();
        Task<NhnLedgerRule?> GetRuleByKeywordAsync(string keyword);
        Task<NhnLedgerRule> AddRuleAsync(NhnLedgerRule rule);
        Task AddRulesAsync(IEnumerable<NhnLedgerRule> rules);
        Task UpdateRuleAsync(NhnLedgerRule rule);
        Task UpdateRulesAsync(IEnumerable<NhnLedgerRule> rules);
        Task DeleteRuleAsync(Guid id);
    }
}
