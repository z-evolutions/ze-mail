using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;

namespace ZeMail.Application.Services;

public class CalendarSyncOrchestrator : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CalendarSyncOrchestrator> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    public CalendarSyncOrchestrator(IServiceProvider services, ILogger<CalendarSyncOrchestrator> logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("CalendarSyncOrchestrator gestartet.");

        // Sofort beim Start syncen
        await SyncDueCalendarsAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, ct);
                await SyncDueCalendarsAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler im CalendarSyncOrchestrator.");
            }
        }
    }

    private async Task SyncDueCalendarsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
        var syncService = scope.ServiceProvider.GetRequiredService<ICalendarSyncService>();

        var now = DateTime.UtcNow;
        var calendars = await Task.Run(() =>
            db.Calendars
              .Where(c => c.Type == CalendarType.CalDav
                       && c.ServerUrl != null
                       && (c.LastSyncedAtUtc == null
                           || c.LastSyncedAtUtc.Value.AddMinutes(c.SyncIntervalMinutes) <= now))
              .ToList(), ct);

        foreach (var calendar in calendars)
        {
            if (ct.IsCancellationRequested) break;
            _logger.LogDebug("Starte Sync für Kalender {Name}.", calendar.Name);
            await syncService.SyncCalendarAsync(calendar, ct);
        }
    }
}