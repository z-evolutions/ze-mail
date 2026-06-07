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

    public string Pop3Host { get; set; } = string.Empty;
    public int Pop3Port { get; set; } = 995;
    public bool Pop3LeaveOnServer { get; set; } = true;
    public string Protocol { get; set; } = "IMAP";
    public string TlsMode { get; set; } = "Implicit";

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // temporär – später Keystore
    public string AccentColor { get; set; } = "#5AC8FA";
    public bool UnifiedInboxEnabled { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Rule> Rules { get; set; } = new List<Rule>();
    public ICollection<Signature> Signatures { get; set; } = new List<Signature>();
    public ICollection<Folder> Folders { get; set; } = new List<Folder>();
}