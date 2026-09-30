using System;

namespace NHN.Power.API.DTOs.BillingRule
{
    public class BillingRuleResponse
    {
        public Guid Id { get; set; }
        public Guid SiteId { get; set; }
        public int DayOfWeek { get; set; }
        public decimal AmountExGst { get; set; }
        public string PublicHolidayAction { get; set; } = "no_change";
        public decimal PublicHolidayExtraExGst { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CreateBillingRuleRequest
    {
        public Guid SiteId { get; set; }
        public int DayOfWeek { get; set; }
        public decimal AmountExGst { get; set; }
        public string PublicHolidayAction { get; set; } = "no_change";
        public decimal PublicHolidayExtraExGst { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateBillingRuleRequest
    {
        public int DayOfWeek { get; set; }
        public decimal AmountExGst { get; set; }
        public string PublicHolidayAction { get; set; } = "no_change";
        public decimal PublicHolidayExtraExGst { get; set; }
        public string? Description { get; set; }
    }
}
