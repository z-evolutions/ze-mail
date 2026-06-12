using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.Core.Interfaces;
using ZeMail.UI.Services;
using ZeMail.UI.ViewModels;
using ZeMail.UI.Views;

namespace ZeMail.UI;

public partial class App : Application
{
    public static IServiceProvider? Services { get; set; }
    public static AppSettings       Settings { get; private set; } = AppSettings.Load();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Theme anwenden
        RequestedThemeVariant = Settings.Theme == "Light"
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm  = new MainWindowViewModel();
            var mainWin = new MainWindow { DataContext = mainVm };
            desktop.MainWindow = mainWin;

            if (Services is not null)
            {
                using var scope = Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
                if (!db.Accounts.Any())
                {
                    var setupVm  = new AccountSetupViewModel();
                    var setupWin = new AccountSetupWindow { DataContext = setupVm };
                    setupVm.OnCancelled += () => setupWin.Close();
                    setupVm.OnSaved     += () => setupWin.Close();
                    mainWin.Opened += (_, _) => setupWin.Show();
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}