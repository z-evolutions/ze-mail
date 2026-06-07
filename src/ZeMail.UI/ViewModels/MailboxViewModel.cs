using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.UI.Models;

namespace ZeMail.UI.ViewModels;

public partial class MailboxViewModel : ViewModelBase
{
    // ── Ordnerbaum ───────────────────────────────────────────────────────────
    public ObservableCollection<FolderViewModel> Folders { get; } = [];

    [ObservableProperty]
    private FolderViewModel? _selectedFolder;

    // ── Nachrichtenliste ─────────────────────────────────────────────────────
    public ObservableCollection<MessageViewModel> Messages { get; } = [];

    [ObservableProperty]
    private MessageViewModel? _selectedMessage;

    // ── Suche ────────────────────────────────────────────────────────────────
    [ObservableProperty]
    private string _searchText = string.Empty;

    // ── Status ───────────────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _isSyncing = false;

    [ObservableProperty]
    private string _statusText = string.Empty;

    // ── Konstruktor ───────────────────────────────────────────────────────────
    public MailboxViewModel()
    {
        if (App.Services is not null)
            _ = LoadFromDbAsync();
        else
            LoadDemoData();
    }

    // ── DB laden ─────────────────────────────────────────────────────────────
    private async Task LoadFromDbAsync()
    {
        using var scope = App.Services!.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        var accounts = db.Accounts.ToList();
        if (!accounts.Any())
        {
            LoadDemoData();
            return;
        }

        Folders.Clear();
        foreach (var account in accounts)
        {
            var folders = db.Folders
                .Where(f => f.AccountId == account.Id)
                .OrderBy(f => f.FullPath)
                .ToList();

            foreach (var folder in folders)
            {
                Folders.Add(new FolderViewModel
                {
                    Id       = folder.Id,
                    Name     = folder.Name,
                    FullPath = folder.FullPath
                });
            }
        }

        if (Folders.Any())
            SelectedFolder = Folders.First(f =>
                f.Name.ToLower() is "inbox" or "posteingang") ?? Folders[0];

        await SyncAsync();
    }

    private void LoadMessagesForFolder(FolderViewModel folder)
    {
        if (App.Services is null) return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        var messages = db.Messages
            .Where(m => m.FolderId == folder.Id && !m.IsDeleted)
            .OrderByDescending(m => m.ReceivedAtUtc)
            .Take(100)
            .ToList();

        Messages.Clear();
        foreach (var m in messages)
        {
            Messages.Add(new MessageViewModel
            {
                Id            = m.Id,
                Subject       = m.Subject,
                FromName      = m.FromName,
                FromAddress   = m.FromAddress,
                ReceivedAtUtc = m.ReceivedAtUtc,
                IsRead        = m.IsRead,
                IsStarred     = m.IsStarred,
                HasAttachments = m.Attachments.Any(),
                BodyText      = m.BodyText,
                BodyHtml      = m.BodyHtml
            });
        }

        SelectedMessage = Messages.FirstOrDefault();
    }

    // ── Sync ─────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SyncAsync()
    {
        if (App.Services is null || IsSyncing) return;

        IsSyncing  = true;
        StatusText = "Synchronisiere…";

        try
        {
            using var scope = App.Services.CreateScope();
            var db   = scope.ServiceProvider
                            .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();
            var sync = scope.ServiceProvider
                            .GetRequiredService<ZeMail.Core.Interfaces.IImapSyncService>();

            var accounts = db.Accounts.ToList();
            foreach (var account in accounts)
                await sync.SyncAccountAsync(account.Id);

            StatusText = $"Sync abgeschlossen {DateTime.Now:HH:mm}";

            // Ordner neu laden
            await LoadFromDbAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Sync-Fehler: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    partial void OnSelectedFolderChanged(FolderViewModel? value)
    {
        SelectedMessage = null;
        if (value is not null && App.Services is not null)
            LoadMessagesForFolder(value);
    }

    partial void OnSearchTextChanged(string value)
    {
        // Später: Suche implementieren
    }

    [RelayCommand]
    private void ToggleStar(MessageViewModel? msg)
    {
        if (msg is null) return;
        msg.IsStarred = !msg.IsStarred;
    }

    [RelayCommand]
    private void MarkRead(MessageViewModel? msg)
    {
        if (msg is null) return;
        msg.IsRead = true;
    }

    // ── Demodaten ─────────────────────────────────────────────────────────────
    private void LoadDemoData()
    {
        Folders.Add(new FolderViewModel { Name = "Inbox",  FullPath = "INBOX"  });
        Folders.Add(new FolderViewModel { Name = "Sent",   FullPath = "Sent"   });
        Folders.Add(new FolderViewModel { Name = "Drafts", FullPath = "Drafts" });
        Folders.Add(new FolderViewModel { Name = "Trash",  FullPath = "Trash"  });

        SelectedFolder = Folders[0];

        Messages.Add(new MessageViewModel
        {
            Id            = Guid.NewGuid(),
            Subject       = "Willkommen bei ZE-Mail!",
            FromName      = "ZE-Mail Team",
            FromAddress   = "noreply@z-evolutions.de",
            ReceivedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            IsRead        = false,
            IsStarred     = true,
            BodyText      = "Hallo,\n\nWillkommen bei ZE-Mail — deinem selbst gehosteten E-Mail-Client.\n\nViel Spaß!"
        });

        Messages.Add(new MessageViewModel
        {
            Id            = Guid.NewGuid(),
            Subject       = "Phase 4 abgeschlossen",
            FromName      = "Cheyenne",
            FromAddress   = "cheyenne@ze-mail.dev",
            ReceivedAtUtc = DateTime.UtcNow.AddHours(-2),
            IsRead        = true,
            IsStarred     = false,
            BodyText      = "Kalender & CalDAV sind fertig. Weiter mit Phase 5!"
        });

        SelectedMessage = Messages[0];
    }
}