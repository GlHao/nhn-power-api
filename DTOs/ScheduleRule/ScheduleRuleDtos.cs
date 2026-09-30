using System;
using System.Collections.Generic;

namespace NHN.Power.API.DTOs.ScheduleRule
{
    public class ScheduleRuleResponse
    {
        public Guid Id { get; set; }
        public Guid SiteId { get; set; }
        public string FrequencyType { get; set; } = null!;
        public List<int>? DaysOfWeek { get; set; }
        public int? DayOfWeek { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public string? MonthlyPattern { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CreateScheduleRuleRequest
    {
        public Guid SiteId { get; set; }
        public string FrequencyType { get; set; } = null!;
        public List<int>? DaysOfWeek { get; set; }
        public int? DayOfWeek { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public string? MonthlyPattern { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateScheduleRuleRequest
    {
        public string FrequencyType { get; set; } = null!;
        public List<int>? DaysOfWeek { get; set; }
        public int? DayOfWeek { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public string? MonthlyPattern { get; set; }
        public string? Description { get; set; }
    }
}
