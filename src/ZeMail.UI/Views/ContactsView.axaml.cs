using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ZeMail.UI.Views;

public partial class ContactsView : UserControl
{
    public ContactsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}