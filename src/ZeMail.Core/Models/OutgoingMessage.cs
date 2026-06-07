namespace ZeMail.Core.Models;

public sealed class OutgoingMessage
{
    public Guid AccountId { get; set; }

    public List<string> To { get; set; } = [];
    public List<string> Cc { get; set; } = [];
    public List<string> Bcc { get; set; } = [];

    public string Subject { get; set; } = string.Empty;
    public string? BodyText { get; set; }
    public string? BodyHtml { get; set; }

    public List<AttachmentFile> Attachments { get; set; } = [];
}

public sealed class AttachmentFile
{
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
}