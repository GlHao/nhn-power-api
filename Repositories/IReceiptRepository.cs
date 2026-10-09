using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NHN.Power.API.Models;

namespace NHN.Power.API.Repositories
{
    public interface IReceiptRepository
    {
        Task<IEnumerable<NhnReceipt>> GetAllAsync(string? status = null);
        Task<NhnReceipt?> GetByIdAsync(Guid id);
        Task<NhnReceipt> AddAsync(NhnReceipt receipt);
        Task UpdateAsync(NhnReceipt receipt);
        Task DeleteAsync(Guid id);
        Task<IEnumerable<NhnReceipt>> GetPendingReceiptsForMatchingAsync(DateTime startDate, DateTime endDate, decimal targetAmount);
    }
}
