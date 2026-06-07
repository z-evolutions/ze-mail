namespace ZeMail.Core.Entities;

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FolderId { get; set; }
    public uint Uid { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string ToAddresses { get; set; } = string.Empty;
    public string CcAddresses { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public bool IsRead { get; set; } = false;
    public bool IsStarred { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public string HeadersJson { get; set; } = string.Empty;
    public string? BodyText { get; set; }
    public string? BodyHtml { get; set; }

    public Folder Folder { get; set; } = null!;
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}