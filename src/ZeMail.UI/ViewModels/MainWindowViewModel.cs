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

public record NavItem(string Label, string Icon, string Key);