namespace ZeMail.Core.Entities;

public class Folder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public uint UidValidity { get; set; }
    public ulong HighestModSeq { get; set; }
    public string TrashMode { get; set; } = "ImapTrash";
    public string CacheMode { get; set; } = "HeaderOnly";
    public bool IsSystem { get; set; } = false;
    public DateTime LastSyncedAtUtc { get; set; } = DateTime.MinValue;

    public Account Account { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}