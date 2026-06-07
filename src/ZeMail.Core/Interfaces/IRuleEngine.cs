using ZeMail.Core.Entities;

namespace ZeMail.Core.Interfaces;

public interface IRuleEngine
{
    /// <summary>
    /// Prüft alle aktiven Regeln des Accounts gegen die Nachricht
    /// und führt zutreffende Aktionen aus.
    /// Gibt true zurück wenn mindestens eine Regel ausgelöst hat.
    /// </summary>
    Task<bool> ApplyRulesAsync(Message message, Guid accountId, CancellationToken ct = default);
}