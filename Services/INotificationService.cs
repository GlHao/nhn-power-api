using System;
using System.Threading.Tasks;
using NHN.Power.API.DTOs;

namespace NHN.Power.API.Services
{
    public interface INotificationService
    {
        Task SendQuoteNotificationsAsync(Guid quoteId, QuoteSubmitRequest request);
    }
}
