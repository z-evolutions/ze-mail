using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;
using ZeMail.UI.Services;

namespace ZeMail.UI.ViewModels;

public partial class AccountItemViewModel : ObservableObject
{
    public Guid   Id                  { get; init; }
    public string Name                { get; init; } = string.Empty;
    public string EmailAddress        { get; init; } = string.Empty;
    public string ImapHost            { get; init; } = string.Empty;
    public int    ImapPort            { get; init; }
    public string SmtpHost            { get; init; } = string.Empty;
    public int    SmtpPort            { get; init; }
    public string Username            { get; init; } = string.Empty;
    public string Password            { get; init; } = string.Empty;
    public string Protocol            { get; init; } = "IMAP";
    public bool   UnifiedInboxEnabled { get; init; }
}

public partial class CalendarItemViewModel : ObservableObject
{
    public Guid   Id        { get; init; }
    public string Name      { get; init; } = string.Empty;
    public string Color     { get; init; } = "#3a3aff";
    public bool   IsDefault { get; init; }
    public bool   IsVisible { get; init; }
}

public partial class SignatureItemViewModel : ObservableObject
{
    public Guid   Id        { get; init; }
    public Guid   AccountId { get; init; }
    public string Name      { get; init; } = string.Empty;
    public bool   IsDefault { get; init; }
}

// ── Signatur-Editor ViewModel ────────────────────────────────────────────────
public partial class SignatureEditorViewModel : ObservableObject
{
    public Guid?  Id        { get; set; }
    [ObservableProperty] private Guid   _accountId;
    [ObservableProperty] private string _name      = string.Empty;
    [ObservableProperty] private string _bodyHtml  = string.Empty;
    [ObservableProperty] private string _bodyText  = string.Empty;
    [ObservableProperty] private bool   _isDefault = false;
    [ObservableProperty] private bool   _useForNew        = true;
    [ObservableProperty] private bool   _useForReply      = false;
    [ObservableProperty] private bool   _useForForward    = false;
}

public partial class SettingsViewModel : ViewModelBase
{
    // ── Navigation ───────────────────────────────────────────────────────────
    [ObservableProperty] private string _activeSection = "Konten";

    public bool IsKonten             => ActiveSection == "Konten";
    public bool IsAllgemein          => ActiveSection == "Allgemein";
    public bool IsVerfassen          => ActiveSection == "Verfassen";
    public bool IsSignaturen         => ActiveSection == "Signaturen";
    public bool IsKalender           => ActiveSection == "Kalender";
    public bool IsMeineKalender      => ActiveSection == "MeineKalender";
    public bool IsBenachrichtigungen => ActiveSection == "Benachrichtigungen";

    partial void OnActiveSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsKonten));
        OnPropertyChanged(nameof(IsAllgemein));
        OnPropertyChanged(nameof(IsVerfassen));
        OnPropertyChanged(nameof(IsSignaturen));
        OnPropertyChanged(nameof(IsKalender));
        OnPropertyChanged(nameof(IsMeineKalender));
        OnPropertyChanged(nameof(IsBenachrichtigungen));
        if (value == "Verfassen")  LoadSignatures();
        if (value == "Signaturen") LoadSignaturesForEditor();
    }

    // ── Konten ───────────────────────────────────────────────────────────────
    public ObservableCollection<AccountItemViewModel> Accounts { get; } = [];

    [ObservableProperty] private AccountItemViewModel? _selectedAccount;
    [ObservableProperty] private string _statusMessage    = string.Empty;
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

    // ── Allgemein ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isDarkTheme = true;

    // ── Verfassen ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _composeHtml     = true;
    [ObservableProperty] private string _quoteStyle      = "Above";
    [ObservableProperty] private string _verfassenStatus = string.Empty;

    public ObservableCollection<SignatureItemViewModel> Signatures { get; } = [];
    [ObservableProperty] private SignatureItemViewModel? _selectedSignature;

    // ── Signaturen Editor ────────────────────────────────────────────────────
    public ObservableCollection<SignatureItemViewModel> AllSignatures { get; } = [];

    [ObservableProperty] private SignatureItemViewModel? _selectedSignatureForEdit;
    [ObservableProperty] private SignatureEditorViewModel _editor = new();
    [ObservableProperty] private bool   _isEditingSignature = false;
    [ObservableProperty] private string _signaturenStatus   = string.Empty;

    // ── Kalender ─────────────────────────────────────────────────────────────
    [ObservableProperty] private int  _defaultEventDuration = 60;
    [ObservableProperty] private int  _workDayStartHour     = 8;
    [ObservableProperty] private int  _workDayEndHour       = 18;
    [ObservableProperty] private int  _firstDayOfWeek       = 1;
    [ObservableProperty] private bool _showWeekends         = true;

    // ── Meine Kalender ───────────────────────────────────────────────────────
    public ObservableCollection<CalendarItemViewModel> Calendars { get; } = [];

    [ObservableProperty] private CalendarItemViewModel? _selectedCalendar;
    [ObservableProperty] private bool   _isAddingCalendar = false;
    [ObservableProperty] private string _newCalendarName  = string.Empty;
    [ObservableProperty] private string _newCalendarColor = "#3a3aff";
    [ObservableProperty] private string _calendarStatus   = string.Empty;

    // ── Benachrichtigungen ───────────────────────────────────────────────────
    [ObservableProperty] private bool _toastEnabled = true;

    // ── Events ───────────────────────────────────────────────────────────────
    public event Action? OnAddAccount;
    public event Action? OnClose;

    public SettingsViewModel()
    {
        LoadAccounts();
        LoadCalendars();
        LoadSettings();
        LoadSignatures();
    }

    // ── Settings ─────────────────────────────────────────────────────────────
    private void LoadSettings()
    {
        var s = App.Settings;
        IsDarkTheme          = s.Theme == "Dark";
        ComposeHtml          = s.ComposeFormat == "HTML";
        QuoteStyle           = s.QuoteStyle;
        DefaultEventDuration = s.DefaultEventDurationMinutes;
        WorkDayStartHour     = s.WorkDayStartHour;
        WorkDayEndHour       = s.WorkDayEndHour;
        FirstDayOfWeek       = s.FirstDayOfWeek;
        ShowWeekends         = s.ShowWeekends;
        ToastEnabled         = s.ToastNotificationsEnabled;
    }

    private void SaveSettings()
    {
        var s = App.Settings;
        s.Theme                       = IsDarkTheme ? "Dark" : "Light";
        s.ComposeFormat               = ComposeHtml ? "HTML" : "Text";
        s.QuoteStyle                  = QuoteStyle;
        s.DefaultEventDurationMinutes = DefaultEventDuration;
        s.WorkDayStartHour            = WorkDayStartHour;
        s.WorkDayEndHour              = WorkDayEndHour;
        s.FirstDayOfWeek              = FirstDayOfWeek;
        s.ShowWeekends                = ShowWeekends;
        s.ToastNotificationsEnabled   = ToastEnabled;
        s.Save();
    }

    // ── Konten ───────────────────────────────────────────────────────────────
    public void LoadAccounts()
    {
        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
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
    private async Task SaveAccountAsync()
    {
        if (SelectedAccount is null || App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
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
    private async Task DeleteAccountAsync()
    {
        if (SelectedAccount is null || App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
        var account = db.Accounts.FirstOrDefault(a => a.Id == SelectedAccount.Id);
        if (account is null) return;
        db.Remove(account);
        await db.SaveChangesAsync();
        StatusMessage = "Konto gelöscht.";
        LoadAccounts();
    }

    [RelayCommand] private void AddAccount() => OnAddAccount?.Invoke();

    // ── Allgemein ────────────────────────────────────────────────────────────
    [RelayCommand]
    private void SaveAllgemein()
    {
        SaveSettings();
        if (Avalonia.Application.Current is not null)
        {
            Avalonia.Application.Current.RequestedThemeVariant = IsDarkTheme
                ? Avalonia.Styling.ThemeVariant.Dark
                : Avalonia.Styling.ThemeVariant.Light;
        }
        StatusMessage = "✓ Gespeichert";
    }

    // ── Verfassen ────────────────────────────────────────────────────────────
    private void LoadSignatures()
    {
        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
        Signatures.Clear();
        Signatures.Add(new SignatureItemViewModel { Id = Guid.Empty, Name = "(Keine Signatur)" });
        foreach (var sig in db.Signatures.ToList())
        {
            Signatures.Add(new SignatureItemViewModel
            {
                Id        = sig.Id,
                AccountId = sig.AccountId,
                Name      = sig.Name,
                IsDefault = sig.IsDefault
            });
        }
        SelectedSignature = Signatures.FirstOrDefault(s => s.IsDefault)
                         ?? Signatures.FirstOrDefault();
    }

    [RelayCommand]
    private void SaveVerfassen()
    {
        SaveSettings();
        _ = SaveVerfassenInternalAsync();
    }

    private async Task SaveVerfassenInternalAsync()
    {
        if (App.Services is not null && SelectedSignature is not null)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
            foreach (var sig in db.Signatures.ToList())
                sig.IsDefault = sig.Id == SelectedSignature.Id;
            await db.SaveChangesAsync();
        }
        VerfassenStatus = "✓ Gespeichert";
    }

    // ── Signaturen Editor ────────────────────────────────────────────────────
    private void LoadSignaturesForEditor()
    {
        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
        AllSignatures.Clear();
        foreach (var sig in db.Signatures.ToList())
        {
            AllSignatures.Add(new SignatureItemViewModel
            {
                Id        = sig.Id,
                AccountId = sig.AccountId,
                Name      = sig.Name,
                IsDefault = sig.IsDefault
            });
        }
        IsEditingSignature = false;
        SelectedSignatureForEdit = AllSignatures.FirstOrDefault();
    }

    partial void OnSelectedSignatureForEditChanged(SignatureItemViewModel? value)
    {
        if (value is null || value.Id == Guid.Empty) return;
        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
        var sig = db.Signatures.FirstOrDefault(s => s.Id == value.Id);
        if (sig is null) return;
        Editor = new SignatureEditorViewModel
        {
            Id         = sig.Id,
            AccountId  = sig.AccountId,
            Name       = sig.Name,
            BodyHtml   = sig.BodyHtml,
            BodyText   = sig.BodyText,
            IsDefault  = sig.IsDefault,
        };
        IsEditingSignature = true;
    }

    [RelayCommand]
    private void NewSignature()
    {
        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
        var account = db.Accounts.FirstOrDefault();
        Editor = new SignatureEditorViewModel
        {
            Id        = null,
            AccountId = account?.Id ?? Guid.Empty,
            Name      = string.Empty,
            BodyHtml  = string.Empty,
            BodyText  = string.Empty,
            IsDefault = false,
        };
        IsEditingSignature    = true;
        SelectedSignatureForEdit = null;
    }

    [RelayCommand]
    private async Task SaveSignatureAsync()
    {
        if (App.Services is null || string.IsNullOrWhiteSpace(Editor.Name)) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();

        if (Editor.Id is null)
        {
            // Neu anlegen
            var entity = new Signature
            {
                AccountId    = Editor.AccountId,
                Name         = Editor.Name.Trim(),
                BodyHtml     = Editor.BodyHtml,
                BodyText     = Editor.BodyText,
                IsDefault    = Editor.IsDefault,
                CreatedAtUtc = DateTime.UtcNow
            };
            if (entity.IsDefault)
            {
                foreach (var s in db.Signatures.ToList())
                    s.IsDefault = false;
            }
            db.Add(entity);
        }
        else
        {
            // Bearbeiten
            var existing = db.Signatures.FirstOrDefault(s => s.Id == Editor.Id);
            if (existing is null) return;
            existing.Name      = Editor.Name.Trim();
            existing.BodyHtml  = Editor.BodyHtml;
            existing.BodyText  = Editor.BodyText;
            existing.AccountId = Editor.AccountId;
            if (Editor.IsDefault)
            {
                foreach (var s in db.Signatures.ToList())
                    s.IsDefault = false;
                existing.IsDefault = true;
            }
            else
            {
                existing.IsDefault = false;
            }
        }

        await db.SaveChangesAsync();
        SignaturenStatus = "✓ Gespeichert";
        LoadSignaturesForEditor();
        LoadSignatures();
    }

    [RelayCommand]
    private async Task DeleteSignatureAsync(SignatureItemViewModel sig)
    {
        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
        var entity = db.Signatures.FirstOrDefault(s => s.Id == sig.Id);
        if (entity is null) return;
        db.Remove(entity);
        await db.SaveChangesAsync();
        SignaturenStatus   = "Signatur gelöscht.";
        IsEditingSignature = false;
        LoadSignaturesForEditor();
        LoadSignatures();
    }

    [RelayCommand]
    private void CancelSignatureEdit()
    {
        IsEditingSignature = false;
        Editor             = new SignatureEditorViewModel();
    }

    // ── Kalender ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private void SaveKalender()
    {
        SaveSettings();
        StatusMessage = "✓ Gespeichert";
    }

    // ── Meine Kalender ───────────────────────────────────────────────────────
    private void LoadCalendars()
    {
        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
        Calendars.Clear();
        foreach (var c in db.Calendars.ToList())
        {
            Calendars.Add(new CalendarItemViewModel
            {
                Id        = c.Id,
                Name      = c.Name,
                Color     = c.Color,
                IsDefault = c.IsDefault,
                IsVisible = c.IsVisible
            });
        }
    }

    [RelayCommand]
    private void BeginAddCalendar()
    {
        IsAddingCalendar = true;
        NewCalendarName  = string.Empty;
        NewCalendarColor = "#3a3aff";
    }

    [RelayCommand]
    private async Task ConfirmAddCalendarAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCalendarName) || App.Services is null)
        { IsAddingCalendar = false; return; }
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
        var account = db.Accounts.FirstOrDefault();
        if (account is null) { IsAddingCalendar = false; return; }
        var entity = new Calendar
        {
            AccountId    = account.Id,
            Name         = NewCalendarName.Trim(),
            Color        = NewCalendarColor,
            IsDefault    = !Calendars.Any(),
            IsVisible    = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Add(entity);
        await db.SaveChangesAsync();
        IsAddingCalendar = false;
        CalendarStatus   = "✓ Kalender angelegt";
        LoadCalendars();
    }

    [RelayCommand]
    private void CancelAddCalendar() => IsAddingCalendar = false;

    [RelayCommand]
    private async Task DeleteCalendarAsync(CalendarItemViewModel cal)
    {
        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
        var entity = db.Calendars.FirstOrDefault(c => c.Id == cal.Id);
        if (entity is null) return;
        db.Remove(entity);
        await db.SaveChangesAsync();
        CalendarStatus = "Kalender gelöscht.";
        LoadCalendars();
    }

    [RelayCommand]
    private async Task SetDefaultCalendarAsync(CalendarItemViewModel cal)
    {
        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
        foreach (var c in db.Calendars.ToList())
            c.IsDefault = c.Id == cal.Id;
        await db.SaveChangesAsync();
        CalendarStatus = $"✓ '{cal.Name}' ist jetzt der Standardkalender";
        LoadCalendars();
    }

    // ── Benachrichtigungen ───────────────────────────────────────────────────
    [RelayCommand]
    private void SaveBenachrichtigungen()
    {
        SaveSettings();
        StatusMessage = "✓ Gespeichert";
    }

    // ── Navigation ───────────────────────────────────────────────────────────
    [RelayCommand] private void Navigate(string section) => ActiveSection = section;
    [RelayCommand] private void Close() => OnClose?.Invoke();
}