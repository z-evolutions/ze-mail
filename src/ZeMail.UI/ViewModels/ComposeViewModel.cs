using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.Core.Models;

namespace ZeMail.UI.ViewModels;

public enum ComposeMode { New, Reply, ReplyAll, Forward }

public partial class ComposeViewModel : ViewModelBase
{
    public ComposeMode Mode { get; init; } = ComposeMode.New;

    [ObservableProperty] private string _to            = string.Empty;
    [ObservableProperty] private string _cc            = string.Empty;
    [ObservableProperty] private string _subject       = string.Empty;
    [ObservableProperty] private string _body          = string.Empty;
    [ObservableProperty] private bool   _isSending     = false;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // ── Account-Auswahl ──────────────────────────────────────────────────────
    public ObservableCollection<AccountItemViewModel> Accounts { get; } = [];

    [ObservableProperty] private AccountItemViewModel? _selectedAccount;

    public Guid AccountId => SelectedAccount?.Id ?? Guid.Empty;

    public event Action? OnSent;
    public event Action? OnCancelled;

    public void Init()
    {
        LoadAccounts();
        ApplySignature();
    }

    private void LoadAccounts()
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
                Id           = a.Id,
                Name         = a.Name,
                EmailAddress = a.EmailAddress,
                ImapHost     = a.ImapHost,
                ImapPort     = a.ImapPort,
                SmtpHost     = a.SmtpHost,
                SmtpPort     = a.SmtpPort,
                Username     = a.Username,
                Password     = a.Password,
                Protocol     = a.Protocol,
            });
        }
        SelectedAccount = Accounts.FirstOrDefault();
    }

    private void ApplySignature()
    {
        if (App.Services is null) return;
        try
        {
            using var scope = App.Services.CreateScope();
            var db       = scope.ServiceProvider
                                .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();
            var settings = App.Settings;
            var isHtml   = settings.ComposeFormat == "HTML";
            var signatures = db.Signatures.ToList();

            ZeMail.Core.Entities.Signature? sig = Mode switch
            {
                ComposeMode.New      => signatures.FirstOrDefault(s => s.UseForNew     && s.IsDefault)
                                     ?? signatures.FirstOrDefault(s => s.UseForNew),
                ComposeMode.Reply    => signatures.FirstOrDefault(s => s.UseForReply   && s.IsDefault)
                                     ?? signatures.FirstOrDefault(s => s.UseForReply),
                ComposeMode.ReplyAll => signatures.FirstOrDefault(s => s.UseForReply   && s.IsDefault)
                                     ?? signatures.FirstOrDefault(s => s.UseForReply),
                ComposeMode.Forward  => signatures.FirstOrDefault(s => s.UseForForward && s.IsDefault)
                                     ?? signatures.FirstOrDefault(s => s.UseForForward),
                _                    => signatures.FirstOrDefault(s => s.IsDefault)
            };

            sig ??= signatures.FirstOrDefault(s => s.IsDefault);
            if (sig is null) return;

            var sigText = isHtml ? sig.BodyHtml : sig.BodyText;
            if (string.IsNullOrEmpty(sigText)) return;

            if (isHtml)
            {
                Body = string.IsNullOrEmpty(Body)
                    ? "<br><br><div class=\"ze-signature\">-- <br>" + sigText + "</div>"
                    : Body + "<br><br><div class=\"ze-signature\">-- <br>" + sigText + "</div>";
            }
            else
            {
                Body = string.IsNullOrEmpty(Body)
                    ? "\n\n-- \n" + sigText
                    : Body + "\n\n-- \n" + sigText;
            }
        }
        catch { }
    }

    [RelayCommand]
    private void Cancel() => OnCancelled?.Invoke();

    [RelayCommand]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(To))
        {
            StatusMessage = "Bitte Empfänger angeben.";
            return;
        }

        if (SelectedAccount is null)
        {
            StatusMessage = "Bitte Absender-Konto auswählen.";
            return;
        }

        IsSending     = true;
        StatusMessage = "Wird gesendet…";

        try
        {
            if (App.Services is null)
                throw new InvalidOperationException("Services nicht verfügbar.");

            using var scope = App.Services.CreateScope();
            var smtp = scope.ServiceProvider
                            .GetRequiredService<ZeMail.Core.Interfaces.ISmtpSenderService>();

            var toList = To.Split(',', ';')
                           .Select(a => a.Trim())
                           .Where(a => !string.IsNullOrEmpty(a))
                           .ToList();

            var ccList = Cc.Split(',', ';')
                           .Select(a => a.Trim())
                           .Where(a => !string.IsNullOrEmpty(a))
                           .ToList();

            var settings = App.Settings;
            var outgoing = new OutgoingMessage
            {
                AccountId = SelectedAccount.Id,
                To        = toList,
                Cc        = ccList,
                Subject   = Subject,
                BodyText  = settings.ComposeFormat == "HTML" ? null : Body,
                BodyHtml  = settings.ComposeFormat == "HTML" ? Body  : null,
            };

            await smtp.SendAsync(outgoing);
            StatusMessage = "✓ Gesendet!";
            await Task.Delay(800);
            OnSent?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Fehler: {ex.Message}";
        }
        finally
        {
            IsSending = false;
        }
    }
}