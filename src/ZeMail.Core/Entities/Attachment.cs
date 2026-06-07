namespace ZeMail.Core.Entities;

public class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? LocalPath { get; set; }
    public byte[]? BlobData { get; set; }

    public Message Message { get; set; } = null!;
}