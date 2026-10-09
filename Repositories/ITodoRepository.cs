using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NHN.Power.API.Models;

namespace NHN.Power.API.Repositories
{
    public interface ITodoRepository
    {
        Task<IEnumerable<TodoGroup>> GetAllGroupsWithItemsAsync();
        Task<TodoGroup?> GetGroupByIdAsync(Guid id);
        Task<TodoGroup> AddGroupAsync(TodoGroup group);
        Task UpdateGroupAsync(TodoGroup group);
        Task DeleteGroupAsync(Guid id);

        Task<TodoItem?> GetItemByIdAsync(Guid id);
        Task<TodoItem> AddItemAsync(TodoItem item);
        Task UpdateItemAsync(TodoItem item);
        Task DeleteItemAsync(Guid id);
    }
}
