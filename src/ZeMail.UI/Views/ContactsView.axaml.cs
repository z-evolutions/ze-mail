using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ZeMail.UI.ViewModels;

namespace ZeMail.UI.Views;

public partial class ContactsView : UserControl
{
    public ContactsView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Website-TextBlock klickbar machen
        var websiteBlock = this.FindControl<TextBlock>("WebsiteText");
        if (websiteBlock is not null)
        {
            websiteBlock.Cursor = new Cursor(StandardCursorType.Hand);
            websiteBlock.PointerPressed += OnWebsiteClicked;
        }
    }

    private void OnWebsiteClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ContactsViewModel vm) return;
        var url = vm.SelectedContact?.Website;
        if (string.IsNullOrEmpty(url)) return;

        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;

        TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri(url));
    }
}