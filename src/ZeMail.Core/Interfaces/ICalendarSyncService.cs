using ZeMail.Core.Entities;

namespace ZeMail.Core.Interfaces;

public interface ICalendarSyncService
{
    /// <summary>Synchronisiert einen einzelnen CalDAV-Kalender.</summary>
    Task SyncCalendarAsync(Calendar calendar, CancellationToken ct = default);

    /// <summary>Testet die Verbindung zu einem CalDAV-Server. Gibt null zurück wenn OK, sonst Fehlermeldung.</summary>
    Task<string?> TestConnectionAsync(string serverUrl, string username, string password, CancellationToken ct = default);
}