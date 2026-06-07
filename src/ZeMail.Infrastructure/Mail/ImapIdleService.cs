using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZeMail.Core.Interfaces;
using ZeMail.Infrastructure.Persistence;

namespace ZeMail.Infrastructure.Mail;

/// <summary>
/// Hält eine IMAP-IDLE-Verbindung für einen Ordner offen.
/// Bei neuer Mail / Flag-Änderung wird automatisch ein Delta-Sync ausgelöst.
/// </summary>
public sealed class ImapIdleService : IAsyncDisposable
{
    private readonly ZeMailDbContext _db;
    private readonly IImapSyncService _syncService;
    private readonly ILogger<ImapIdleService> _logger;

    private ImapClient? _client;
    private IMailFolder? _folder;
    private CancellationTokenSource? _idleDone;
    private bool _messagesArrived;

    public ImapIdleService(
        ZeMailDbContext db,
        IImapSyncService syncService,
        ILogger<ImapIdleService> logger)
    {
        _db = db;
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// Startet IDLE für den angegebenen Ordner.
    /// Läuft bis ct abgebrochen wird (z.B. App-Shutdown).
    /// </summary>
    public async Task RunAsync(Guid folderId, CancellationToken ct)
    {
        var localFolder = await _db.Folders
            .Include(f => f.Account)
            .FirstOrDefaultAsync(f => f.Id == folderId, ct)
            ?? throw new InvalidOperationException($"Folder {folderId} nicht gefunden.");

        var account = localFolder.Account;

        _client = new ImapClient();

        await _client.ConnectAsync(
            account.ImapHost,
            account.ImapPort,
            SecureSocketOptions.SslOnConnect,
            ct);

        await _client.AuthenticateAsync(account.Username, account.Password, ct);

        _folder = await _client.GetFolderAsync(localFolder.FullPath, ct);
        await _folder.OpenAsync(FolderAccess.ReadOnly, ct);

        // Event-Handler registrieren
        _folder.CountChanged  += OnCountChanged;
        _folder.MessageExpunged += OnMessageExpunged;

        _logger.LogInformation("IMAP IDLE gestartet: {Folder}", localFolder.FullPath);

        // IDLE-Loop: alle 9 Minuten neu starten (Server-Timeout ist meist 30 Min,
        // wir erneuern sicherheitshalber nach 9 Min um die Verbindung zu halten)
        while (!ct.IsCancellationRequested)
        {
            _idleDone = new CancellationTokenSource();
            _messagesArrived = false;

            try
            {
                if (_client.Capabilities.HasFlag(ImapCapabilities.Idle))
                {
                    // Echter IDLE-Befehl
                    await _client.IdleAsync(
                        _idleDone.Token,
                        ct);
                }
                else
                {
                    // Fallback: NOOP alle 30 Sekunden
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                    await _client.NoOpAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                break; // App-Shutdown
            }

            // Nach IDLE: wenn neue Nachrichten angekommen → Delta-Sync
            if (_messagesArrived)
            {
                _logger.LogInformation(
                    "Neue Nachrichten erkannt – starte Delta-Sync für {Folder}",
                    localFolder.FullPath);

                try { await _syncService.SyncFolderAsync(folderId, ct); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Delta-Sync nach IDLE fehlgeschlagen.");
                }
            }

            // 9-Minuten-Timer: IDLE erneuern
            if (!ct.IsCancellationRequested)
            {
                _ = Task.Delay(TimeSpan.FromMinutes(9), ct)
                    .ContinueWith(_ => _idleDone?.Cancel(), CancellationToken.None);
            }
        }

        _logger.LogInformation("IMAP IDLE beendet: {Folder}", localFolder.FullPath);
    }

    // ── Event: neue Mail angekommen ─────────────────────────────────────────
    private void OnCountChanged(object? sender, EventArgs e)
    {
        _logger.LogDebug("IDLE: CountChanged – neue Mail erkannt.");
        _messagesArrived = true;
        _idleDone?.Cancel(); // IDLE unterbrechen → Sync starten
    }

    // ── Event: Mail vom Server gelöscht ─────────────────────────────────────
    private void OnMessageExpunged(object? sender, MessageEventArgs e)
    {
        _logger.LogDebug("IDLE: MessageExpunged – Mail {Index} entfernt.", e.Index);
        _messagesArrived = true;
        _idleDone?.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        _idleDone?.Cancel();

        if (_folder is not null)
        {
            _folder.CountChanged    -= OnCountChanged;
            _folder.MessageExpunged -= OnMessageExpunged;

            try { await _folder.CloseAsync(false); } catch { /* ignore */ }
        }

        if (_client is not null)
        {
            try { await _client.DisconnectAsync(true); } catch { /* ignore */ }
            _client.Dispose();
        }

        _idleDone?.Dispose();
    }
}