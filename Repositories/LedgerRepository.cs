using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NHN.Power.API.Data;
using NHN.Power.API.Models;

namespace NHN.Power.API.Repositories
{
    public class LedgerRepository : ILedgerRepository
    {
        private readonly AppDbContext _context;

        public LedgerRepository(AppDbContext context)
        {
            _context = context;
        }

        // Quarters
        public async Task<IEnumerable<NhnLedgerQuarter>> GetAllQuartersAsync()
        {
            return await _context.LedgerQuarters
                .AsNoTracking()
                .OrderByDescending(q => q.YearQuarter)
                .ToListAsync();
        }

        public async Task<NhnLedgerQuarter?> GetQuarterByIdAsync(Guid id)
        {
            return await _context.LedgerQuarters.FirstOrDefaultAsync(q => q.Id == id);
        }

        public async Task<NhnLedgerQuarter?> GetQuarterByYearQuarterAsync(string yearQuarter)
        {
            return await _context.LedgerQuarters.FirstOrDefaultAsync(q => q.YearQuarter == yearQuarter);
        }

        public async Task<NhnLedgerQuarter> AddQuarterAsync(NhnLedgerQuarter quarter)
        {
            if (quarter.Id == Guid.Empty) quarter.Id = Guid.NewGuid();
            quarter.CreatedAt = DateTimeOffset.UtcNow;
            quarter.UpdatedAt = DateTimeOffset.UtcNow;

            _context.LedgerQuarters.Add(quarter);
            await _context.SaveChangesAsync();
            return quarter;
        }

        public async Task UpdateQuarterAsync(NhnLedgerQuarter quarter)
        {
            quarter.UpdatedAt = DateTimeOffset.UtcNow;
            _context.LedgerQuarters.Update(quarter);
            await _context.SaveChangesAsync();
        }

        // Transactions
        public async Task<IEnumerable<NhnLedgerTransaction>> GetTransactionsByQuarterIdAsync(Guid quarterId)
        {
            return await _context.LedgerTransactions
                .AsNoTracking()
                .Include(t => t.MatchedReceipt)
                .Where(t => t.QuarterId == quarterId)
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<NhnLedgerTransaction?> GetTransactionByIdAsync(Guid id)
        {
            return await _context.LedgerTransactions
                .Include(t => t.MatchedReceipt)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task AddTransactionsAsync(IEnumerable<NhnLedgerTransaction> transactions)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var tx in transactions)
            {
                if (tx.Id == Guid.Empty) tx.Id = Guid.NewGuid();
                tx.CreatedAt = now;
                tx.UpdatedAt = now;
            }

            await _context.LedgerTransactions.AddRangeAsync(transactions);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateTransactionAsync(NhnLedgerTransaction transaction)
        {
            transaction.UpdatedAt = DateTimeOffset.UtcNow;
            var tracked = await _context.LedgerTransactions.FindAsync(transaction.Id);
            if (tracked != null && tracked != transaction)
            {
                _context.Entry(tracked).CurrentValues.SetValues(transaction);
                tracked.MatchedReceiptId = transaction.MatchedReceiptId;
                tracked.Status = transaction.Status;
                tracked.GstAmount = transaction.GstAmount;
                tracked.Notes = transaction.Notes;
            }
            else
            {
                _context.LedgerTransactions.Update(transaction);
            }
            await _context.SaveChangesAsync();
        }

        public async Task DeleteTransactionsByQuarterIdAsync(Guid quarterId)
        {
            var existing = await _context.LedgerTransactions.Where(t => t.QuarterId == quarterId).ToListAsync();
            if (existing.Any())
            {
                _context.LedgerTransactions.RemoveRange(existing);
                await _context.SaveChangesAsync();
            }
        }

        // Rules
        public async Task<IEnumerable<NhnLedgerRule>> GetAllRulesAsync()
        {
            return await _context.LedgerRules
                .AsNoTracking()
                .OrderByDescending(r => r.Keyword.Length)
                .ToListAsync();
        }

        public async Task<NhnLedgerRule?> GetRuleByKeywordAsync(string keyword)
        {
            return await _context.LedgerRules
                .FirstOrDefaultAsync(r => r.Keyword.ToLower() == keyword.ToLower());
        }

        public async Task<NhnLedgerRule> AddRuleAsync(NhnLedgerRule rule)
        {
            if (rule.Id == Guid.Empty) rule.Id = Guid.NewGuid();
            rule.CreatedAt = DateTimeOffset.UtcNow;
            rule.UpdatedAt = DateTimeOffset.UtcNow;

            _context.LedgerRules.Add(rule);
            await _context.SaveChangesAsync();
            return rule;
        }

        public async Task UpdateRuleAsync(NhnLedgerRule rule)
        {
            rule.UpdatedAt = DateTimeOffset.UtcNow;
            _context.LedgerRules.Update(rule);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteRuleAsync(Guid id)
        {
            var rule = await _context.LedgerRules.FirstOrDefaultAsync(r => r.Id == id);
            if (rule != null)
            {
                _context.LedgerRules.Remove(rule);
                await _context.SaveChangesAsync();
            }
        }
    }
}
