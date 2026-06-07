namespace ZeMail.Core.Models;

public sealed class SearchResult
{
    public Guid MessageId { get; set; }
    public Guid FolderId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public double Rank { get; set; }
}