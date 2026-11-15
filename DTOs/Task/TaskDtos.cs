using System;
using System.Collections.Generic;

namespace NHN.Power.API.DTOs.Task
{
    public class AdhocTaskDto
    {
        public Guid Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Address { get; set; } = string.Empty;
        public DateTime TaskDate { get; set; }
        public string? TaskTime { get; set; }
        public string Status { get; set; } = "pending";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public bool IsInvoiced { get; set; }
        public DateTimeOffset? InvoicedAt { get; set; }
        public Guid? TaxInvoiceId { get; set; }
        public int PhotoCount { get; set; }
        public int SentPhotoCount { get; set; }
        public List<TaskPhotoDto> Photos { get; set; } = new();
    }

    public class CreateAdhocTaskDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Address { get; set; } = string.Empty;
        public DateTime TaskDate { get; set; }
        public string? TaskTime { get; set; }
        public string? Status { get; set; } = "pending";
    }

    public class UpdateAdhocTaskDto
    {
        public string? Description { get; set; }
        public decimal? Amount { get; set; }
        public string? Address { get; set; }
        public DateTime? TaskDate { get; set; }
        public string? TaskTime { get; set; }
        public string? Status { get; set; }
    }

    public class WeeklyTaskCalendarDto
    {
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public int WeekNumber { get; set; }
        public List<DailyTaskGroupDto> Days { get; set; } = new();
    }

    public class DailyTaskGroupDto
    {
        public DateTime Date { get; set; }
        public int DayOfWeek { get; set; }
        public string DayName { get; set; } = string.Empty;
        public List<RegularTaskItemDto> Tasks { get; set; } = new();
    }

    public class RegularTaskItemDto
    {
        public Guid SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string FrequencyType { get; set; } = string.Empty;
        public DateTime ServiceDate { get; set; }
        public int PhotoCount { get; set; }
        public int SentPhotoCount { get; set; }
        public List<TaskPhotoDto> Photos { get; set; } = new();
    }

    public class TaskPhotoDto
    {
        public Guid Id { get; set; }
        public string TaskType { get; set; } = string.Empty;
        public Guid? AdhocTaskId { get; set; }
        public Guid? SiteId { get; set; }
        public DateTime ServiceDate { get; set; }
        public string StorageType { get; set; } = "local";
        public string FilePath { get; set; } = string.Empty;
        public DateTimeOffset WatermarkTime { get; set; }
        public string WatermarkAddress { get; set; } = string.Empty;
        public bool IsSent { get; set; }
        public DateTimeOffset? SentAt { get; set; }
        public string? SentChannel { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class CreateTaskPhotoDto
    {
        public string TaskType { get; set; } = "adhoc"; // "adhoc" | "regular"
        public Guid? AdhocTaskId { get; set; }
        public Guid? SiteId { get; set; }
        public DateTime ServiceDate { get; set; }
        public string StorageType { get; set; } = "local";
        public string FilePath { get; set; } = string.Empty;
        public DateTimeOffset WatermarkTime { get; set; }
        public string WatermarkAddress { get; set; } = string.Empty;
    }

    public class MarkPhotosSentDto
    {
        public List<Guid> PhotoIds { get; set; } = new();
        public string? Channel { get; set; }
    }

    public class PhotoCleanupRequestDto
    {
        public Guid? AdhocTaskId { get; set; }
        public Guid? SiteId { get; set; }
        public DateTime? ServiceDate { get; set; }
        public bool OnlySent { get; set; } = true;
        public int? OlderThanDays { get; set; }
    }

    public class PhotoCleanupResultDto
    {
        public int DeletedCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
