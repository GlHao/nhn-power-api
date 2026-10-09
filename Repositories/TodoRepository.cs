using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NHN.Power.API.Data;
using NHN.Power.API.Models;

namespace NHN.Power.API.Repositories
{
    public class TodoRepository : ITodoRepository
    {
        private readonly AppDbContext _context;

        public TodoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<TodoGroup>> GetAllGroupsWithItemsAsync()
        {
            return await _context.TodoGroups
                .AsNoTracking()
                .Include(g => g.Items)
                .OrderBy(g => g.SortOrder)
                .ThenBy(g => g.CreatedAt)
                .ToListAsync();
        }

        public async Task<TodoGroup?> GetGroupByIdAsync(Guid id)
        {
            return await _context.TodoGroups
                .Include(g => g.Items)
                .FirstOrDefaultAsync(g => g.Id == id);
        }

        public async Task<TodoGroup> AddGroupAsync(TodoGroup group)
        {
            if (group.Id == Guid.Empty) group.Id = Guid.NewGuid();
            group.CreatedAt = DateTimeOffset.UtcNow;
            group.UpdatedAt = DateTimeOffset.UtcNow;

            _context.TodoGroups.Add(group);
            await _context.SaveChangesAsync();
            return group;
        }

        public async Task UpdateGroupAsync(TodoGroup group)
        {
            group.UpdatedAt = DateTimeOffset.UtcNow;
            _context.TodoGroups.Update(group);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteGroupAsync(Guid id)
        {
            var group = await _context.TodoGroups.FirstOrDefaultAsync(g => g.Id == id);
            if (group != null)
            {
                _context.TodoGroups.Remove(group);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<TodoItem?> GetItemByIdAsync(Guid id)
        {
            return await _context.TodoItems.FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<TodoItem> AddItemAsync(TodoItem item)
        {
            if (item.Id == Guid.Empty) item.Id = Guid.NewGuid();
            item.CreatedAt = DateTimeOffset.UtcNow;
            item.UpdatedAt = DateTimeOffset.UtcNow;

            _context.TodoItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public async Task UpdateItemAsync(TodoItem item)
        {
            item.UpdatedAt = DateTimeOffset.UtcNow;
            _context.TodoItems.Update(item);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteItemAsync(Guid id)
        {
            var item = await _context.TodoItems.FirstOrDefaultAsync(i => i.Id == id);
            if (item != null)
            {
                _context.TodoItems.Remove(item);
                await _context.SaveChangesAsync();
            }
        }
    }
}
