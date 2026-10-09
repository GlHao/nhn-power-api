using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NHN.Power.API.Data;
using NHN.Power.API.Models;

namespace NHN.Power.API.Repositories
{
    public class ReceiptRepository : IReceiptRepository
    {
        private readonly AppDbContext _context;

        public ReceiptRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<NhnReceipt>> GetAllAsync(string? status = null)
        {
            IQueryable<NhnReceipt> query = _context.Receipts.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }

            return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        }

        public async Task<NhnReceipt?> GetByIdAsync(Guid id)
        {
            return await _context.Receipts.FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<NhnReceipt> AddAsync(NhnReceipt receipt)
        {
            if (receipt.Id == Guid.Empty)
            {
                receipt.Id = Guid.NewGuid();
            }
            receipt.CreatedAt = DateTimeOffset.UtcNow;
            receipt.UpdatedAt = DateTimeOffset.UtcNow;

            _context.Receipts.Add(receipt);
            await _context.SaveChangesAsync();
            return receipt;
        }

        public async Task UpdateAsync(NhnReceipt receipt)
        {
            receipt.UpdatedAt = DateTimeOffset.UtcNow;
            var tracked = await _context.Receipts.FindAsync(receipt.Id);
            if (tracked != null && tracked != receipt)
            {
                _context.Entry(tracked).CurrentValues.SetValues(receipt);
                tracked.Status = receipt.Status;
                tracked.MatchedTransactionId = receipt.MatchedTransactionId;
                tracked.MerchantName = receipt.MerchantName;
                tracked.TransactionDate = receipt.TransactionDate;
                tracked.TotalAmount = receipt.TotalAmount;
                tracked.GstAmount = receipt.GstAmount;
            }
            else
            {
                _context.Receipts.Update(receipt);
            }
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var receipt = await GetByIdAsync(id);
            if (receipt != null)
            {
                _context.Receipts.Remove(receipt);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<NhnReceipt>> GetPendingReceiptsForMatchingAsync(DateTime startDate, DateTime endDate, decimal targetAmount)
        {
            // Amount must match exactly (or Math.Abs(TotalAmount - targetAmount) < 0.01m)
            // Date between startDate and endDate (e.g. ±3 days)
            return await _context.Receipts
                .Where(r => r.Status == "pending"
                            && r.TransactionDate.HasValue
                            && r.TransactionDate.Value.Date >= startDate.Date
                            && r.TransactionDate.Value.Date <= endDate.Date
                            && Math.Abs(r.TotalAmount - targetAmount) < 0.01m)
                .ToListAsync();
        }
    }
}
