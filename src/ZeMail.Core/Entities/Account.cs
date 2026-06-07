namespace ZeMail.Core.Entities;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string ImapHost { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 465;
    public string Protocol { get; set; } = "IMAP";
    public string TlsMode { get; set; } = "Implicit";
    public string AccentColor { get; set; } = "#5AC8FA";
    public bool UnifiedInboxEnabled { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Folder> Folders { get; set; } = new List<Folder>();
}