namespace ZeMail.Core.Entities;

public enum CalendarType
{
    Local  = 0,
    CalDav = 1
}

public class Calendar
{
    public Guid   Id        { get; set; } = Guid.NewGuid();
    public Guid   AccountId { get; set; }

    public string       Name    { get; set; } = string.Empty;
    public string       Color   { get; set; } = "#3a3aff";
    public bool         IsDefault { get; set; } = false;
    public bool         IsVisible { get; set; } = true;
    public CalendarType Type    { get; set; } = CalendarType.Local;

    // CalDAV
    public string? ServerUrl          { get; set; }
    public string? Username           { get; set; }
    public string? PasswordEncrypted  { get; set; }
    public int     SyncIntervalMinutes { get; set; } = 15;
    public string? CalDavSyncToken    { get; set; }
    public DateTime? LastSyncedAtUtc  { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Account             Account { get; set; } = null!;
    public List<CalendarEvent> Events  { get; set; } = [];
}