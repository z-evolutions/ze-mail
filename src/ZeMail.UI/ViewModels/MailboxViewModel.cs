using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.UI.Models;
using ZeMail.UI.Services;
using ZeMail.UI.Views;

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

    // ── Ordnernamen lokalisieren ──────────────────────────────────────────────
    private static string LocalizeFolderName(string name) => name.ToLower() switch
    {
        "inbox"         => "Posteingang",
        "sent"          => "Gesendet",
        "sent items"    => "Gesendet",
        "sent messages" => "Gesendet",
        "drafts"        => "Entwürfe",
        "draft"         => "Entwürfe",
        "trash"         => "Papierkorb",
        "deleted"       => "Papierkorb",
        "deleted items" => "Papierkorb",
        "junk"          => "Spam",
        "spam"          => "Spam",
        "junk email"    => "Spam",
        "junk e-mail"   => "Spam",
        "archive"       => "Archiv",
        "archives"      => "Archiv",
        "outbox"        => "Postausgang",
        _               => name
    };

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
            Folders.Add(new FolderViewModel
            {
                IsAccountHeader = true,
                AccountName     = account.Name,
                Name            = account.Name,
                FullPath        = string.Empty
            });

            var folders = db.Folders
                .Where(f => f.AccountId == account.Id)
                .OrderBy(f => f.FullPath)
                .ToList();

            foreach (var folder in folders)
            {
                Folders.Add(new FolderViewModel
                {
                    Id          = folder.Id,
                    Name        = LocalizeFolderName(folder.Name),
                    FullPath    = folder.FullPath,
                    AccountName = account.Name
                });
            }
        }

        if (Folders.Any())
        {
            SelectedFolder = Folders.FirstOrDefault(f =>
                f.Name.ToLower() is "inbox" or "posteingang") ?? Folders[0];
        }

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
            var vm = new MessageViewModel
            {
                Id             = m.Id,
                Subject        = m.Subject,
                FromName       = m.FromName,
                FromAddress    = m.FromAddress,
                ReceivedAtUtc  = m.ReceivedAtUtc,
                IsRead         = m.IsRead,
                IsStarred      = m.IsStarred,
                HasAttachments = m.Attachments.Any(),
                BodyText       = m.BodyText,
                BodyHtml       = m.BodyHtml
            };
            Messages.Add(vm);
            _ = LoadAvatarAsync(vm);
        }

        SelectedMessage = Messages.FirstOrDefault();
    }

    // ── Avatar laden ─────────────────────────────────────────────────────────
    private static async Task LoadAvatarAsync(MessageViewModel msg)
    {
        var bitmap = await AvatarService.ResolveAsync(msg.FromAddress, msg.FromName);
        msg.AvatarBitmap = bitmap;
    }

    // ── Body nachladen ────────────────────────────────────────────────────────
    partial void OnSelectedMessageChanged(MessageViewModel? value)
    {
        if (value is not null &&
            string.IsNullOrEmpty(value.BodyText) &&
            string.IsNullOrEmpty(value.BodyHtml))
        {
            _ = FetchBodyAsync(value);
        }
    }

    private async Task FetchBodyAsync(MessageViewModel msg)
    {
        if (App.Services is null) return;

        try
        {
            using var scope = App.Services.CreateScope();
            var sync = scope.ServiceProvider
                            .GetRequiredService<ZeMail.Core.Interfaces.IImapSyncService>();
            await sync.FetchBodyAsync(msg.Id);

            using var scope2 = App.Services.CreateScope();
            var db = scope2.ServiceProvider
                           .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();
            var dbMsg = db.Messages.FirstOrDefault(m => m.Id == msg.Id);
            if (dbMsg is not null)
            {
                msg.BodyText = dbMsg.BodyText;
                msg.BodyHtml = dbMsg.BodyHtml;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Body-Fehler: {ex.Message}";
        }
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
        if (value is not null && !value.IsAccountHeader && App.Services is not null)
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

    [RelayCommand]
    private void Reply()     => OpenCompose(ComposeMode.Reply);

    [RelayCommand]
    private void ReplyAll()  => OpenCompose(ComposeMode.ReplyAll);

    [RelayCommand]
    private void Forward()   => OpenCompose(ComposeMode.Forward);

    [RelayCommand]
    private void NewMail()   => OpenCompose(ComposeMode.New);

    private void OpenCompose(ComposeMode mode)
    {
        if (App.Services is null) return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                    .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();
        var account = db.Accounts.FirstOrDefault();
        if (account is null) return;

        var vm = new ComposeViewModel { AccountId = account.Id, Mode = mode };

        if (mode == ComposeMode.Reply && SelectedMessage is not null)
        {
            vm.To      = SelectedMessage.FromAddress;
            vm.Subject = "Re: " + SelectedMessage.Subject;
            vm.Body    = $"\n\n--- Originalnachricht ---\nVon: {SelectedMessage.SenderDisplay}\n\n{SelectedMessage.BodyText}";
        }
        else if (mode == ComposeMode.ReplyAll && SelectedMessage is not null)
        {
            vm.To      = SelectedMessage.FromAddress;
            vm.Subject = "Re: " + SelectedMessage.Subject;
            vm.Body    = $"\n\n--- Originalnachricht ---\nVon: {SelectedMessage.SenderDisplay}\n\n{SelectedMessage.BodyText}";
        }
        else if (mode == ComposeMode.Forward && SelectedMessage is not null)
        {
            vm.Subject = "Fwd: " + SelectedMessage.Subject;
            vm.Body    = $"\n\n--- Weitergeleitet ---\nVon: {SelectedMessage.SenderDisplay}\nBetreff: {SelectedMessage.Subject}\n\n{SelectedMessage.BodyText}";
        }

        var win = new ComposeWindow { DataContext = vm };
        vm.OnSent      += () => win.Close();
        vm.OnCancelled += () => win.Close();
        win.Show();
    }

    // ── Demodaten ─────────────────────────────────────────────────────────────
    private void LoadDemoData()
    {
        Folders.Add(new FolderViewModel { Name = "Posteingang", FullPath = "INBOX"  });
        Folders.Add(new FolderViewModel { Name = "Gesendet",    FullPath = "Sent"   });
        Folders.Add(new FolderViewModel { Name = "Entwürfe",    FullPath = "Drafts" });
        Folders.Add(new FolderViewModel { Name = "Papierkorb",  FullPath = "Trash"  });

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