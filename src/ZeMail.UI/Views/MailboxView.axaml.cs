using System;
using System.Net;
using System.Text;
using Avalonia.Controls;
using ZeMail.UI.Services;
using ZeMail.UI.ViewModels;

namespace ZeMail.UI.Views;

public partial class MailboxView : UserControl
{
    private const string DarkModeStyle =
        "background:#0a0a1a; color:#c0c0d0; font-family:sans-serif; " +
        "font-size:14px; line-height:1.6; padding:0; margin:0;";

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
            html = HtmlSanitizer.SanitizeAndWrap(msg.BodyHtml, DarkModeStyle);
        }
        else
        {
            html =
                "<!DOCTYPE html><html><head>" +
                "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline';\">" +
                "<meta charset='utf-8'>" +
                "<style>" +
                "body { " + DarkModeStyle + " padding: 24px; } " +
                "pre { white-space: pre-wrap; font-family: sans-serif; font-size: 14px; } " +
                "a { color: #7070ff; }" +
                "</style></head><body>" +
                "<pre>" + WebUtility.HtmlEncode(msg.BodyText ?? string.Empty) + "</pre>" +
                "</body></html>";
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