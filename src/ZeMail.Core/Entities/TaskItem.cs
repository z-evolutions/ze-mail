using ZeMail.Core.Enums;

namespace ZeMail.Core.Entities;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public Guid? TaskListId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DueUtc { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAtUtc { get; set; }
    public bool IsImportant { get; set; } = false;
    public bool IsMyDay { get; set; } = false;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public int SortOrder { get; set; } = 0;           // für Drag&Drop-Reihenfolge
    public Guid? LinkedMessageId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Account Account { get; set; } = null!;
    public TaskList? TaskList { get; set; }
    public Message? LinkedMessage { get; set; }
}