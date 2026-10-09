using System;
using System.Collections.Generic;

namespace NHN.Power.API.DTOs.Todo
{
    public class TodoGroupDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public List<TodoItemDto> Items { get; set; } = new();
    }

    public class CreateTodoGroupDto
    {
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 0;
    }

    public class UpdateTodoGroupDto
    {
        public string? Name { get; set; }
        public int? SortOrder { get; set; }
    }

    public class TodoItemDto
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsCompleted { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CreateTodoItemDto
    {
        public Guid GroupId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class UpdateTodoItemDto
    {
        public string? Title { get; set; }
        public string? Notes { get; set; }
        public DateTime? DueDate { get; set; }
        public bool? IsCompleted { get; set; }
    }
}
