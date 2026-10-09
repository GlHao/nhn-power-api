using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NHN.Power.API.DTOs.Todo;

namespace NHN.Power.API.Services
{
    public interface ITodoService
    {
        Task<IEnumerable<TodoGroupDto>> GetAllGroupsAsync();
        Task<TodoGroupDto?> CreateGroupAsync(CreateTodoGroupDto dto);
        Task<TodoGroupDto?> UpdateGroupAsync(Guid id, UpdateTodoGroupDto dto);
        Task<bool> DeleteGroupAsync(Guid id);

        Task<TodoItemDto?> CreateItemAsync(CreateTodoItemDto dto);
        Task<TodoItemDto?> UpdateItemAsync(Guid id, UpdateTodoItemDto dto);
        Task<bool> ToggleItemCompletionAsync(Guid id);
        Task<bool> DeleteItemAsync(Guid id);
    }
}
