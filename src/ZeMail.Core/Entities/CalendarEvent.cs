namespace ZeMail.Core.Entities;

public class CalendarEvent
{
    public Guid  Id         { get; set; } = Guid.NewGuid();
    public Guid  AccountId  { get; set; }
    public Guid? CalendarId { get; set; }

    public string  Title       { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location    { get; set; }

    public DateTime StartUtc { get; set; }
    public DateTime EndUtc   { get; set; }
    public bool     IsAllDay { get; set; }

    public string? RecurrenceRule { get; set; }
    public string? ICalRaw        { get; set; }
    public string? CalDavEtag     { get; set; }
    public string? CalDavHref     { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Account   Account  { get; set; } = null!;
    public Calendar? Calendar { get; set; }
}