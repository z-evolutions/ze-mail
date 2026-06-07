using ZeMail.Core.Entities;

namespace ZeMail.Core.Interfaces;

public interface ICalendarService
{
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(Guid accountId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<CalendarEvent?>               GetEventAsync(Guid id, CancellationToken ct = default);
    Task<CalendarEvent>                CreateEventAsync(CalendarEvent ev, CancellationToken ct = default);
    Task<CalendarEvent>                UpdateEventAsync(CalendarEvent ev, CancellationToken ct = default);
    Task                               DeleteEventAsync(Guid id, CancellationToken ct = default);

    /// <summary>RSVP per Mail — sendet Accept/Decline als iCal-Reply</summary>
    Task SendRsvpAsync(Guid eventId, bool accepted, CancellationToken ct = default);
}