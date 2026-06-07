using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;
using ZeMail.Infrastructure.Persistence;

namespace ZeMail.Infrastructure.Mail;

public sealed class ImapSyncService : IImapSyncService
{
    private readonly ZeMailDbContext _db;
    private readonly ILogger<ImapSyncService> _logger;

    public ImapSyncService(ZeMailDbContext db, ILogger<ImapSyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Account-Sync: alle IMAP-Ordner synchronisieren ──────────────────────
    public async Task SyncAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw new InvalidOperationException($"Account {accountId} nicht gefunden.");

        using var client = await ConnectAsync(account, ct);

        await SyncFolderListAsync(account, client, ct);

        var folders = await _db.Folders
            .Where(f => f.AccountId == accountId)
            .ToListAsync(ct);

        foreach (var folder in folders)
        {
            try { await SyncFolderCoreAsync(account, folder, client, ct); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Sync von Ordner {Folder}", folder.FullPath);
            }
        }

        await client.DisconnectAsync(true, ct);
    }

    // ── Einzelnen Ordner synchronisieren ────────────────────────────────────
    public async Task SyncFolderAsync(Guid folderId, CancellationToken ct = default)
    {
        var folder = await _db.Folders
            .Include(f => f.Account)
            .FirstOrDefaultAsync(f => f.Id == folderId, ct)
            ?? throw new InvalidOperationException($"Folder {folderId} nicht gefunden.");

        using var client = await ConnectAsync(folder.Account, ct);
        await SyncFolderCoreAsync(folder.Account, folder, client, ct);
        await client.DisconnectAsync(true, ct);
    }

    // ── Kernlogik: Vollsync oder Delta-Sync ─────────────────────────────────
    private async Task SyncFolderCoreAsync(
        Account account, Folder localFolder,
        ImapClient client, CancellationToken ct)
    {
        var imapFolder = await client.GetFolderAsync(localFolder.FullPath, ct);
        await imapFolder.OpenAsync(FolderAccess.ReadOnly, ct);

        // UidValidity-Prüfung – bei Änderung: Cache leeren, Vollsync erzwingen
        if (localFolder.UidValidity != 0 &&
            localFolder.UidValidity != (uint)imapFolder.UidValidity)
        {
            _logger.LogWarning(
                "UidValidity geändert für {Folder} – Cache wird geleert.", localFolder.FullPath);
            await InvalidateFolderCacheAsync(localFolder, ct);
        }

        localFolder.UidValidity = (uint)imapFolder.UidValidity;

        bool supportsCondstore = client.Capabilities.HasFlag(ImapCapabilities.CondStore);
        bool isDeltaSync = supportsCondstore
                           && localFolder.HighestModSeq > 0
                           && imapFolder.HighestModSeq > localFolder.HighestModSeq;

        if (isDeltaSync)
            await DeltaSyncAsync(localFolder, imapFolder, ct);
        else
            await FullSyncAsync(localFolder, imapFolder, ct);

        if (supportsCondstore)
            localFolder.HighestModSeq = imapFolder.HighestModSeq;

        localFolder.LastSyncedAtUtc = DateTime.UtcNow;

        await imapFolder.CloseAsync(false, ct);
        await _db.SaveChangesAsync(ct);
    }

    // ── Vollsync ─────────────────────────────────────────────────────────────
    private async Task FullSyncAsync(
        Folder localFolder, IMailFolder imapFolder, CancellationToken ct)
    {
        _logger.LogInformation("Vollsync: {Folder}", localFolder.FullPath);

        var uids = await imapFolder.SearchAsync(SearchQuery.All, ct);
        if (uids.Count == 0) return;

        var summaries = await imapFolder.FetchAsync(
            uids,
            MessageSummaryItems.UniqueId |
            MessageSummaryItems.Flags |
            MessageSummaryItems.Envelope,
            ct);

        foreach (var summary in summaries)
            await UpsertMessageAsync(localFolder, summary, ct);
    }

    // ── Delta-Sync (CONDSTORE) ────────────────────────────────────────────────
    private async Task DeltaSyncAsync(
        Folder localFolder, IMailFolder imapFolder, CancellationToken ct)
    {
        _logger.LogInformation(
            "Delta-Sync: {Folder} ab ModSeq {ModSeq}",
            localFolder.FullPath, localFolder.HighestModSeq);

        var summaries = await imapFolder.FetchAsync(
            UniqueIdRange.All,
            localFolder.HighestModSeq,
            MessageSummaryItems.UniqueId |
            MessageSummaryItems.Flags |
            MessageSummaryItems.Envelope |
            MessageSummaryItems.ModSeq,
            ct);

        foreach (var summary in summaries)
            await UpsertMessageAsync(localFolder, summary, ct);

        await RemoveDeletedMessagesAsync(localFolder, imapFolder, ct);
    }

    // ── Nachricht einfügen oder aktualisieren ────────────────────────────────
    private async Task UpsertMessageAsync(
        Folder localFolder, IMessageSummary summary, CancellationToken ct)
    {
        var uid = (uint)summary.UniqueId.Id;
        var flags = summary.Flags ?? MessageFlags.None;

        var existing = await _db.Messages
            .FirstOrDefaultAsync(m => m.FolderId == localFolder.Id && m.Uid == uid, ct);

        if (existing is null)
        {
            _db.Messages.Add(new Message
            {
                Id            = Guid.NewGuid(),
                FolderId      = localFolder.Id,
                Uid           = uid,
                MessageId     = summary.Envelope?.MessageId ?? string.Empty,
                Subject       = summary.Envelope?.Subject ?? string.Empty,
                FromAddress   = summary.Envelope?.From?.Mailboxes.FirstOrDefault()?.Address ?? string.Empty,
                FromName      = summary.Envelope?.From?.Mailboxes.FirstOrDefault()?.Name ?? string.Empty,
                ToAddresses   = string.Join(", ", summary.Envelope?.To?.Mailboxes.Select(m => m.Address) ?? []),
                CcAddresses   = string.Join(", ", summary.Envelope?.Cc?.Mailboxes.Select(m => m.Address) ?? []),
                SentAtUtc     = summary.Envelope?.Date?.UtcDateTime ?? DateTime.UtcNow,
                ReceivedAtUtc = DateTime.UtcNow,
                IsRead        = flags.HasFlag(MessageFlags.Seen),
                IsStarred     = flags.HasFlag(MessageFlags.Flagged),
                IsDeleted     = flags.HasFlag(MessageFlags.Deleted),
            });
        }
        else
        {
            // Flags aktualisieren
            existing.IsRead    = flags.HasFlag(MessageFlags.Seen);
            existing.IsStarred = flags.HasFlag(MessageFlags.Flagged);
            existing.IsDeleted = flags.HasFlag(MessageFlags.Deleted);
        }
    }

    // ── Gelöschte Nachrichten lokal entfernen ───────────────────────────────
    private async Task RemoveDeletedMessagesAsync(
        Folder localFolder, IMailFolder imapFolder, CancellationToken ct)
    {
        var serverUids = (await imapFolder.SearchAsync(SearchQuery.All, ct))
            .Select(u => (uint)u.Id)
            .ToHashSet();

        var toDelete = await _db.Messages
            .Where(m => m.FolderId == localFolder.Id && !serverUids.Contains(m.Uid))
            .ToListAsync(ct);

        if (toDelete.Count > 0)
        {
            _logger.LogInformation("{Count} gelöschte Nachrichten lokal entfernt.", toDelete.Count);
            _db.Messages.RemoveRange(toDelete);
        }
    }

    // ── Ordnerliste vom Server spiegeln ─────────────────────────────────────
    private async Task SyncFolderListAsync(
        Account account, ImapClient client, CancellationToken ct)
    {
        var serverFolders = await client.GetFoldersAsync(
            client.PersonalNamespaces[0], false, ct);

        foreach (var sf in serverFolders)
        {
            var exists = await _db.Folders
                .AnyAsync(f => f.AccountId == account.Id && f.FullPath == sf.FullName, ct);

            if (!exists)
            {
                _db.Folders.Add(new Folder
                {
                    Id        = Guid.NewGuid(),
                    AccountId = account.Id,
                    Name      = sf.Name,
                    FullPath  = sf.FullName,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ── UidValidity-Änderung: Cache leeren ──────────────────────────────────
    private async Task InvalidateFolderCacheAsync(Folder folder, CancellationToken ct)
    {
        var messages = await _db.Messages
            .Where(m => m.FolderId == folder.Id)
            .ToListAsync(ct);

        _db.Messages.RemoveRange(messages);
        folder.HighestModSeq = 0;
        await _db.SaveChangesAsync(ct);
    }

    // ── IMAP-Verbindung herstellen ───────────────────────────────────────────
    private static async Task<ImapClient> ConnectAsync(Account account, CancellationToken ct)
    {
        var client = new ImapClient();
        client.CheckCertificateRevocation = false;
        client.ServerCertificateValidationCallback =
            (sender, certificate, chain, errors) => true;

        await client.ConnectAsync(
            account.ImapHost,
            account.ImapPort,
            SecureSocketOptions.SslOnConnect,
            ct);
        await client.AuthenticateAsync(account.Username, account.Password, ct);
        return client;
    }

    public async Task FetchBodyAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await _db.Messages
            .Include(m => m.Folder)
            .ThenInclude(f => f.Account)
            .FirstOrDefaultAsync(m => m.Id == messageId, ct)
            ?? throw new InvalidOperationException($"Message {messageId} nicht gefunden.");

        if (!string.IsNullOrEmpty(message.BodyText) || !string.IsNullOrEmpty(message.BodyHtml))
            return; // Body bereits vorhanden

        var account = message.Folder.Account;
        using var client = await ConnectAsync(account, ct);

        var imapFolder = await client.GetFolderAsync(message.Folder.FullPath, ct);
        await imapFolder.OpenAsync(FolderAccess.ReadOnly, ct);

        var uid = new UniqueId(message.Uid);
        var mime = await imapFolder.GetMessageAsync(uid, ct);

        message.BodyText = mime.TextBody;
        message.BodyHtml = mime.HtmlBody;

        await imapFolder.CloseAsync(false, ct);
        await client.DisconnectAsync(true, ct);
        await _db.SaveChangesAsync(ct);
    }

    // ── IDLE – wird vom ImapIdleService genutzt ──────────────────────────────
    public Task StartIdleAsync(Guid folderId, CancellationToken ct = default)
    {
        // Der ImapIdleService übernimmt die eigentliche IDLE-Logik.
        // Diese Methode erfüllt das Interface – direkter Aufruf via DI empfohlen.
        throw new NotSupportedException(
            "Bitte ImapIdleService.RunAsync() direkt verwenden.");
    }
}