using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NHN.Power.API.DTOs.Payroll;
using NHN.Power.API.Models;
using NHN.Power.API.Repositories;

namespace NHN.Power.API.Services
{
    public interface IPayrollCalculatorService
    {
        Task<EmployeePayrollPreviewDto> CalculatePayrollAsync(EmployeePayrollPreviewRequestDto request);
    }

    public class PayrollCalculatorService : IPayrollCalculatorService
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IBillingRuleRepository _billingRuleRepository;
        private readonly IScheduleRuleRepository _scheduleRuleRepository;
        private readonly IScheduleCalculator _scheduleCalculator;
        private readonly IPublicHolidayHelper _publicHolidayHelper;

        public PayrollCalculatorService(
            IEmployeeRepository employeeRepository,
            IBillingRuleRepository billingRuleRepository,
            IScheduleRuleRepository scheduleRuleRepository,
            IScheduleCalculator scheduleCalculator,
            IPublicHolidayHelper publicHolidayHelper)
        {
            _employeeRepository = employeeRepository;
            _billingRuleRepository = billingRuleRepository;
            _scheduleRuleRepository = scheduleRuleRepository;
            _scheduleCalculator = scheduleCalculator;
            _publicHolidayHelper = publicHolidayHelper;
        }

        public async Task<EmployeePayrollPreviewDto> CalculatePayrollAsync(EmployeePayrollPreviewRequestDto request)
        {
            var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId);
            if (employee == null || !employee.IsActive)
            {
                throw new Exception("Employee not found or inactive.");
            }

            var preview = new EmployeePayrollPreviewDto
            {
                EmployeeId = employee.Id,
                EmployeeName = employee.Name,
                EmployeePhone = employee.Phone,
                EmployeeEmail = employee.Email,
                PeriodStart = request.PeriodStart,
                PeriodEnd = request.PeriodEnd,
                Items = new List<EmployeePayrollPreviewItemDto>()
            };

            var employeeBillingRules = (await _billingRuleRepository.GetByEmployeeIdAsync(employee.Id)).ToList();
            if (!employeeBillingRules.Any())
            {
                return preview;
            }

            var siteGroups = employeeBillingRules.GroupBy(b => b.SiteId);

            foreach (var siteGroup in siteGroups)
            {
                var siteId = siteGroup.Key;
                var rulesForSite = siteGroup.ToList();
                var siteObj = rulesForSite.FirstOrDefault()?.Site;
                var siteName = siteObj != null
                    ? (!string.IsNullOrWhiteSpace(siteObj.Alias) ? $"{siteObj.Name} ({siteObj.Alias})" : siteObj.Name)
                    : "Unknown Site";

                var scheduleRules = (await _scheduleRuleRepository.GetBySiteIdAsync(siteId)).ToList();
                if (!scheduleRules.Any()) continue;

                var serviceDates = _scheduleCalculator.CalculateServiceDates(request.PeriodStart, request.PeriodEnd, scheduleRules);

                foreach (var date in serviceDates)
                {
                    var dayOfWeek = (int)date.DayOfWeek;
                    var ruleForDay = rulesForSite.FirstOrDefault(r => r.DayOfWeek == dayOfWeek);

                    if (ruleForDay == null) continue;

                    string? note = null;

                    if (_publicHolidayHelper.IsPublicHoliday(date))
                    {
                        var holidayName = _publicHolidayHelper.GetPublicHolidayName(date);
                        note = $"Public Holiday: {holidayName}";

                        if (string.Equals(ruleForDay.PublicHolidayAction, "skip", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ruleForDay.PublicHolidayAction, "closed", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    preview.Items.Add(new EmployeePayrollPreviewItemDto
                    {
                        SiteId = siteId,
                        SiteName = siteName,
                        ServiceDate = date,
                        DayOfWeek = dayOfWeek,
                        Description = ruleForDay.Description,
                        PayExGst = ruleForDay.EmployeePayExGst,
                        Note = note
                    });
                }
            }

            preview.Items = preview.Items
                .OrderBy(i => i.ServiceDate)
                .ThenBy(i => i.SiteName)
                .ToList();

            preview.TotalServices = preview.Items.Count;
            preview.TotalPayExGst = preview.Items.Sum(i => i.PayExGst);

            return preview;
        }
    }
}
