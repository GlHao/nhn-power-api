using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs.Todo;
using NHN.Power.API.Models;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    public class TodoListFunction
    {
        private readonly ITodoService _todoService;
        private readonly ILogger<TodoListFunction> _logger;

        public TodoListFunction(ITodoService todoService, ILogger<TodoListFunction> logger)
        {
            _todoService = todoService;
            _logger = logger;
        }

        [Function("GetTodoGroups")]
        public async Task<HttpResponseData> GetTodoGroups(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todo/groups")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all todo groups.");
            var groups = await _todoService.GetAllGroupsAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<object>.CreateSuccess(groups));
            return response;
        }

        [Function("CreateTodoGroup")]
        public async Task<HttpResponseData> CreateTodoGroup(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todo/groups")] HttpRequestData req)
        {
            var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
            var dto = JsonSerializer.Deserialize<CreateTodoGroupDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Group name is required."));
                return badReq;
            }

            var group = await _todoService.CreateGroupAsync(dto);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<TodoGroupDto>.CreateSuccess(group!));
            return response;
        }

        [Function("UpdateTodoGroup")]
        public async Task<HttpResponseData> UpdateTodoGroup(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todo/groups/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var groupId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Group ID format."));
                return badReq;
            }

            var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
            var dto = JsonSerializer.Deserialize<UpdateTodoGroupDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto == null)
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid request payload."));
                return badReq;
            }

            var group = await _todoService.UpdateGroupAsync(groupId, dto);
            if (group == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Todo group not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<TodoGroupDto>.CreateSuccess(group));
            return response;
        }

        [Function("DeleteTodoGroup")]
        public async Task<HttpResponseData> DeleteTodoGroup(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todo/groups/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var groupId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Group ID format."));
                return badReq;
            }

            bool deleted = await _todoService.DeleteGroupAsync(groupId);
            if (!deleted)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Todo group not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<string>.CreateSuccess("Todo group deleted."));
            return response;
        }

        [Function("CreateTodoItem")]
        public async Task<HttpResponseData> CreateTodoItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todo/items")] HttpRequestData req)
        {
            var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
            var dto = JsonSerializer.Deserialize<CreateTodoItemDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto == null || dto.GroupId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Title))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("GroupId and Title are required."));
                return badReq;
            }

            var item = await _todoService.CreateItemAsync(dto);
            if (item == null)
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Specified group does not exist."));
                return badReq;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<TodoItemDto>.CreateSuccess(item));
            return response;
        }

        [Function("UpdateTodoItem")]
        public async Task<HttpResponseData> UpdateTodoItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todo/items/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var itemId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Item ID format."));
                return badReq;
            }

            var bodyText = await new StreamReader(req.Body).ReadToEndAsync();
            var dto = JsonSerializer.Deserialize<UpdateTodoItemDto>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto == null)
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid request payload."));
                return badReq;
            }

            var item = await _todoService.UpdateItemAsync(itemId, dto);
            if (item == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Todo item not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<TodoItemDto>.CreateSuccess(item));
            return response;
        }

        [Function("ToggleTodoItem")]
        public async Task<HttpResponseData> ToggleTodoItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todo/items/{id}/toggle")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var itemId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Item ID format."));
                return badReq;
            }

            bool success = await _todoService.ToggleItemCompletionAsync(itemId);
            if (!success)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Todo item not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<string>.CreateSuccess("Item status toggled."));
            return response;
        }

        [Function("DeleteTodoItem")]
        public async Task<HttpResponseData> DeleteTodoItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todo/items/{id}")] HttpRequestData req,
            string id)
        {
            if (!Guid.TryParse(id, out var itemId))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteAsJsonAsync(ApiResponse<object>.CreateError("Invalid Item ID format."));
                return badReq;
            }

            bool deleted = await _todoService.DeleteItemAsync(itemId);
            if (!deleted)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(ApiResponse<object>.CreateError("Todo item not found."));
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(ApiResponse<string>.CreateSuccess("Todo item deleted."));
            return response;
        }
    }
}
