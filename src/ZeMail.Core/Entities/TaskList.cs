namespace ZeMail.Core.Entities;

public class TaskList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#7070ff";   // Hex
    public string Icon { get; set; } = "📋";
    public bool IsSystem { get; set; } = false;       // true = Standardliste, nicht löschbar
    public string? SystemKey { get; set; }            // "myday" | "important" | "planned" | "all"
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Account Account { get; set; } = null!;
    public ICollection<TaskItem> Tasks { get; set; } = [];
}