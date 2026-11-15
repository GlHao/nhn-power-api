using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs.Task;
using NHN.Power.API.Models;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    public class TaskFunction
    {
        private readonly ITaskService _taskService;
        private readonly ILogger<TaskFunction> _logger;

        public TaskFunction(ITaskService taskService, ILogger<TaskFunction> logger)
        {
            _taskService = taskService;
            _logger = logger;
        }

        #region Adhoc Tasks Endpoints

        [Function("GetAdhocTasks")]
        public async Task<HttpResponseData> GetAdhocTasks(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tasks/adhoc")] HttpRequestData req)
        {
            _logger.LogInformation("Getting adhoc tasks.");

            try
            {
                var query = HttpUtility.ParseQueryString(req.Url.Query);
                DateTime? startDate = null;
                DateTime? endDate = null;

                if (DateTime.TryParse(query["startDate"], out var sDate))
                {
                    startDate = sDate;
                }

                if (DateTime.TryParse(query["endDate"], out var eDate))
                {
                    endDate = eDate;
                }

                string? status = query["status"];

                var tasks = await _taskService.GetAdhocTasksAsync(startDate, endDate, status);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(tasks));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting adhoc tasks");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return err;
            }
        }

        [Function("GetAdhocTaskById")]
        public async Task<HttpResponseData> GetAdhocTaskById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tasks/adhoc/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var taskId))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Task ID."));
                return bad;
            }

            var task = await _taskService.GetAdhocTaskByIdAsync(taskId);
            if (task == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Task not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<AdhocTaskDto>.CreateSuccess(task));
            return response;
        }

        [Function("CreateAdhocTask")]
        public async Task<HttpResponseData> CreateAdhocTask(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tasks/adhoc")] HttpRequestData req)
        {
            try
            {
                var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<CreateAdhocTaskDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (dto == null || string.IsNullOrWhiteSpace(dto.Description) || string.IsNullOrWhiteSpace(dto.Address))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Description and Address are required."));
                    return badReq;
                }

                var task = await _taskService.CreateAdhocTaskAsync(dto);
                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(ApiResponse<AdhocTaskDto>.CreateSuccess(task, "Task created successfully."));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating adhoc task");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return err;
            }
        }

        [Function("UpdateAdhocTask")]
        public async Task<HttpResponseData> UpdateAdhocTask(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "tasks/adhoc/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var taskId))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Task ID."));
                return bad;
            }

            try
            {
                var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<UpdateAdhocTaskDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (dto == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid request payload."));
                    return bad;
                }

                var updated = await _taskService.UpdateAdhocTaskAsync(taskId, dto);
                if (updated == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Task not found."));
                    return notFound;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<AdhocTaskDto>.CreateSuccess(updated, "Task updated successfully."));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating adhoc task");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return err;
            }
        }

        [Function("DeleteAdhocTask")]
        public async Task<HttpResponseData> DeleteAdhocTask(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tasks/adhoc/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var taskId))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Task ID."));
                return bad;
            }

            try
            {
                bool deleted = await _taskService.DeleteAdhocTaskAsync(taskId);
                if (!deleted)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Task not found."));
                    return notFound;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<bool>.CreateSuccess(true, "Task deleted successfully."));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting adhoc task");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return err;
            }
        }

        #endregion

        #region Regular Weekly Tasks Endpoint

        [Function("GetWeeklyRegularTasks")]
        public async Task<HttpResponseData> GetWeeklyRegularTasks(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tasks/regular/weekly")] HttpRequestData req)
        {
            _logger.LogInformation("Getting weekly regular tasks schedule.");

            try
            {
                var query = HttpUtility.ParseQueryString(req.Url.Query);
                DateTime weekDate = DateTime.UtcNow.Date;

                if (DateTime.TryParse(query["weekDate"], out var parsedDate))
                {
                    weekDate = parsedDate;
                }

                var calendar = await _taskService.GetWeeklyRegularTasksAsync(weekDate);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<WeeklyTaskCalendarDto>.CreateSuccess(calendar));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting weekly regular tasks");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return err;
            }
        }

        #endregion

        #region Photo Evidence Endpoints

        [Function("CreateTaskPhoto")]
        public async Task<HttpResponseData> CreateTaskPhoto(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tasks/photos")] HttpRequestData req)
        {
            _logger.LogInformation("Saving task photo metadata.");

            try
            {
                var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<CreateTaskPhotoDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (dto == null || string.IsNullOrWhiteSpace(dto.FilePath) || string.IsNullOrWhiteSpace(dto.WatermarkAddress))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteAsJsonAsync(ApiResponse<object>.CreateError("FilePath and WatermarkAddress are required."));
                    return bad;
                }

                var photo = await _taskService.CreateTaskPhotoAsync(dto);
                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(ApiResponse<TaskPhotoDto>.CreateSuccess(photo, "Photo metadata saved."));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving task photo metadata");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return err;
            }
        }

        [Function("MarkPhotosSent")]
        public async Task<HttpResponseData> MarkPhotosSent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tasks/photos/mark-sent")] HttpRequestData req)
        {
            _logger.LogInformation("Marking photos as sent.");

            try
            {
                var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<MarkPhotosSentDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (dto == null || dto.PhotoIds == null || dto.PhotoIds.Count == 0)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteAsJsonAsync(ApiResponse<object>.CreateError("PhotoIds cannot be empty."));
                    return bad;
                }

                int updatedCount = await _taskService.MarkPhotosSentAsync(dto);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<int>.CreateSuccess(updatedCount, $"Marked {updatedCount} photos as sent."));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking photos as sent");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return err;
            }
        }

        [Function("CleanupPhotos")]
        public async Task<HttpResponseData> CleanupPhotos(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tasks/photos/cleanup")] HttpRequestData req)
        {
            _logger.LogInformation("Cleaning up task photos metadata.");

            try
            {
                var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
                var dto = string.IsNullOrWhiteSpace(bodyText)
                    ? new PhotoCleanupRequestDto()
                    : JsonSerializer.Deserialize<PhotoCleanupRequestDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new PhotoCleanupRequestDto();

                var result = await _taskService.CleanupPhotosAsync(dto);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResponse<PhotoCleanupResultDto>.CreateSuccess(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up photos");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(ApiResponse<object>.CreateError(ex.Message));
                return err;
            }
        }

        #endregion
    }
}
