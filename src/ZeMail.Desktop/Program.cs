using System;
using System.IO;
using System.Threading;
using Avalonia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZeMail.Application.Services;
using ZeMail.Core.Interfaces;
using ZeMail.Infrastructure.Mail;
using ZeMail.Infrastructure.Persistence;
using ZeMail.Infrastructure.Search;
using ZeMail.UI;

namespace ZeMail.Desktop;

class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static readonly CancellationTokenSource _syncCts = new();

    [STAThread]
    public static void Main(string[] args)
    {
        var sc = new ServiceCollection();

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ZE-Mail", "zemail.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        sc.AddDbContext<ZeMailDbContext>(opt =>
            opt.UseSqlite($"Data Source={dbPath}"));
        sc.AddScoped<IZeMailDbContext>(sp => sp.GetRequiredService<ZeMailDbContext>());

        sc.AddScoped<ICalendarService, CalendarService>();
        sc.AddScoped<ISmtpSenderService, SmtpSenderService>();
        sc.AddScoped<ISignatureService, SignatureService>();
        sc.AddScoped<ISearchService, SearchService>();
        sc.AddScoped<IImapSyncService, ImapSyncService>();
        sc.AddScoped<IAccountTestService, AccountTestService>();
        sc.AddScoped<ICalendarSyncService, CalDavSyncService>();

        sc.AddSingleton<CalendarSyncOrchestrator>();

        sc.AddLogging(b => b
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug));

        Services = sc.BuildServiceProvider();
        App.Services = Services;

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZeMailDbContext>();
        db.Database.Migrate();

        // CalendarSyncOrchestrator manuell starten
        Console.WriteLine("[ZE-Mail] Starte CalendarSyncOrchestrator...");
        var orchestrator = Services.GetRequiredService<CalendarSyncOrchestrator>();
        var startTask = orchestrator.StartAsync(_syncCts.Token);
        Console.WriteLine("[ZE-Mail] CalendarSyncOrchestrator gestartet.");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        // Beim Beenden sauber stoppen
        _syncCts.Cancel();
        _ = orchestrator.StopAsync(CancellationToken.None);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}