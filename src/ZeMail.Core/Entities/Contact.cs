namespace ZeMail.Core.Entities;

public class Contact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AccountId { get; set; }          // null = global (kontoübergreifend)
    public string DisplayName { get; set; } = string.Empty;
    public string EmailsJson { get; set; } = "[]"; // string[] als JSON
    public string PhonesJson { get; set; } = "[]"; // string[] als JSON
    public string? Organization { get; set; }
    public string? Notes { get; set; }
    public string? VCardRaw { get; set; }          // Original vCard 3/4 für Export
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Account? Account { get; set; }
}