using ZeMail.Core.Enums;

namespace ZeMail.Core.Entities;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DueUtc { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAtUtc { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public Guid? LinkedMessageId { get; set; }    // optional: verknüpfte Mail
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Account Account { get; set; } = null!;
    public Message? LinkedMessage { get; set; }
}