using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZeMail.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    private NavItem _selectedNavItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public List<NavItem> NavItems { get; } =
    [
        new NavItem("Mail",     "✉",  "Mail"),
        new NavItem("Kalender", "📅", "Calendar"),
        new NavItem("Kontakte", "👤", "Contacts"),
        new NavItem("Aufgaben", "☑", "Tasks"),
    ];

    public MailboxViewModel  MailboxVM  { get; } = new();
    public CalendarViewModel CalendarVM { get; } = new();
    public ContactsViewModel ContactsVM { get; } = new();
    public TasksViewModel    TasksVM    { get; } = new();

    public MainWindowViewModel()
    {
        _selectedNavItem = NavItems[0];
        _currentView     = MailboxVM;
    }

    [RelayCommand]
    private void Navigate(NavItem item)
    {
        SelectedNavItem = item;
        CurrentView = item.Key switch
        {
            "Mail"     => MailboxVM,
            "Calendar" => CalendarVM,
            "Contacts" => ContactsVM,
            "Tasks"    => TasksVM,
            _          => MailboxVM
        };
    }
}

public record NavItem(string Label, string Icon, string Key);