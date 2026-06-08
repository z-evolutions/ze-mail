using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.UI.Models;

namespace ZeMail.UI.ViewModels;

public partial class CalendarViewModel : ViewModelBase
{
    // ── Navigation ───────────────────────────────────────────────────────────
    [ObservableProperty]
    private DateTime _currentMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private string _currentMonthLabel = string.Empty;

    // ── Kalenderraster (6 Wochen × 7 Tage = 42 Zellen) ──────────────────────
    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];

    public ObservableCollection<string> WeekDayHeaders { get; } =
        ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];

    // ── Selektierter Tag + Events ─────────────────────────────────────────────
    [ObservableProperty]
    private CalendarDayViewModel? _selectedDay;

    [ObservableProperty]
    private ObservableCollection<CalendarEventViewModel> _selectedDayEvents = [];

    // ── Status ───────────────────────────────────────────────────────────────
    [ObservableProperty]
    private string _statusText = string.Empty;

    public CalendarViewModel()
    {
        BuildCalendar();
        _ = LoadEventsAsync();
    }

    // ── Monatsnavigation ─────────────────────────────────────────────────────
    [RelayCommand]
    private void PreviousMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(-1);
        BuildCalendar();
        _ = LoadEventsAsync();
    }

    [RelayCommand]
    private void NextMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(1);
        BuildCalendar();
        _ = LoadEventsAsync();
    }

    [RelayCommand]
    private void GoToToday()
    {
        CurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        BuildCalendar();
        _ = LoadEventsAsync();
    }

    [RelayCommand]
    private void SelectDay(CalendarDayViewModel? day)
    {
        if (day is null) return;

        if (SelectedDay is not null)
            SelectedDay.IsSelected = false;

        SelectedDay = day;
        day.IsSelected = true;

        SelectedDayEvents.Clear();
        foreach (var ev in day.Events)
            SelectedDayEvents.Add(ev);
    }

    [RelayCommand]
    private void NewEvent()
    {
        var date = SelectedDay?.Date ?? DateTime.Today;
        OpenEventEditor(null, date);
    }

    // ── Kalender aufbauen ─────────────────────────────────────────────────────
    private void BuildCalendar()
    {
        CurrentMonthLabel = CurrentMonth.ToString("MMMM yyyy");
        Days.Clear();

        // Ersten Tag des Monats — auf Montag zurückgehen
        var firstDay  = CurrentMonth;
        var startDate = firstDay.AddDays(-(((int)firstDay.DayOfWeek + 6) % 7));

        for (int i = 0; i < 42; i++)
        {
            var date = startDate.AddDays(i);
            Days.Add(new CalendarDayViewModel
            {
                Date           = date,
                IsToday        = date.Date == DateTime.Today,
                IsCurrentMonth = date.Month == CurrentMonth.Month,
                IsSelected     = date.Date == DateTime.Today && date.Month == CurrentMonth.Month
            });
        }

        SelectedDay = Days.FirstOrDefault(d => d.IsSelected);
    }

    // ── Events aus DB laden ───────────────────────────────────────────────────
    private async Task LoadEventsAsync()
    {
        if (App.Services is null) return;

        StatusText = "Lade Termine…";

        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider
                          .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

            var from = Days.First().Date;
            var to   = Days.Last().Date.AddDays(1);

            var events = db.CalendarEvents
                .Where(e => e.StartUtc < to && e.EndUtc >= from)
                .ToList();

            // Events auf Tage verteilen
            foreach (var day in Days)
                day.Events.Clear();

            foreach (var ev in events)
            {
                var local = ev.StartUtc.ToLocalTime();
                var day   = Days.FirstOrDefault(d => d.Date.Date == local.Date);
                if (day is null) continue;

                day.Events.Add(new CalendarEventViewModel
                {
                    Id       = ev.Id,
                    Title    = ev.Title,
                    Location = ev.Location,
                    StartUtc = ev.StartUtc,
                    EndUtc   = ev.EndUtc,
                    IsAllDay = ev.IsAllDay,
                });
            }

            // Selektierten Tag aktualisieren
            if (SelectedDay is not null)
            {
                SelectedDayEvents.Clear();
                foreach (var ev in SelectedDay.Events)
                    SelectedDayEvents.Add(ev);
            }

            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler: {ex.Message}";
        }
    }

    private void OpenEventEditor(CalendarEventViewModel? existing, DateTime date)
    {
        // Später: EventEditorWindow öffnen
    }
}