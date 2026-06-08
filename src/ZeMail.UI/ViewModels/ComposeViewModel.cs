using System;
using System.Collections.Generic;
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

    [ObservableProperty] private string _to      = string.Empty;
    [ObservableProperty] private string _cc      = string.Empty;
    [ObservableProperty] private string _subject = string.Empty;
    [ObservableProperty] private string _body    = string.Empty;

    [ObservableProperty] private bool   _isSending = false;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public Guid AccountId { get; init; }

    public event Action? OnSent;
    public event Action? OnCancelled;

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

        IsSending     = true;
        StatusMessage = "Wird gesendet…";

        try
        {
            if (App.Services is null)
                throw new InvalidOperationException("Services nicht verfügbar.");

            using var scope = App.Services.CreateScope();
            var smtp = scope.ServiceProvider
                            .GetRequiredService<ZeMail.Core.Interfaces.ISmtpSenderService>();

            var toList = new List<string>();
            foreach (var addr in To.Split(',', ';'))
            {
                var trimmed = addr.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    toList.Add(trimmed);
            }

            var ccList = new List<string>();
            foreach (var addr in Cc.Split(',', ';'))
            {
                var trimmed = addr.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    ccList.Add(trimmed);
            }

            var outgoing = new OutgoingMessage
            {
                AccountId = AccountId,
                To        = toList,
                Cc        = ccList,
                Subject   = Subject,
                BodyText  = Body,
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