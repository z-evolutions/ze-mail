using ZeMail.Core.Entities;

namespace ZeMail.Core.Interfaces;

public interface ICalendarSyncService
{
    /// <summary>Synchronisiert einen einzelnen CalDAV-Kalender (Pull vom Server).</summary>
    Task SyncCalendarAsync(Calendar calendar, CancellationToken ct = default);

    /// <summary>Testet die Verbindung zu einem CalDAV-Server. Gibt null zurück wenn OK, sonst Fehlermeldung.</summary>
    Task<string?> TestConnectionAsync(string serverUrl, string username, string password, CancellationToken ct = default);

    /// <summary>
    /// Lädt ein lokal geändertes Event per CalDAV PUT zum Server hoch.
    /// Erzeugt eine neue href falls das Event noch nicht auf dem Server existiert.
    /// Aktualisiert ev.CalDavHref und ev.CalDavEtag mit den Werten vom Server.
    /// Erfordert dass ev.Calendar gesetzt ist (Navigation Property).
    /// </summary>
    Task PushEventAsync(CalendarEvent ev, CancellationToken ct = default);
}