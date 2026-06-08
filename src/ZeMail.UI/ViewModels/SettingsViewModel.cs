using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.Core.Entities;

namespace ZeMail.UI.ViewModels;

public partial class AccountItemViewModel : ObservableObject
{
    public Guid   Id           { get; init; }
    public string Name         { get; init; } = string.Empty;
    public string EmailAddress { get; init; } = string.Empty;
    public string ImapHost     { get; init; } = string.Empty;
    public int    ImapPort     { get; init; }
    public string SmtpHost     { get; init; } = string.Empty;
    public int    SmtpPort     { get; init; }
    public string Username     { get; init; } = string.Empty;
    public string Password     { get; init; } = string.Empty;
    public string Protocol     { get; init; } = "IMAP";
    public bool   UnifiedInboxEnabled { get; init; }
}

public partial class SettingsViewModel : ViewModelBase
{
    public ObservableCollection<AccountItemViewModel> Accounts { get; } = [];

    [ObservableProperty]
    private AccountItemViewModel? _selectedAccount;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Editierfelder
    [ObservableProperty] private string _editName         = string.Empty;
    [ObservableProperty] private string _editEmail        = string.Empty;
    [ObservableProperty] private string _editImapHost     = string.Empty;
    [ObservableProperty] private int    _editImapPort     = 993;
    [ObservableProperty] private string _editSmtpHost     = string.Empty;
    [ObservableProperty] private int    _editSmtpPort     = 465;
    [ObservableProperty] private string _editUsername     = string.Empty;
    [ObservableProperty] private string _editPassword     = string.Empty;
    [ObservableProperty] private bool   _editUnifiedInbox = false;
    [ObservableProperty] private bool   _hasSelection     = false;

    public event Action? OnAddAccount;
    public event Action? OnClose;

    public SettingsViewModel()
    {
        LoadAccounts();
    }

    public void LoadAccounts()
    {
        if (App.Services is null) return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        Accounts.Clear();
        foreach (var a in db.Accounts.ToList())
        {
            Accounts.Add(new AccountItemViewModel
            {
                Id                  = a.Id,
                Name                = a.Name,
                EmailAddress        = a.EmailAddress,
                ImapHost            = a.ImapHost,
                ImapPort            = a.ImapPort,
                SmtpHost            = a.SmtpHost,
                SmtpPort            = a.SmtpPort,
                Username            = a.Username,
                Password            = a.Password,
                Protocol            = a.Protocol,
                UnifiedInboxEnabled = a.UnifiedInboxEnabled
            });
        }

        SelectedAccount = Accounts.FirstOrDefault();
    }

    partial void OnSelectedAccountChanged(AccountItemViewModel? value)
    {
        HasSelection = value is not null;
        if (value is null) return;

        EditName         = value.Name;
        EditEmail        = value.EmailAddress;
        EditImapHost     = value.ImapHost;
        EditImapPort     = value.ImapPort;
        EditSmtpHost     = value.SmtpHost;
        EditSmtpPort     = value.SmtpPort;
        EditUsername     = value.Username;
        EditPassword     = value.Password;
        EditUnifiedInbox = value.UnifiedInboxEnabled;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedAccount is null || App.Services is null) return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        var account = db.Accounts.FirstOrDefault(a => a.Id == SelectedAccount.Id);
        if (account is null) return;

        account.Name                = EditName;
        account.EmailAddress        = EditEmail;
        account.ImapHost            = EditImapHost;
        account.ImapPort            = EditImapPort;
        account.SmtpHost            = EditSmtpHost;
        account.SmtpPort            = EditSmtpPort;
        account.Username            = EditUsername;
        account.Password            = EditPassword;
        account.UnifiedInboxEnabled = EditUnifiedInbox;

        await db.SaveChangesAsync();
        StatusMessage = "✓ Gespeichert";
        LoadAccounts();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedAccount is null || App.Services is null) return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        var account = db.Accounts.FirstOrDefault(a => a.Id == SelectedAccount.Id);
        if (account is null) return;

        db.Remove(account);
        await db.SaveChangesAsync();
        StatusMessage = "Account gelöscht.";
        LoadAccounts();
    }

    [RelayCommand]
    private void AddAccount() => OnAddAccount?.Invoke();

    [RelayCommand]
    private void Close() => OnClose?.Invoke();
}