using ZeMail.Core.Entities;
using ZeMail.Core.Models;

namespace ZeMail.Core.Interfaces;

public interface IContactService
{
    Task<List<Contact>> SearchAsync(string query, Guid? accountId = null, CancellationToken ct = default);
    Task<Contact?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Contact> CreateAsync(Contact contact, CancellationToken ct = default);
    Task UpdateAsync(Contact contact, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Importiert vCard-Datei (einzeln oder mehrere VCARD-Blöcke).</summary>
    Task<ImportResult> ImportVCardAsync(string vCardContent, Guid? accountId = null, CancellationToken ct = default);

    /// <summary>Exportiert alle Kontakte des Accounts als vCard 3.0.</summary>
    Task<string> ExportVCardAsync(Guid? accountId = null, CancellationToken ct = default);

    /// <summary>Autovervollständigung für die An/CC-Zeile.</summary>
    Task<List<ContactSuggestion>> SuggestAsync(string input, Guid? accountId = null, CancellationToken ct = default);
}