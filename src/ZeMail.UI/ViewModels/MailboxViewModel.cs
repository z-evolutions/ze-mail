using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    // ── Konstruktor ───────────────────────────────────────────────────────────
    public MailboxViewModel()
    {
        LoadDemoData();
    }

    partial void OnSelectedFolderChanged(FolderViewModel? value)
    {
        SelectedMessage = null;
    }

    partial void OnSearchTextChanged(string value)
    {
        // Später: gefilterte Nachrichten laden
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
        var inbox = new FolderViewModel { Name = "Inbox",  FullPath = "INBOX"  };
        var sent  = new FolderViewModel { Name = "Sent",   FullPath = "Sent"   };
        var draft = new FolderViewModel { Name = "Drafts", FullPath = "Drafts" };
        var trash = new FolderViewModel { Name = "Trash",  FullPath = "Trash"  };

        Folders.Add(inbox);
        Folders.Add(sent);
        Folders.Add(draft);
        Folders.Add(trash);

        SelectedFolder = inbox;

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

        Messages.Add(new MessageViewModel
        {
            Id             = Guid.NewGuid(),
            Subject        = "Test mit Anhang",
            FromName       = "Sascha Merken",
            FromAddress    = "info@z-evolutions.de",
            ReceivedAtUtc  = DateTime.UtcNow.AddDays(-1),
            IsRead         = false,
            IsStarred      = false,
            HasAttachments = true,
            BodyText       = "Diese Mail hat einen Anhang."
        });

        SelectedMessage = Messages[0];
    }
}