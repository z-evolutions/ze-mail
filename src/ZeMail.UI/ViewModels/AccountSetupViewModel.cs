using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ZeMail.UI.ViewModels;

public partial class AccountSetupViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _currentStep = 1;

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _emailAddress = string.Empty;

    [ObservableProperty]
    private string _protocol = "IMAP";

    [ObservableProperty]
    private string _imapHost = string.Empty;

    [ObservableProperty]
    private int _imapPort = 993;

    [ObservableProperty]
    private string _smtpHost = string.Empty;

    [ObservableProperty]
    private int _smtpPort = 465;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _unifiedInboxEnabled = true;

    [ObservableProperty]
    private string _statusMessage = "Bereit zum Verbindungstest.";

    [ObservableProperty]
    private bool _isTesting = false;

    [ObservableProperty]
    private bool _testSucceeded = false;

    public event Action? OnSaved;
    public event Action? OnCancelled;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
    }

    partial void OnEmailAddressChanged(string value)
    {
        var domain = value.Contains('@') ? value.Split('@')[1].ToLower() : "";
        switch (domain)
        {
            case "gmail.com":
                ImapHost = "imap.gmail.com"; ImapPort = 993;
                SmtpHost = "smtp.gmail.com"; SmtpPort = 465;
                Username = value;
                break;
            case "outlook.com":
            case "hotmail.com":
            case "live.com":
                ImapHost = "outlook.office365.com"; ImapPort = 993;
                SmtpHost = "smtp.office365.com";    SmtpPort = 587;
                Username = value;
                break;
            case "yahoo.com":
                ImapHost = "imap.mail.yahoo.com"; ImapPort = 993;
                SmtpHost = "smtp.mail.yahoo.com"; SmtpPort = 465;
                Username = value;
                break;
            case "icloud.com":
                ImapHost = "imap.mail.me.com"; ImapPort = 993;
                SmtpHost = "smtp.mail.me.com"; SmtpPort = 587;
                Username = value;
                break;
        }
    }

    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStep < 3) CurrentStep++;
    }

    [RelayCommand]
    private void PrevStep()
    {
        if (CurrentStep > 1) CurrentStep--;
    }

    [RelayCommand]
    private void Cancel() => OnCancelled?.Invoke();

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsTesting     = true;
        TestSucceeded = false;
        StatusMessage = "Verbinde mit IMAP-Server…";

        try
        {
            if (ZeMail.UI.App.Services is null)
                throw new InvalidOperationException("Services nicht verfügbar.");

            using var scope = ZeMail.UI.App.Services.CreateScope();
            var tester = scope.ServiceProvider
                              .GetRequiredService<ZeMail.Core.Interfaces.IAccountTestService>();

            var (success, message) = await tester.TestImapAsync(
                ImapHost, ImapPort, Username, Password);

            TestSucceeded = success;
            StatusMessage = message;
        }
        catch (Exception ex)
        {
            TestSucceeded = false;
            StatusMessage = $"✗ {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TestSucceeded) return;

        if (ZeMail.UI.App.Services is not null)
        {
            using var scope = ZeMail.UI.App.Services.CreateScope();
            var db = scope.ServiceProvider
                          .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

            var account = new ZeMail.Core.Entities.Account
            {
                Name                = DisplayName,
                EmailAddress        = EmailAddress,
                ImapHost            = ImapHost,
                ImapPort            = ImapPort,
                SmtpHost            = SmtpHost,
                SmtpPort            = SmtpPort,
                Username            = Username,
                Password            = Password,
                Protocol            = Protocol,
                UnifiedInboxEnabled = UnifiedInboxEnabled,
                CreatedAtUtc        = DateTime.UtcNow
            };

            db.Add(account);
            await db.SaveChangesAsync();

            StatusMessage = "Synchronisiere Ordner…";
            try
            {
                using var syncScope = ZeMail.UI.App.Services.CreateScope();
                var sync = syncScope.ServiceProvider
                                    .GetRequiredService<ZeMail.Core.Interfaces.IImapSyncService>();
                await sync.SyncAccountAsync(account.Id);
                StatusMessage = "✓ Ordner synchronisiert!";
                OnSaved?.Invoke();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sync-Fehler: {ex.GetType().Name}: {ex.Message}";
                if (ex.InnerException is not null)
                    StatusMessage += $"\nInner: {ex.InnerException.Message}";
            }
        }
    }
}