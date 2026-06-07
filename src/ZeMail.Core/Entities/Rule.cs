using ZeMail.Core.Enums;

namespace ZeMail.Core.Entities;

public class Rule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; } = 0;          // niedrig = zuerst ausgeführt
    public bool IsActive { get; set; } = true;
    public bool StopProcessing { get; set; } = false; // nach dieser Regel aufhören?
    public string ConditionsJson { get; set; } = "[]";
    public string ActionsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Account Account { get; set; } = null!;
}