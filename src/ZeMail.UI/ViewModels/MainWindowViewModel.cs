using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZeMail.UI.Views;

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

    [RelayCommand]
    private async Task OpenAccountSetup()
    {
        var vm  = new AccountSetupViewModel();
        var win = new AccountSetupWindow { DataContext = vm };

        vm.OnSaved     += () => win.Close();
        vm.OnCancelled += () => win.Close();

        if (Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime dt)
        {
            await win.ShowDialog(dt.MainWindow!);
        }
    }
}

public record NavItem(string Label, string Icon, string Key);