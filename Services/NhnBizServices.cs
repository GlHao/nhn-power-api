using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NHN.Power.API.DTOs.Client;
using NHN.Power.API.DTOs.Site;
using NHN.Power.API.DTOs.BillingRule;
using NHN.Power.API.DTOs.ScheduleRule;
using NHN.Power.API.DTOs.TaxInvoice;
using NHN.Power.API.Models;
using NHN.Power.API.Repositories;

namespace NHN.Power.API.Services
{
    public interface IClientService
    {
        Task<IEnumerable<ClientResponse>> GetAllClientsAsync();
        Task<ClientResponse?> GetClientByIdAsync(Guid id);
        Task<ClientResponse> CreateClientAsync(CreateClientRequest request);
        Task<ClientResponse?> UpdateClientAsync(Guid id, UpdateClientRequest request);
        Task<bool> DeleteClientAsync(Guid id);
    }

    public interface ISiteService
    {
        Task<IEnumerable<SiteResponse>> GetSitesByClientIdAsync(Guid clientId);
        Task<SiteResponse?> GetSiteByIdAsync(Guid id);
        Task<SiteResponse> CreateSiteAsync(CreateSiteRequest request);
        Task<SiteResponse?> UpdateSiteAsync(Guid id, UpdateSiteRequest request);
        Task<bool> DeleteSiteAsync(Guid id);
    }

    public interface IBillingRuleService
    {
        Task<IEnumerable<BillingRuleResponse>> GetRulesBySiteIdAsync(Guid siteId);
        Task<BillingRuleResponse?> GetRuleByIdAsync(Guid id);
        Task<BillingRuleResponse> CreateRuleAsync(CreateBillingRuleRequest request);
        Task<BillingRuleResponse?> UpdateRuleAsync(Guid id, UpdateBillingRuleRequest request);
        Task<bool> DeleteRuleAsync(Guid id);
    }

    public interface IScheduleRuleService
    {
        Task<IEnumerable<ScheduleRuleResponse>> GetRulesBySiteIdAsync(Guid siteId);
        Task<ScheduleRuleResponse?> GetRuleByIdAsync(Guid id);
        Task<ScheduleRuleResponse> CreateRuleAsync(CreateScheduleRuleRequest request);
        Task<ScheduleRuleResponse?> UpdateRuleAsync(Guid id, UpdateScheduleRuleRequest request);
        Task<bool> DeleteRuleAsync(Guid id);
    }

    public interface IPublicHolidayHelper
    {
        bool IsPublicHoliday(DateTime date);
        string? GetPublicHolidayName(DateTime date);
    }

    public interface IScheduleCalculator
    {
        List<DateTime> CalculateServiceDates(DateTime periodStart, DateTime periodEnd, List<ServiceScheduleRule> rules);
    }

    public interface IBillingCalculator
    {
        TaxInvoicePreviewItemDto CalculateItem(Site site, DateTime serviceDate, List<BillingRule> billingRules);
    }

    public interface ITaxInvoicePreviewService
    {
        Task<TaxInvoicePreviewDto> GeneratePreviewAsync(TaxInvoicePreviewRequestDto request);
    }

    public interface ITaxInvoicePdfService
    {
        byte[] GeneratePdf(TaxInvoicePdfRequestDto request);
        Task<(bool Success, string Message, string? R2Path)> SendTaxInvoiceEmailAsync(SendTaxInvoiceEmailRequestDto request);
    }

    public class ClientService : IClientService
    {
        private readonly IClientRepository _repository;

        public ClientService(IClientRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<ClientResponse>> GetAllClientsAsync()
        {
            var clients = await _repository.GetAllActiveAsync();
            return clients.Select(MapToResponse);
        }

        public async Task<ClientResponse?> GetClientByIdAsync(Guid id)
        {
            var client = await _repository.GetByIdAsync(id);
            return client != null ? MapToResponse(client) : null;
        }

        public async Task<ClientResponse> CreateClientAsync(CreateClientRequest request)
        {
            var client = new Client
            {
                Name = request.Name,
                Abn = request.Abn,
                Email = request.Email,
                Notes = request.Notes
            };

            var created = await _repository.AddAsync(client);
            return MapToResponse(created);
        }

        public async Task<ClientResponse?> UpdateClientAsync(Guid id, UpdateClientRequest request)
        {
            var client = await _repository.GetByIdAsync(id);
            if (client == null) return null;

            client.Name = request.Name;
            client.Abn = request.Abn;
            client.Email = request.Email;
            client.Notes = request.Notes;

            await _repository.UpdateAsync(client);
            return MapToResponse(client);
        }

        public async Task<bool> DeleteClientAsync(Guid id)
        {
            var client = await _repository.GetByIdAsync(id);
            if (client == null) return false;

            await _repository.SoftDeleteAsync(id);
            return true;
        }

        private static ClientResponse MapToResponse(Client client)
        {
            return new ClientResponse
            {
                Id = client.Id,
                Name = client.Name,
                Abn = client.Abn,
                Email = client.Email,
                BrandId = client.BrandId,
                Notes = client.Notes,
                IsActive = client.IsActive,
                CreatedAt = client.CreatedAt,
                UpdatedAt = client.UpdatedAt
            };
        }
    }

    public class SiteService : ISiteService
    {
        private readonly ISiteRepository _repository;

        public SiteService(ISiteRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<SiteResponse>> GetSitesByClientIdAsync(Guid clientId)
        {
            var sites = await _repository.GetByClientIdAsync(clientId);
            return sites.Select(MapToResponse);
        }

        public async Task<SiteResponse?> GetSiteByIdAsync(Guid id)
        {
            var site = await _repository.GetByIdAsync(id);
            return site != null ? MapToResponse(site) : null;
        }

        public async Task<SiteResponse> CreateSiteAsync(CreateSiteRequest request)
        {
            var site = new Site
            {
                ClientId = request.ClientId,
                Name = request.Name,
                Address = request.Address,
                Notes = request.Notes
            };

            var created = await _repository.AddAsync(site);
            return MapToResponse(created);
        }

        public async Task<SiteResponse?> UpdateSiteAsync(Guid id, UpdateSiteRequest request)
        {
            var site = await _repository.GetByIdAsync(id);
            if (site == null) return null;

            site.Name = request.Name;
            site.Address = request.Address;
            site.Notes = request.Notes;

            await _repository.UpdateAsync(site);
            return MapToResponse(site);
        }

        public async Task<bool> DeleteSiteAsync(Guid id)
        {
            var site = await _repository.GetByIdAsync(id);
            if (site == null) return false;

            await _repository.SoftDeleteAsync(id);
            return true;
        }

        private static SiteResponse MapToResponse(Site site)
        {
            return new SiteResponse
            {
                Id = site.Id,
                ClientId = site.ClientId,
                Name = site.Name,
                Address = site.Address,
                Notes = site.Notes,
                IsActive = site.IsActive,
                CreatedAt = site.CreatedAt,
                UpdatedAt = site.UpdatedAt
            };
        }
    }

    public class BillingRuleService : IBillingRuleService
    {
        private readonly IBillingRuleRepository _repository;

        public BillingRuleService(IBillingRuleRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<BillingRuleResponse>> GetRulesBySiteIdAsync(Guid siteId)
        {
            var rules = await _repository.GetBySiteIdAsync(siteId);
            return rules.Select(MapToResponse);
        }

        public async Task<BillingRuleResponse?> GetRuleByIdAsync(Guid id)
        {
            var rule = await _repository.GetByIdAsync(id);
            return rule != null ? MapToResponse(rule) : null;
        }

        public async Task<BillingRuleResponse> CreateRuleAsync(CreateBillingRuleRequest request)
        {
            var rule = new BillingRule
            {
                SiteId = request.SiteId,
                DayOfWeek = request.DayOfWeek,
                AmountExGst = request.AmountExGst,
                PublicHolidayAction = request.PublicHolidayAction,
                PublicHolidayExtraExGst = request.PublicHolidayExtraExGst,
                Description = request.Description
            };

            var created = await _repository.AddAsync(rule);
            return MapToResponse(created);
        }

        public async Task<BillingRuleResponse?> UpdateRuleAsync(Guid id, UpdateBillingRuleRequest request)
        {
            var rule = await _repository.GetByIdAsync(id);
            if (rule == null) return null;

            rule.DayOfWeek = request.DayOfWeek;
            rule.AmountExGst = request.AmountExGst;
            rule.PublicHolidayAction = request.PublicHolidayAction;
            rule.PublicHolidayExtraExGst = request.PublicHolidayExtraExGst;
            rule.Description = request.Description;

            await _repository.UpdateAsync(rule);
            return MapToResponse(rule);
        }

        public async Task<bool> DeleteRuleAsync(Guid id)
        {
            var rule = await _repository.GetByIdAsync(id);
            if (rule == null) return false;

            await _repository.SoftDeleteAsync(id);
            return true;
        }

        private static BillingRuleResponse MapToResponse(BillingRule rule)
        {
            return new BillingRuleResponse
            {
                Id = rule.Id,
                SiteId = rule.SiteId,
                DayOfWeek = rule.DayOfWeek,
                AmountExGst = rule.AmountExGst,
                PublicHolidayAction = rule.PublicHolidayAction,
                PublicHolidayExtraExGst = rule.PublicHolidayExtraExGst,
                Description = rule.Description,
                IsActive = rule.IsActive,
                CreatedAt = rule.CreatedAt,
                UpdatedAt = rule.UpdatedAt
            };
        }
    }

    public class ScheduleRuleService : IScheduleRuleService
    {
        private readonly IScheduleRuleRepository _repository;

        public ScheduleRuleService(IScheduleRuleRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<ScheduleRuleResponse>> GetRulesBySiteIdAsync(Guid siteId)
        {
            var rules = await _repository.GetBySiteIdAsync(siteId);
            return rules.Select(MapToResponse);
        }

        public async Task<ScheduleRuleResponse?> GetRuleByIdAsync(Guid id)
        {
            var rule = await _repository.GetByIdAsync(id);
            return rule != null ? MapToResponse(rule) : null;
        }

        public async Task<ScheduleRuleResponse> CreateRuleAsync(CreateScheduleRuleRequest request)
        {
            var rule = new ServiceScheduleRule
            {
                SiteId = request.SiteId,
                FrequencyType = request.FrequencyType,
                DaysOfWeek = request.DaysOfWeek,
                DayOfWeek = request.DayOfWeek,
                StartDate = request.StartDate,
                MonthlyPattern = request.MonthlyPattern,
                Description = request.Description
            };

            var created = await _repository.AddAsync(rule);
            return MapToResponse(created);
        }

        public async Task<ScheduleRuleResponse?> UpdateRuleAsync(Guid id, UpdateScheduleRuleRequest request)
        {
            var rule = await _repository.GetByIdAsync(id);
            if (rule == null) return null;

            rule.FrequencyType = request.FrequencyType;
            rule.DaysOfWeek = request.DaysOfWeek;
            rule.DayOfWeek = request.DayOfWeek;
            rule.StartDate = request.StartDate;
            rule.MonthlyPattern = request.MonthlyPattern;
            rule.Description = request.Description;

            await _repository.UpdateAsync(rule);
            return MapToResponse(rule);
        }

        public async Task<bool> DeleteRuleAsync(Guid id)
        {
            var rule = await _repository.GetByIdAsync(id);
            if (rule == null) return false;

            await _repository.SoftDeleteAsync(id);
            return true;
        }

        private static ScheduleRuleResponse MapToResponse(ServiceScheduleRule rule)
        {
            return new ScheduleRuleResponse
            {
                Id = rule.Id,
                SiteId = rule.SiteId,
                FrequencyType = rule.FrequencyType,
                DaysOfWeek = rule.DaysOfWeek,
                DayOfWeek = rule.DayOfWeek,
                StartDate = rule.StartDate,
                MonthlyPattern = rule.MonthlyPattern,
                Description = rule.Description,
                IsActive = rule.IsActive,
                CreatedAt = rule.CreatedAt,
                UpdatedAt = rule.UpdatedAt
            };
        }
    }

    public class PublicHolidayHelper : IPublicHolidayHelper
    {
        private readonly Dictionary<DateTime, string> _holidays = new()
        {
            { new DateTime(2026, 1, 1), "New Year's Day" },
            { new DateTime(2026, 1, 26), "Australia Day" },
            { new DateTime(2026, 3, 9), "Labour Day" },
            { new DateTime(2026, 4, 3), "Good Friday" },
            { new DateTime(2026, 4, 4), "Saturday before Easter Sunday" },
            { new DateTime(2026, 4, 5), "Easter Sunday" },
            { new DateTime(2026, 4, 6), "Easter Monday" },
            { new DateTime(2026, 4, 25), "ANZAC Day" },
            { new DateTime(2026, 6, 8), "King's Birthday" },
            { new DateTime(2026, 9, 25), "Friday before the AFL Grand Final" },
            { new DateTime(2026, 11, 3), "Melbourne Cup" },
            { new DateTime(2026, 12, 25), "Christmas Day" },
            { new DateTime(2026, 12, 26), "Boxing Day" },
            { new DateTime(2026, 12, 28), "Boxing Day (Additional)" }
        };

        public bool IsPublicHoliday(DateTime date) => _holidays.ContainsKey(date.Date);
        public string? GetPublicHolidayName(DateTime date) => _holidays.TryGetValue(date.Date, out var name) ? name : null;
    }

    public class ScheduleCalculator : IScheduleCalculator
    {
        public List<DateTime> CalculateServiceDates(DateTime periodStart, DateTime periodEnd, List<ServiceScheduleRule> rules)
        {
            var serviceDates = new HashSet<DateTime>();

            foreach (var rule in rules.Where(r => r.IsActive))
            {
                switch (rule.FrequencyType.ToLower())
                {
                    case "weekly":
                        CalculateWeekly(periodStart, periodEnd, rule, serviceDates);
                        break;
                    case "fortnightly":
                        CalculateFortnightly(periodStart, periodEnd, rule, serviceDates);
                        break;
                    case "monthly":
                        CalculateMonthly(periodStart, periodEnd, rule, serviceDates);
                        break;
                }
            }

            return serviceDates.OrderBy(d => d).ToList();
        }

        private void CalculateWeekly(DateTime start, DateTime end, ServiceScheduleRule rule, HashSet<DateTime> dates)
        {
            if (rule.DaysOfWeek == null || rule.DaysOfWeek.Count == 0) return;

            for (var current = start; current <= end; current = current.AddDays(1))
            {
                if (rule.DaysOfWeek.Contains((int)current.DayOfWeek))
                {
                    dates.Add(current);
                }
            }
        }

        private void CalculateFortnightly(DateTime start, DateTime end, ServiceScheduleRule rule, HashSet<DateTime> dates)
        {
            if (!rule.DayOfWeek.HasValue || !rule.StartDate.HasValue) return;

            var ruleStart = rule.StartDate.Value.Date;

            for (var current = start; current <= end; current = current.AddDays(1))
            {
                if ((int)current.DayOfWeek == rule.DayOfWeek.Value)
                {
                    var daysDifference = (current - ruleStart).Days;
                    if (daysDifference >= 0)
                    {
                        var weeksDifference = daysDifference / 7;
                        if (weeksDifference % 2 == 0)
                        {
                            dates.Add(current);
                        }
                    }
                }
            }
        }

        private void CalculateMonthly(DateTime start, DateTime end, ServiceScheduleRule rule, HashSet<DateTime> dates)
        {
            if (!rule.DayOfWeek.HasValue || string.IsNullOrEmpty(rule.MonthlyPattern)) return;

            var currentMonth = new DateTime(start.Year, start.Month, 1);
            var endMonth = new DateTime(end.Year, end.Month, 1);

            while (currentMonth <= endMonth)
            {
                DateTime? targetDate = null;

                if (rule.MonthlyPattern.ToLower() == "first")
                {
                    targetDate = FindFirstDayOfWeekInMonth(currentMonth.Year, currentMonth.Month, (DayOfWeek)rule.DayOfWeek.Value);
                }
                else if (rule.MonthlyPattern.ToLower() == "last")
                {
                    targetDate = FindLastDayOfWeekInMonth(currentMonth.Year, currentMonth.Month, (DayOfWeek)rule.DayOfWeek.Value);
                }

                if (targetDate.HasValue && targetDate.Value >= start && targetDate.Value <= end)
                {
                    dates.Add(targetDate.Value);
                }

                currentMonth = currentMonth.AddMonths(1);
            }
        }

        private DateTime FindFirstDayOfWeekInMonth(int year, int month, DayOfWeek dayOfWeek)
        {
            var dt = new DateTime(year, month, 1);
            while (dt.DayOfWeek != dayOfWeek) dt = dt.AddDays(1);
            return dt;
        }

        private DateTime FindLastDayOfWeekInMonth(int year, int month, DayOfWeek dayOfWeek)
        {
            var dt = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            while (dt.DayOfWeek != dayOfWeek) dt = dt.AddDays(-1);
            return dt;
        }
    }

    public class BillingCalculator : IBillingCalculator
    {
        private readonly IPublicHolidayHelper _publicHolidayHelper;

        public BillingCalculator(IPublicHolidayHelper publicHolidayHelper)
        {
            _publicHolidayHelper = publicHolidayHelper;
        }

        public TaxInvoicePreviewItemDto CalculateItem(Site site, DateTime serviceDate, List<BillingRule> billingRules)
        {
            var item = new TaxInvoicePreviewItemDto
            {
                SiteId = site.Id,
                SiteName = site.Name,
                ServiceDate = serviceDate,
                DayOfWeek = (int)serviceDate.DayOfWeek
            };

            var rule = billingRules.FirstOrDefault(r => r.IsActive && r.DayOfWeek == item.DayOfWeek);

            if (rule == null)
            {
                item.BaseAmountExGst = 0;
                item.FinalAmountExGst = 0;
                item.Status = "missing_billing_rule";
                item.Note = "No billing rule found for this day of the week.";
                return item;
            }

            item.BaseAmountExGst = rule.AmountExGst;
            item.FinalAmountExGst = rule.AmountExGst;
            item.Description = rule.Description;

            if (_publicHolidayHelper.IsPublicHoliday(serviceDate))
            {
                var holidayName = _publicHolidayHelper.GetPublicHolidayName(serviceDate);
                item.Note = $"Public Holiday: {holidayName}";

                switch (rule.PublicHolidayAction.ToLower())
                {
                    case "skip":
                    case "closed":
                        item.Status = "public_holiday_skipped";
                        item.FinalAmountExGst = 0;
                        break;
                    case "manual_adjust":
                        item.Status = "public_holiday_manual_adjust";
                        break;
                    case "extra_charge":
                        item.Status = "public_holiday_extra_applied";
                        item.ExtraAmountExGst = rule.PublicHolidayExtraExGst;
                        item.FinalAmountExGst = item.BaseAmountExGst + item.ExtraAmountExGst;
                        break;
                    case "no_change":
                    case "none":
                    default:
                        break;
                }
            }

            if (item.Status != "public_holiday_skipped")
            {
                item.GstAmount = Math.Round(item.FinalAmountExGst * 0.1m, 2);
                item.FinalAmountIncGst = item.FinalAmountExGst + item.GstAmount;
            }

            return item;
        }
    }

    public class TaxInvoicePreviewService : ITaxInvoicePreviewService
    {
        private readonly IClientRepository _clientRepository;
        private readonly ISiteRepository _siteRepository;
        private readonly IScheduleRuleRepository _scheduleRuleRepository;
        private readonly IBillingRuleRepository _billingRuleRepository;
        private readonly IScheduleCalculator _scheduleCalculator;
        private readonly IBillingCalculator _billingCalculator;

        public TaxInvoicePreviewService(
            IClientRepository clientRepository,
            ISiteRepository siteRepository,
            IScheduleRuleRepository scheduleRuleRepository,
            IBillingRuleRepository billingRuleRepository,
            IScheduleCalculator scheduleCalculator,
            IBillingCalculator billingCalculator)
        {
            _clientRepository = clientRepository;
            _siteRepository = siteRepository;
            _scheduleRuleRepository = scheduleRuleRepository;
            _billingRuleRepository = billingRuleRepository;
            _scheduleCalculator = scheduleCalculator;
            _billingCalculator = billingCalculator;
        }

        public async Task<TaxInvoicePreviewDto> GeneratePreviewAsync(TaxInvoicePreviewRequestDto request)
        {
            var client = await _clientRepository.GetByIdAsync(request.ClientId);
            if (client == null || !client.IsActive)
            {
                throw new Exception("Client not found or inactive.");
            }

            var preview = new TaxInvoicePreviewDto
            {
                ClientId = client.Id,
                ClientName = client.Name,
                ClientAbn = client.Abn,
                ClientEmail = client.Email,
                PeriodStart = request.PeriodStart,
                PeriodEnd = request.PeriodEnd,
                Items = new List<TaxInvoicePreviewItemDto>()
            };

            var sites = await _siteRepository.GetByClientIdAsync(client.Id);

            foreach (var site in sites)
            {
                var scheduleRules = (await _scheduleRuleRepository.GetBySiteIdAsync(site.Id)).ToList();
                var billingRules = (await _billingRuleRepository.GetBySiteIdAsync(site.Id)).ToList();

                var serviceDates = _scheduleCalculator.CalculateServiceDates(request.PeriodStart, request.PeriodEnd, scheduleRules);

                foreach (var date in serviceDates)
                {
                    var item = _billingCalculator.CalculateItem(site, date, billingRules);
                    if (item.Status != "public_holiday_skipped")
                    {
                        preview.Items.Add(item);
                    }
                }
            }

            preview.Items = preview.Items.OrderBy(i => i.ServiceDate).ThenBy(i => i.SiteName).ToList();

            preview.SubtotalExGst = preview.Items.Sum(i => i.FinalAmountExGst);
            preview.GstAmount = Math.Round(preview.SubtotalExGst * 0.1m, 2);
            preview.TotalIncGst = preview.SubtotalExGst + preview.GstAmount;

            return preview;
        }
    }
}
