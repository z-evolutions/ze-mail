namespace ZeMail.Core.Entities;

public class PendingOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int Retries { get; set; } = 0;
    public string? LastErrorMessage { get; set; }

    public Account Account { get; set; } = null!;
}