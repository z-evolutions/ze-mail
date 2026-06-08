using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ZeMail.UI.Views;

public partial class EventEditorWindow : Window
{
    public EventEditorWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}