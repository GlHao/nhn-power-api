using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NHN.Power.API.DTOs.Task;

namespace NHN.Power.API.Services
{
    public interface ITaskService
    {
        // Ad-hoc Tasks
        Task<List<AdhocTaskDto>> GetAdhocTasksAsync(DateTime? startDate = null, DateTime? endDate = null, string? status = null);
        Task<AdhocTaskDto?> GetAdhocTaskByIdAsync(Guid id);
        Task<AdhocTaskDto> CreateAdhocTaskAsync(CreateAdhocTaskDto dto);
        Task<AdhocTaskDto?> UpdateAdhocTaskAsync(Guid id, UpdateAdhocTaskDto dto);
        Task<bool> DeleteAdhocTaskAsync(Guid id);

        // Regular Tasks Weekly Calendar
        Task<WeeklyTaskCalendarDto> GetWeeklyRegularTasksAsync(DateTime weekDate);

        // Photo Evidence
        Task<TaskPhotoDto> CreateTaskPhotoAsync(CreateTaskPhotoDto dto);
        Task<int> MarkPhotosSentAsync(MarkPhotosSentDto dto);
        Task<PhotoCleanupResultDto> CleanupPhotosAsync(PhotoCleanupRequestDto dto);
    }
}
