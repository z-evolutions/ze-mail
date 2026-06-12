namespace ZeMail.Core.Entities;

public class Calendar
{
    public Guid   Id        { get; set; } = Guid.NewGuid();
    public Guid   AccountId { get; set; }

    public string Name      { get; set; } = string.Empty;
    public string Color     { get; set; } = "#3a3aff";
    public bool   IsDefault { get; set; } = false;
    public bool   IsVisible { get; set; } = true;

    public string? CalDavUrl  { get; set; }
    public string? CalDavSync { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public Account             Account { get; set; } = null!;
    public List<CalendarEvent> Events  { get; set; } = [];
}