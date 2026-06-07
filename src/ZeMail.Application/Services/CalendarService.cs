using Microsoft.Extensions.Logging;
using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;

namespace ZeMail.Application.Services;

public class CalendarService : ICalendarService
{
    private readonly IZeMailDbContext    _db;
    private readonly ISmtpSenderService  _smtp;
    private readonly ILogger<CalendarService> _log;

    public CalendarService(
        IZeMailDbContext     db,
        ISmtpSenderService   smtp,
        ILogger<CalendarService> log)
    {
        _db   = db;
        _smtp = smtp;
        _log  = log;
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        Guid accountId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var list = _db.CalendarEvents
            .Where(e => e.AccountId == accountId
                     && e.StartUtc  <  to
                     && e.EndUtc    >= from)
            .OrderBy(e => e.StartUtc)
            .ToList();

        return await Task.FromResult(list);
    }

    public async Task<CalendarEvent?> GetEventAsync(Guid id, CancellationToken ct = default)
        => await Task.FromResult(_db.CalendarEvents.FirstOrDefault(e => e.Id == id));

    public async Task<CalendarEvent> CreateEventAsync(CalendarEvent ev, CancellationToken ct = default)
    {
        ev.Id          = Guid.NewGuid();
        ev.CreatedAtUtc = DateTime.UtcNow;
        ev.UpdatedAtUtc = DateTime.UtcNow;
        _db.Add(ev);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("CalendarEvent {Id} created", ev.Id);
        return ev;
    }

    public async Task<CalendarEvent> UpdateEventAsync(CalendarEvent ev, CancellationToken ct = default)
    {
        ev.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("CalendarEvent {Id} updated", ev.Id);
        return ev;
    }

    public async Task DeleteEventAsync(Guid id, CancellationToken ct = default)
    {
        var ev = _db.CalendarEvents.FirstOrDefault(e => e.Id == id);
        if (ev is null) return;
        _db.Remove(ev);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("CalendarEvent {Id} deleted", id);
    }

    public async Task SendRsvpAsync(Guid eventId, bool accepted, CancellationToken ct = default)
    {
        var ev = _db.CalendarEvents.FirstOrDefault(e => e.Id == eventId)
                ?? throw new InvalidOperationException($"Event {eventId} not found");

        var account = _db.Accounts.FirstOrDefault(a => a.Id == ev.AccountId)
                    ?? throw new InvalidOperationException("Account not found");

        var status = accepted ? "ACCEPTED" : "DECLINED";

        var iCalReply = $"""
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//ZE-Mail//ZE-Mail 1.0//EN
            METHOD:REPLY
            BEGIN:VEVENT
            UID:{eventId}
            DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}
            DTSTART:{ev.StartUtc:yyyyMMddTHHmmssZ}
            DTEND:{ev.EndUtc:yyyyMMddTHHmmssZ}
            SUMMARY:{ev.Title}
            ATTENDEE;PARTSTAT={status}:mailto:{account.EmailAddress}
            END:VEVENT
            END:VCALENDAR
            """;

        var outgoing = new ZeMail.Core.Models.OutgoingMessage
        {
            AccountId    = ev.AccountId,
            To           = [ev.Description ?? ""],
            Subject      = $"{(accepted ? "Zugesagt" : "Abgesagt")}: {ev.Title}",
            BodyText     = accepted
                        ? $"Ich nehme an dem Termin \"{ev.Title}\" teil."
                        : $"Ich kann an dem Termin \"{ev.Title}\" leider nicht teilnehmen.",
            ICalPayload  = iCalReply
        };

        await _smtp.SendAsync(outgoing, ct);

        _log.LogInformation("RSVP {Status} sent for event {Id}", status, eventId);
    }
}