using System;
using System.Text;
using Avalonia.Controls;
using ZeMail.UI.ViewModels;

namespace ZeMail.UI.Views;

public partial class ComposeWindow : Window
{
    private NativeWebView? _webView;
    private bool           _editorReady  = false;
    private string         _currentBody  = string.Empty;

    public ComposeWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += OnWindowOpened;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        _webView = this.FindControl<NativeWebView>("ComposeBodyWebView");
        if (_webView is not null)
        {
            _webView.NavigationStarted  += OnNavigationStarted;
            _webView.NavigationCompleted += OnEditorReady;
            LoadEditor();
        }
    }

    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        // Body-Sync über ze-mail://body?html=... Schema
        if (e.Request?.Scheme == "ze-mail")
        {
            e.Cancel = true;
            var query = e.Request.Query;
            if (query.StartsWith("?html="))
            {
                var encoded = query.Substring(6);
                try
                {
                    var bytes = Convert.FromBase64String(Uri.UnescapeDataString(encoded));
                    _currentBody = Encoding.UTF8.GetString(bytes);
                    if (DataContext is ComposeViewModel vm)
                        vm.Body = _currentBody;
                }
                catch { }
            }
        }
        else if (e.Request?.Scheme is "http" or "https")
        {
            e.Cancel = true;
            TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(e.Request);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ComposeViewModel vm)
            vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ComposeViewModel.Body) && !_editorReady)
            LoadEditor();
    }

    private void LoadEditor()
    {
        if (_webView is null) return;
        if (DataContext is not ComposeViewModel vm) return;

        var isDark      = App.Settings.Theme == "Dark";
        var bgColor     = isDark ? "#040d1a" : "#ffffff";
        var fgColor     = isDark ? "#e0f0ff" : "#0a1428";
        var borderColor = isDark ? "#0d2a4a" : "#b0c8e0";
        var sigColor    = isDark ? "#6090b0" : "#305070";

        var body = string.IsNullOrEmpty(vm.Body) ? "<br>" : vm.Body;

        if (!body.TrimStart().StartsWith("<"))
        {
            body = "<p>" + System.Net.WebUtility.HtmlEncode(body)
                .Replace("\n\n", "</p><p>")
                .Replace("\n", "<br>") + "</p>";
        }

        // JavaScript sendet Body-Änderungen über ze-mail:// Schema zurück
        var js =
            "const editor = document.getElementById('editor');" +
            "function syncBody() {" +
            "  const html = editor.innerHTML;" +
            "  const b64 = btoa(unescape(encodeURIComponent(html)));" +
            "  window.location.href = 'ze-mail://body?html=' + b64;" +
            "}" +
            "editor.addEventListener('blur', syncBody);" +
            "editor.addEventListener('keyup', function(e) { if(e.key === 'Tab') syncBody(); });";

        var html =
            "<!DOCTYPE html><html><head><meta charset='utf-8'>" +
            "<style>" +
            "* { box-sizing: border-box; margin: 0; padding: 0; }" +
            "html, body { width: 100%; height: 100%; background: " + bgColor + "; color: " + fgColor + "; font-family: Arial, sans-serif; font-size: 14px; line-height: 1.6; }" +
            "#editor { width: 100%; height: 100%; min-height: 200px; padding: 12px 16px; outline: none; word-break: break-word; }" +
            "#editor:focus { outline: none; }" +
            "a { color: #00cccc; }" +
            "table { max-width: 100% !important; width: auto !important; table-layout: fixed; }" +
            "td { word-break: break-word; }" +
            "img { max-width: 100%; height: auto; }" +
            ".ze-signature { margin-top: 16px; padding-top: 12px; border-top: 1px solid " + borderColor + "; color: " + sigColor + "; font-size: 13px; }" +
            "blockquote { border-left: 3px solid " + borderColor + "; padding-left: 12px; color: " + sigColor + "; margin: 8px 0; }" +
            "</style></head><body>" +
            "<div id='editor' contenteditable='true'>" + body + "</div>" +
            "<script>" + js + "</script>" +
            "</body></html>";

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(html));
        _webView.Source = new Uri("data:text/html;charset=utf-8;base64," + base64);
        _editorReady = true;
    }

    private void OnEditorReady(object? sender, EventArgs e)
    {
        _editorReady = true;
    }

    protected override void OnClosing(Avalonia.Controls.WindowClosingEventArgs e)
    {
        base.OnClosing(e);
    }
}