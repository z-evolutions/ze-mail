using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;
using ZeMail.Core.Models;

namespace ZeMail.Application;

public class ContactService : IContactService
{
    private readonly IZeMailDbContext _db;
    private readonly ILogger<ContactService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ContactService(IZeMailDbContext db, ILogger<ContactService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public Task<List<Contact>> SearchAsync(string query, Guid? accountId = null, CancellationToken ct = default)
    {
        var q = _db.Contacts.AsEnumerable();

        if (accountId.HasValue)
            q = q.Where(c => c.AccountId == null || c.AccountId == accountId);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(c =>
                c.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.EmailsJson.Contains(query, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(q.OrderBy(c => c.DisplayName).ToList());
    }

    public Task<Contact?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_db.Contacts.FirstOrDefault(c => c.Id == id));

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<Contact> CreateAsync(Contact contact, CancellationToken ct = default)
    {
        var primaryEmail = GetPrimaryEmail(contact.EmailsJson);
        if (primaryEmail is not null)
        {
            var exists = _db.Contacts.AsEnumerable()
                .Any(c => c.AccountId == contact.AccountId &&
                           GetPrimaryEmail(c.EmailsJson) == primaryEmail);
            if (exists)
                throw new InvalidOperationException($"Kontakt mit E-Mail '{primaryEmail}' existiert bereits.");
        }

        contact.CreatedAtUtc = DateTime.UtcNow;
        contact.UpdatedAtUtc = DateTime.UtcNow;
        _db.Add(contact);
        await _db.SaveChangesAsync(ct);
        return contact;
    }

    public async Task UpdateAsync(Contact contact, CancellationToken ct = default)
    {
        contact.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var contact = _db.Contacts.FirstOrDefault(c => c.Id == id);
        if (contact is null) return;
        _db.Remove(contact);
        await _db.SaveChangesAsync(ct);
    }

    // ── vCard Import ──────────────────────────────────────────────────────────

    public async Task<ImportResult> ImportVCardAsync(string vCardContent, Guid? accountId = null, CancellationToken ct = default)
    {
        var blocks = SplitVCards(vCardContent);
        int imported = 0, skipped = 0, failed = 0;
        var errors = new List<string>();

        foreach (var block in blocks)
        {
            try
            {
                var contact = ParseVCard(block, accountId);
                if (contact is null) { failed++; continue; }

                var primaryEmail = GetPrimaryEmail(contact.EmailsJson);
                var duplicate = primaryEmail is not null && _db.Contacts.AsEnumerable()
                    .Any(c => c.AccountId == accountId &&
                               GetPrimaryEmail(c.EmailsJson) == primaryEmail);

                if (duplicate) { skipped++; continue; }

                contact.CreatedAtUtc = DateTime.UtcNow;
                contact.UpdatedAtUtc = DateTime.UtcNow;
                _db.Add(contact);
                imported++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add(ex.Message);
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("vCard import: {I} imported, {S} skipped, {F} failed", imported, skipped, failed);
        return new ImportResult(imported, skipped, failed, errors);
    }

    // ── vCard Export ──────────────────────────────────────────────────────────

    public Task<string> ExportVCardAsync(Guid? accountId = null, CancellationToken ct = default)
    {
        var contacts = _db.Contacts.AsEnumerable();
        if (accountId.HasValue)
            contacts = contacts.Where(c => c.AccountId == null || c.AccountId == accountId);

        var sb = new StringBuilder();
        foreach (var c in contacts)
            sb.AppendLine(c.VCardRaw is not null ? c.VCardRaw.TrimEnd() : BuildVCard(c));

        return Task.FromResult(sb.ToString());
    }

    // ── Suggest ───────────────────────────────────────────────────────────────

    public Task<List<ContactSuggestion>> SuggestAsync(string input, Guid? accountId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult(new List<ContactSuggestion>());

        var contacts = _db.Contacts.AsEnumerable()
            .Where(c => accountId == null || c.AccountId == null || c.AccountId == accountId)
            .Where(c => c.DisplayName.Contains(input, StringComparison.OrdinalIgnoreCase) ||
                        c.EmailsJson.Contains(input, StringComparison.OrdinalIgnoreCase));

        var suggestions = new List<ContactSuggestion>();
        foreach (var c in contacts)
        {
            var emails = DeserializeEmails(c.EmailsJson);
            foreach (var email in emails.Where(e => e.Contains(input, StringComparison.OrdinalIgnoreCase)))
                suggestions.Add(new ContactSuggestion(c.Id, c.DisplayName, email));
        }

        return Task.FromResult(suggestions.Take(10).ToList());
    }

    // ── vCard Helpers ─────────────────────────────────────────────────────────

    private static List<string> SplitVCards(string content)
    {
        var blocks = new List<string>();
        var lines = content.ReplaceLineEndings("\n").Split('\n');
        var current = new StringBuilder();
        bool inCard = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Equals("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase))
            {
                current.Clear();
                inCard = true;
            }
            if (inCard) current.AppendLine(trimmed);
            if (trimmed.Equals("END:VCARD", StringComparison.OrdinalIgnoreCase) && inCard)
            {
                blocks.Add(current.ToString());
                inCard = false;
            }
        }

        return blocks;
    }

    private static Contact? ParseVCard(string block, Guid? accountId)
    {
        var lines = block.ReplaceLineEndings("\n").Split('\n');
        string displayName = string.Empty;
        var emails = new List<string>();
        string? phone = null;
        string? org = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("FN:", StringComparison.OrdinalIgnoreCase))
                displayName = line[3..].Trim();
            else if (line.StartsWith("EMAIL", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
            {
                var val = line[(line.IndexOf(':') + 1)..].Trim();
                if (!string.IsNullOrEmpty(val)) emails.Add(val);
            }
            else if (line.StartsWith("TEL", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
            {
                var val = line[(line.IndexOf(':') + 1)..].Trim();
                if (!string.IsNullOrEmpty(val)) phone = val;
            }
            else if (line.StartsWith("ORG:", StringComparison.OrdinalIgnoreCase))
                org = line[4..].Trim();
        }

        if (string.IsNullOrWhiteSpace(displayName) && emails.Count == 0) return null;
        if (string.IsNullOrWhiteSpace(displayName)) displayName = emails[0];

        return new Contact
        {
            AccountId    = accountId,
            DisplayName  = displayName,
            EmailsJson   = JsonSerializer.Serialize(emails),
            PhonesJson   = JsonSerializer.Serialize(phone is null ? Array.Empty<string>() : new[] { phone }),
            Organization = org,
            VCardRaw     = block
        };
    }

    private static string BuildVCard(Contact c)
    {
        var emails = DeserializeEmails(c.EmailsJson);
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCARD");
        sb.AppendLine("VERSION:3.0");
        sb.AppendLine($"FN:{c.DisplayName}");
        foreach (var email in emails)
            sb.AppendLine($"EMAIL;TYPE=INTERNET:{email}");
        if (!string.IsNullOrEmpty(c.Organization))
            sb.AppendLine($"ORG:{c.Organization}");
        sb.AppendLine("END:VCARD");
        return sb.ToString().TrimEnd();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? GetPrimaryEmail(string emailsJson)
    {
        var list = DeserializeEmails(emailsJson);
        return list.Count > 0 ? list[0].ToLowerInvariant() : null;
    }

    private static List<string> DeserializeEmails(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json, _json) ?? new(); }
        catch { return new(); }
    }
}