using System;
using System.Net;
using System.Text;
using Avalonia.Controls;
using ZeMail.UI.ViewModels;

namespace ZeMail.UI.Views;

public partial class MailboxView : UserControl
{
    public MailboxView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadMessageBody();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MailboxViewModel vm)
            vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MailboxViewModel.SelectedMessage))
            LoadMessageBody();
    }

    private void LoadMessageBody()
    {
        var webView = this.FindControl<NativeWebView>("MailBodyWebView");
        if (webView is null) return;

        webView.NavigationStarted -= OnWebViewNavigationStarted;
        webView.NavigationStarted += OnWebViewNavigationStarted;

        if (DataContext is not MailboxViewModel vm || vm.SelectedMessage is null)
        {
            webView.Source = new Uri("about:blank");
            return;
        }

        var msg = vm.SelectedMessage;

        string html;
        if (!string.IsNullOrEmpty(msg.BodyHtml))
        {
            html = msg.BodyHtml.Contains("<body", StringComparison.OrdinalIgnoreCase)
                ? msg.BodyHtml.Replace(
                    "<body",
                    "<body style=\"background:#0a0a1a;color:#c0c0d0;font-family:sans-serif;\"",
                    StringComparison.OrdinalIgnoreCase)
                : "<html><body style='background:#0a0a1a;color:#c0c0d0;font-family:sans-serif;'>"
                  + msg.BodyHtml + "</body></html>";
        }
        else
        {
            html = "<!DOCTYPE html><html><head><meta charset='utf-8'><style>"
                   + "body{font-family:sans-serif;font-size:14px;"
                   + "background:#0a0a1a;color:#c0c0d0;padding:24px;line-height:1.6;}"
                   + "a{color:#7070ff;}</style></head><body><pre style='white-space:pre-wrap;font-family:sans-serif'>"
                   + WebUtility.HtmlEncode(msg.BodyText ?? "")
                   + "</pre></body></html>";
        }

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(html));
        webView.Source = new Uri("data:text/html;charset=utf-8;base64," + base64);
    }

    private void OnWebViewNavigationStarted(object? sender,
        WebViewNavigationStartingEventArgs e)
    {
        var uri = e.Request;
        if (uri is null) return;

        if (uri.Scheme is "http" or "https")
        {
            e.Cancel = true;
            TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(uri);
        }
    }
}