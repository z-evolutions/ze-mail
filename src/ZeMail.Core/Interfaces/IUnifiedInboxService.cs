using ZeMail.Core.Entities;

namespace ZeMail.Core.Interfaces;

public interface IUnifiedInboxService
{
    /// <summary>
    /// Gibt alle Nachrichten aus INBOX-Ordnern aller Accounts
    /// mit UnifiedInboxEnabled = true zurück, sortiert nach Empfangsdatum.
    /// </summary>
    Task<List<Message>> GetMessagesAsync(
        int skip = 0,
        int take = 50,
        bool unreadOnly = false,
        CancellationToken ct = default);

    /// <summary>Anzahl ungelesener Mails über alle Unified-Inbox-Accounts.</summary>
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
}