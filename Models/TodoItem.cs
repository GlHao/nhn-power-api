using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NHN.Power.API.Models
{
    [Table("todo_items")]
    public class TodoItem
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("group_id")]
        public Guid GroupId { get; set; }

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("due_date", TypeName = "date")]
        public DateTime? DueDate { get; set; }

        [Column("is_completed")]
        public bool IsCompleted { get; set; } = false;

        [Column("completed_at")]
        public DateTimeOffset? CompletedAt { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [ForeignKey("GroupId")]
        public TodoGroup? Group { get; set; }
    }
}
