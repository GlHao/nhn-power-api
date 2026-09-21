using System.Threading.Tasks;

namespace NHN.Power.API.Services
{
    public interface ISmsService
    {
        Task<bool> SendSmsAsync(string toPhone, string messageText, string? senderId = null);
    }
}
