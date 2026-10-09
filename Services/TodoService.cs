using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NHN.Power.API.DTOs.Todo;
using NHN.Power.API.Models;
using NHN.Power.API.Repositories;

namespace NHN.Power.API.Services
{
    public class TodoService : ITodoService
    {
        private readonly ITodoRepository _todoRepository;

        public TodoService(ITodoRepository todoRepository)
        {
            _todoRepository = todoRepository;
        }

        public async Task<IEnumerable<TodoGroupDto>> GetAllGroupsAsync()
        {
            var groups = await _todoRepository.GetAllGroupsWithItemsAsync();
            return groups.Select(MapGroupToDto);
        }

        public async Task<TodoGroupDto?> CreateGroupAsync(CreateTodoGroupDto dto)
        {
            var group = new TodoGroup
            {
                Id = Guid.NewGuid(),
                Name = dto.Name.Trim(),
                SortOrder = dto.SortOrder,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _todoRepository.AddGroupAsync(group);
            return MapGroupToDto(group);
        }

        public async Task<TodoGroupDto?> UpdateGroupAsync(Guid id, UpdateTodoGroupDto dto)
        {
            var group = await _todoRepository.GetGroupByIdAsync(id);
            if (group == null) return null;

            if (dto.Name != null) group.Name = dto.Name.Trim();
            if (dto.SortOrder.HasValue) group.SortOrder = dto.SortOrder.Value;

            await _todoRepository.UpdateGroupAsync(group);
            return MapGroupToDto(group);
        }

        public async Task<bool> DeleteGroupAsync(Guid id)
        {
            var group = await _todoRepository.GetGroupByIdAsync(id);
            if (group == null) return false;

            await _todoRepository.DeleteGroupAsync(id);
            return true;
        }

        public async Task<TodoItemDto?> CreateItemAsync(CreateTodoItemDto dto)
        {
            var group = await _todoRepository.GetGroupByIdAsync(dto.GroupId);
            if (group == null) return null;

            var item = new TodoItem
            {
                Id = Guid.NewGuid(),
                GroupId = dto.GroupId,
                Title = dto.Title.Trim(),
                Notes = dto.Notes?.Trim(),
                DueDate = dto.DueDate,
                IsCompleted = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _todoRepository.AddItemAsync(item);
            return MapItemToDto(item);
        }

        public async Task<TodoItemDto?> UpdateItemAsync(Guid id, UpdateTodoItemDto dto)
        {
            var item = await _todoRepository.GetItemByIdAsync(id);
            if (item == null) return null;

            if (dto.Title != null) item.Title = dto.Title.Trim();
            if (dto.Notes != null) item.Notes = dto.Notes.Trim();
            if (dto.DueDate.HasValue) item.DueDate = dto.DueDate.Value;
            if (dto.IsCompleted.HasValue)
            {
                item.IsCompleted = dto.IsCompleted.Value;
                item.CompletedAt = item.IsCompleted ? DateTimeOffset.UtcNow : null;
            }

            await _todoRepository.UpdateItemAsync(item);
            return MapItemToDto(item);
        }

        public async Task<bool> ToggleItemCompletionAsync(Guid id)
        {
            var item = await _todoRepository.GetItemByIdAsync(id);
            if (item == null) return false;

            item.IsCompleted = !item.IsCompleted;
            item.CompletedAt = item.IsCompleted ? DateTimeOffset.UtcNow : null;

            await _todoRepository.UpdateItemAsync(item);
            return true;
        }

        public async Task<bool> DeleteItemAsync(Guid id)
        {
            var item = await _todoRepository.GetItemByIdAsync(id);
            if (item == null) return false;

            await _todoRepository.DeleteItemAsync(id);
            return true;
        }

        private TodoGroupDto MapGroupToDto(TodoGroup group)
        {
            var items = group.Items.OrderBy(i => i.IsCompleted).ThenByDescending(i => i.CreatedAt).Select(MapItemToDto).ToList();
            return new TodoGroupDto
            {
                Id = group.Id,
                Name = group.Name,
                SortOrder = group.SortOrder,
                TotalCount = items.Count,
                CompletedCount = items.Count(i => i.IsCompleted),
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt,
                Items = items
            };
        }

        private TodoItemDto MapItemToDto(TodoItem item)
        {
            return new TodoItemDto
            {
                Id = item.Id,
                GroupId = item.GroupId,
                Title = item.Title,
                Notes = item.Notes,
                DueDate = item.DueDate,
                IsCompleted = item.IsCompleted,
                CompletedAt = item.CompletedAt,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            };
        }
    }
}
