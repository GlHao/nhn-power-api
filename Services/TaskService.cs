using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NHN.Power.API.Data;
using NHN.Power.API.DTOs.Task;
using NHN.Power.API.Models;

namespace NHN.Power.API.Services
{
    public class TaskService : ITaskService
    {
        private readonly AppDbContext _dbContext;
        private readonly IScheduleCalculator _scheduleCalculator;
        private readonly ILogger<TaskService> _logger;

        private static readonly string[] DayNamesChinese = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };

        public TaskService(
            AppDbContext dbContext,
            IScheduleCalculator scheduleCalculator,
            ILogger<TaskService> logger)
        {
            _dbContext = dbContext;
            _scheduleCalculator = scheduleCalculator;
            _logger = logger;
        }

        #region Ad-hoc Tasks

        public async Task<List<AdhocTaskDto>> GetAdhocTasksAsync(DateTime? startDate = null, DateTime? endDate = null, string? status = null)
        {
            var query = _dbContext.AdhocTasks
                .Include(t => t.Photos)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(t => t.TaskDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(t => t.TaskDate <= endDate.Value.Date);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(t => t.Status.ToLower() == status.ToLower());
            }

            var tasks = await query
                .OrderByDescending(t => t.TaskDate)
                .ThenByDescending(t => t.CreatedAt)
                .ToListAsync();

            return tasks.Select(MapToAdhocTaskDto).ToList();
        }

        public async Task<AdhocTaskDto?> GetAdhocTaskByIdAsync(Guid id)
        {
            var task = await _dbContext.AdhocTasks
                .Include(t => t.Photos)
                .FirstOrDefaultAsync(t => t.Id == id);

            return task == null ? null : MapToAdhocTaskDto(task);
        }

        public async Task<AdhocTaskDto> CreateAdhocTaskAsync(CreateAdhocTaskDto dto)
        {
            var task = new AdhocTask
            {
                Description = dto.Description,
                Amount = dto.Amount,
                Address = dto.Address,
                TaskDate = DateTime.SpecifyKind(dto.TaskDate.Date, DateTimeKind.Utc),
                TaskTime = dto.TaskTime,
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "pending" : dto.Status.ToLower(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.AdhocTasks.Add(task);
            await _dbContext.SaveChangesAsync();

            return MapToAdhocTaskDto(task);
        }

        public async Task<AdhocTaskDto?> UpdateAdhocTaskAsync(Guid id, UpdateAdhocTaskDto dto)
        {
            var task = await _dbContext.AdhocTasks
                .Include(t => t.Photos)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null) return null;

            if (dto.Description != null) task.Description = dto.Description;
            if (dto.Amount.HasValue) task.Amount = dto.Amount.Value;
            if (dto.Address != null) task.Address = dto.Address;
            if (dto.TaskDate.HasValue) task.TaskDate = DateTime.SpecifyKind(dto.TaskDate.Value.Date, DateTimeKind.Utc);
            if (dto.TaskTime != null) task.TaskTime = dto.TaskTime;
            if (!string.IsNullOrWhiteSpace(dto.Status)) task.Status = dto.Status.ToLower();

            task.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync();

            return MapToAdhocTaskDto(task);
        }

        public async Task<bool> DeleteAdhocTaskAsync(Guid id)
        {
            var task = await _dbContext.AdhocTasks
                .Include(t => t.Photos)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null) return false;

            // Remove associated photos if any (DB cascade or EF Core cascade)
            if (task.Photos != null && task.Photos.Any())
            {
                _dbContext.TaskPhotos.RemoveRange(task.Photos);
            }

            _dbContext.AdhocTasks.Remove(task);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Deleted adhoc task {Id} and associated photos.", id);
            return true;
        }

        #endregion

        #region Regular Tasks Weekly Calendar

        public async Task<WeeklyTaskCalendarDto> GetWeeklyRegularTasksAsync(DateTime weekDate)
        {
            // Determine Monday of the week
            int diff = ((int)weekDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            DateTime monday = weekDate.Date.AddDays(-diff);
            DateTime sunday = monday.AddDays(6);

            int weekNumber = ISOWeek.GetWeekOfYear(monday);

            // Fetch all active rules with Sites and Clients
            var activeRules = await _dbContext.ServiceScheduleRules
                .Include(r => r.Site)
                .ThenInclude(s => s.Client)
                .Where(r => r.IsActive && r.DeletedAt == null && r.Site.IsActive && r.Site.DeletedAt == null)
                .ToListAsync();

            // Fetch all regular task photos within this week
            var weekPhotos = await _dbContext.TaskPhotos
                .Where(p => p.TaskType == "regular" && p.ServiceDate >= monday && p.ServiceDate <= sunday)
                .ToListAsync();

            var calendar = new WeeklyTaskCalendarDto
            {
                WeekStart = monday,
                WeekEnd = sunday,
                WeekNumber = weekNumber,
                Days = new List<DailyTaskGroupDto>()
            };

            // Loop 7 days (Monday -> Sunday)
            for (int i = 0; i < 7; i++)
            {
                var currentDate = monday.AddDays(i);
                var dayTasks = new List<RegularTaskItemDto>();

                foreach (var rule in activeRules)
                {
                    var scheduledDates = _scheduleCalculator.CalculateServiceDates(currentDate, currentDate, new List<ServiceScheduleRule> { rule });
                    if (scheduledDates.Contains(currentDate))
                    {
                        var matchingPhotos = weekPhotos
                            .Where(p => p.SiteId == rule.SiteId && p.ServiceDate.Date == currentDate.Date)
                            .ToList();

                        dayTasks.Add(new RegularTaskItemDto
                        {
                            SiteId = rule.SiteId,
                            SiteName = rule.Site?.Name ?? "未命名站点",
                            ClientName = rule.Site?.Client?.Name ?? "未指定客户",
                            Address = rule.Site?.Address ?? string.Empty,
                            Notes = rule.Site?.Notes,
                            FrequencyType = rule.FrequencyType,
                            ServiceDate = currentDate,
                            PhotoCount = matchingPhotos.Count,
                            SentPhotoCount = matchingPhotos.Count(p => p.IsSent),
                            Photos = matchingPhotos.Select(MapToTaskPhotoDto).ToList()
                        });
                    }
                }

                int dayOfWeekIdx = (int)currentDate.DayOfWeek;
                string dayName = DayNamesChinese[dayOfWeekIdx];

                calendar.Days.Add(new DailyTaskGroupDto
                {
                    Date = currentDate,
                    DayOfWeek = dayOfWeekIdx,
                    DayName = dayName,
                    Tasks = dayTasks.OrderBy(t => t.SiteName).ToList()
                });
            }

            return calendar;
        }

        #endregion

        #region Photo Evidence

        public async Task<TaskPhotoDto> CreateTaskPhotoAsync(CreateTaskPhotoDto dto)
        {
            var photo = new TaskPhoto
            {
                TaskType = dto.TaskType.ToLower(),
                AdhocTaskId = dto.AdhocTaskId,
                SiteId = dto.SiteId,
                ServiceDate = DateTime.SpecifyKind(dto.ServiceDate.Date, DateTimeKind.Utc),
                StorageType = string.IsNullOrWhiteSpace(dto.StorageType) ? "local" : dto.StorageType.ToLower(),
                FilePath = dto.FilePath,
                WatermarkTime = dto.WatermarkTime,
                WatermarkAddress = dto.WatermarkAddress,
                IsSent = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.TaskPhotos.Add(photo);
            await _dbContext.SaveChangesAsync();

            return MapToTaskPhotoDto(photo);
        }

        public async Task<int> MarkPhotosSentAsync(MarkPhotosSentDto dto)
        {
            if (dto.PhotoIds == null || !dto.PhotoIds.Any()) return 0;

            var photos = await _dbContext.TaskPhotos
                .Where(p => dto.PhotoIds.Contains(p.Id))
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            foreach (var p in photos)
            {
                p.IsSent = true;
                p.SentAt = now;
                if (!string.IsNullOrWhiteSpace(dto.Channel))
                {
                    p.SentChannel = dto.Channel.ToLower();
                }
                p.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync();
            return photos.Count;
        }

        public async Task<PhotoCleanupResultDto> CleanupPhotosAsync(PhotoCleanupRequestDto dto)
        {
            var query = _dbContext.TaskPhotos.AsQueryable();

            if (dto.AdhocTaskId.HasValue)
            {
                query = query.Where(p => p.AdhocTaskId == dto.AdhocTaskId.Value);
            }

            if (dto.SiteId.HasValue)
            {
                query = query.Where(p => p.SiteId == dto.SiteId.Value);
            }

            if (dto.ServiceDate.HasValue)
            {
                query = query.Where(p => p.ServiceDate == dto.ServiceDate.Value.Date);
            }

            if (dto.OnlySent)
            {
                query = query.Where(p => p.IsSent);
            }

            if (dto.OlderThanDays.HasValue && dto.OlderThanDays.Value > 0)
            {
                var threshold = DateTimeOffset.UtcNow.AddDays(-dto.OlderThanDays.Value);
                query = query.Where(p => p.WatermarkTime <= threshold);
            }

            var photosToDelete = await query.ToListAsync();
            int count = photosToDelete.Count;

            if (count > 0)
            {
                _dbContext.TaskPhotos.RemoveRange(photosToDelete);
                await _dbContext.SaveChangesAsync();
            }

            return new PhotoCleanupResultDto
            {
                DeletedCount = count,
                Message = $"成功清理了 {count} 张留证照片元数据记录。"
            };
        }

        #endregion

        #region Helpers

        private static AdhocTaskDto MapToAdhocTaskDto(AdhocTask t)
        {
            var photos = t.Photos ?? new List<TaskPhoto>();
            return new AdhocTaskDto
            {
                Id = t.Id,
                Description = t.Description,
                Amount = t.Amount,
                Address = t.Address,
                TaskDate = t.TaskDate,
                TaskTime = t.TaskTime,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                IsInvoiced = t.IsInvoiced,
                InvoicedAt = t.InvoicedAt,
                TaxInvoiceId = t.TaxInvoiceId,
                PhotoCount = photos.Count,
                SentPhotoCount = photos.Count(p => p.IsSent),
                Photos = photos.Select(MapToTaskPhotoDto).ToList()
            };
        }

        private static TaskPhotoDto MapToTaskPhotoDto(TaskPhoto p)
        {
            return new TaskPhotoDto
            {
                Id = p.Id,
                TaskType = p.TaskType,
                AdhocTaskId = p.AdhocTaskId,
                SiteId = p.SiteId,
                ServiceDate = p.ServiceDate,
                StorageType = p.StorageType,
                FilePath = p.FilePath,
                WatermarkTime = p.WatermarkTime,
                WatermarkAddress = p.WatermarkAddress,
                IsSent = p.IsSent,
                SentAt = p.SentAt,
                SentChannel = p.SentChannel,
                CreatedAt = p.CreatedAt
            };
        }

        #endregion
    }
}
