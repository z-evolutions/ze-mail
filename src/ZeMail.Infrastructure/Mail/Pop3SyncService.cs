using MailKit;
using MailKit.Net.Pop3;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;
using ZeMail.Infrastructure.Persistence;

namespace ZeMail.Infrastructure.Mail;

public sealed class Pop3SyncService : IPop3SyncService
{
    private readonly ZeMailDbContext _db;
    private readonly ILogger<Pop3SyncService> _logger;

    public Pop3SyncService(ZeMailDbContext db, ILogger<Pop3SyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SyncAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw new InvalidOperationException(
                $"Account {accountId} nicht gefunden.");

        // Synthetischen INBOX-Ordner holen oder anlegen
        var inbox = await GetOrCreateInboxAsync(account, ct);

        using var client = new Pop3Client();

        _logger.LogInformation(
            "POP3 verbinde: {Host}:{Port}", account.Pop3Host, account.Pop3Port);

        await client.ConnectAsync(
            account.Pop3Host,
            account.Pop3Port,
            SecureSocketOptions.SslOnConnect,
            ct);

        await client.AuthenticateAsync(account.Username, account.Password, ct);

        // Bereits bekannte UIDLs laden (Duplikat-Schutz)
        var knownUidls = (await _db.Messages
            .Where(m => m.FolderId == inbox.Id)
            .Select(m => m.MessageId)
            .ToListAsync(ct))
            .ToHashSet();

        int downloaded = 0;

        for (int i = 0; i < client.Count; i++)
        {
            var uidl = await client.GetMessageUidAsync(i, ct);

            // Bereits heruntergeladen → überspringen
            if (knownUidls.Contains(uidl))
                continue;

            var mime = await client.GetMessageAsync(i, ct);
            await SaveMessageAsync(inbox, mime, uidl, (uint)(i + 1), ct);
            downloaded++;

            // Server-Delete wenn gewünscht
            if (!account.Pop3LeaveOnServer)
                await client.DeleteMessageAsync(i, ct);
        }

        _logger.LogInformation(
            "POP3 Sync abgeschlossen: {Count} neue Nachrichten.", downloaded);

        await _db.SaveChangesAsync(ct);
        await client.DisconnectAsync(true, ct);
    }

    // ── MimeMessage in DB speichern ──────────────────────────────────────────
    private async Task SaveMessageAsync(
        Folder inbox, MimeMessage mime, string uidl, uint uid, CancellationToken ct)
    {
        var from = mime.From.Mailboxes.FirstOrDefault();

        var message = new Message
        {
            Id            = Guid.NewGuid(),
            FolderId      = inbox.Id,
            Uid           = uid,
            MessageId     = uidl, // UIDL als MessageId für Duplikat-Schutz
            Subject       = mime.Subject ?? string.Empty,
            FromAddress   = from?.Address ?? string.Empty,
            FromName      = from?.Name ?? string.Empty,
            ToAddresses   = string.Join(", ",
                                mime.To.Mailboxes.Select(m => m.Address)),
            CcAddresses   = string.Join(", ",
                                mime.Cc.Mailboxes.Select(m => m.Address)),
            SentAtUtc     = mime.Date.UtcDateTime,
            ReceivedAtUtc = DateTime.UtcNow,
            IsRead        = false,
            BodyText      = mime.TextBody,
            BodyHtml      = mime.HtmlBody,
        };

        _db.Messages.Add(message);

        // Anhänge speichern
        foreach (var attachment in mime.Attachments)
        {
            if (attachment is not MimePart part) continue;

            using var ms = new MemoryStream();
                if (part.Content is not null)
                    await part.Content.DecodeToAsync(ms, ct);

            _db.Attachments.Add(new Attachment
            {
                Id        = Guid.NewGuid(),
                MessageId = message.Id,
                FileName  = part.FileName ?? "attachment_{Guid.NewGuid():N}",
                MimeType  = part.ContentType.MimeType,
                SizeBytes = ms.Length,
                BlobData  = ms.ToArray(),
            });
        }
    }

    // ── Synthetischen INBOX-Ordner holen oder anlegen ───────────────────────
    private async Task<Folder> GetOrCreateInboxAsync(Account account, CancellationToken ct)
    {
        var inbox = await _db.Folders
            .FirstOrDefaultAsync(f =>
                f.AccountId == account.Id && f.FullPath == "INBOX", ct);

        if (inbox is not null)
            return inbox;

        inbox = new Folder
        {
            Id        = Guid.NewGuid(),
            AccountId = account.Id,
            Name      = "INBOX",
            FullPath  = "INBOX",
            IsSystem  = true,
        };

        _db.Folders.Add(inbox);
        await _db.SaveChangesAsync(ct);

        return inbox;
    }
}