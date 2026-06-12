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
        new NavItem("Mail",     "M20,8L12,13L4,8V6L12,11L20,6M20,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V6C22,4.89 21.1,4 20,4Z",  "Mail"),
        new NavItem("Kalender", "M19,19H5V8H19M16,1V3H8V1H6V3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3H18V1M17,13H12V18H17V13Z", "Calendar"),
        new NavItem("Kontakte", "M12,4A4,4 0 0,1 16,8A4,4 0 0,1 12,12A4,4 0 0,1 8,8A4,4 0 0,1 12,4M12,14C16.42,14 20,15.79 20,18V20H4V18C4,15.79 7.58,14 12,14Z", "Contacts"),
        new NavItem("Aufgaben", "M21,7L9,19L3.5,13.5L4.91,12.09L9,16.17L19.59,5.59L21,7Z", "Tasks"),
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
    private async Task OpenSettings()
    {
        var vm  = new SettingsViewModel();
        var win = new SettingsWindow { DataContext = vm };

        vm.OnClose += () => win.Close();
        vm.OnAddAccount += async () =>
        {
            var setupVm  = new AccountSetupViewModel();
            var setupWin = new AccountSetupWindow { DataContext = setupVm };
            setupVm.OnSaved     += () => { setupWin.Close(); vm.LoadAccounts(); };
            setupVm.OnCancelled += () => setupWin.Close();
            await setupWin.ShowDialog(win);
        };

        if (Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime dt)
            await win.ShowDialog(dt.MainWindow!);
    }

    [RelayCommand]
    private async Task Sync()
    {
        if (CurrentView is MailboxViewModel mailbox)
            await mailbox.SyncCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void NewMail()
    {
        if (CurrentView is MailboxViewModel mailbox)
            mailbox.NewMailCommand.Execute(null);
    }
}

public record NavItem(string Label, string PathData, string Key);