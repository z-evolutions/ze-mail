using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.UI.Models;

namespace ZeMail.UI.ViewModels;

public enum CalendarViewMode { Month, Week, Day }

public partial class CalendarViewModel : ViewModelBase
{
    // ── Ansichtsmodus ────────────────────────────────────────────────────────
    [ObservableProperty]
    private CalendarViewMode _viewMode = CalendarViewMode.Month;

    public bool IsMonthView => ViewMode == CalendarViewMode.Month;
    public bool IsWeekView  => ViewMode == CalendarViewMode.Week;
    public bool IsDayView   => ViewMode == CalendarViewMode.Day;

    partial void OnViewModeChanged(CalendarViewMode value)
    {
        OnPropertyChanged(nameof(IsMonthView));
        OnPropertyChanged(nameof(IsWeekView));
        OnPropertyChanged(nameof(IsDayView));
        BuildCalendar();
        _ = LoadEventsAsync();
    }

    // ── Navigation ───────────────────────────────────────────────────────────
    [ObservableProperty]
    private DateTime _currentMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private DateTime _currentWeekStart = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));

    [ObservableProperty]
    private DateTime _currentDay = DateTime.Today;

    [ObservableProperty]
    private string _currentPeriodLabel = string.Empty;

    // ── Kalenderraster ───────────────────────────────────────────────────────
    public ObservableCollection<CalendarDayViewModel> Days     { get; } = [];
    public ObservableCollection<CalendarDayViewModel> WeekDays { get; } = [];
    public ObservableCollection<string> WeekDayHeaders { get; } =
        ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];

    // ── Selektierter Tag ─────────────────────────────────────────────────────
    [ObservableProperty]
    private CalendarDayViewModel? _selectedDay;

    [ObservableProperty]
    private ObservableCollection<CalendarEventViewModel> _selectedDayEvents = [];

    // ── Stunden für Tagesansicht ─────────────────────────────────────────────
    public ObservableCollection<string> HourLabels { get; } = [];

    // ── Status ───────────────────────────────────────────────────────────────
    [ObservableProperty]
    private string _statusText = string.Empty;

    public CalendarViewModel()
    {
        for (int h = 0; h < 24; h++)
            HourLabels.Add($"{h:00}:00");

        BuildCalendar();
        _ = LoadEventsAsync();
    }

    // ── Ansicht wechseln ─────────────────────────────────────────────────────
    [RelayCommand] private void SetMonthView() => ViewMode = CalendarViewMode.Month;
    [RelayCommand] private void SetWeekView()  => ViewMode = CalendarViewMode.Week;
    [RelayCommand] private void SetDayView()   => ViewMode = CalendarViewMode.Day;

    // ── Navigation ───────────────────────────────────────────────────────────
    [RelayCommand]
    private void Previous()
    {
        switch (ViewMode)
        {
            case CalendarViewMode.Month:
                CurrentMonth = CurrentMonth.AddMonths(-1); break;
            case CalendarViewMode.Week:
                CurrentWeekStart = CurrentWeekStart.AddDays(-7); break;
            case CalendarViewMode.Day:
                CurrentDay = CurrentDay.AddDays(-1); break;
        }
        BuildCalendar();
        _ = LoadEventsAsync();
    }

    [RelayCommand]
    private void Next()
    {
        switch (ViewMode)
        {
            case CalendarViewMode.Month:
                CurrentMonth = CurrentMonth.AddMonths(1); break;
            case CalendarViewMode.Week:
                CurrentWeekStart = CurrentWeekStart.AddDays(7); break;
            case CalendarViewMode.Day:
                CurrentDay = CurrentDay.AddDays(1); break;
        }
        BuildCalendar();
        _ = LoadEventsAsync();
    }

    [RelayCommand]
    private void GoToToday()
    {
        CurrentMonth     = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        CurrentWeekStart = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        CurrentDay       = DateTime.Today;
        BuildCalendar();
        _ = LoadEventsAsync();
    }

    [RelayCommand]
    private void SelectDay(CalendarDayViewModel? day)
    {
        if (day is null) return;
        if (SelectedDay is not null) SelectedDay.IsSelected = false;
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
        switch (ViewMode)
        {
            case CalendarViewMode.Month:  BuildMonthCalendar();  break;
            case CalendarViewMode.Week:   BuildWeekCalendar();   break;
            case CalendarViewMode.Day:    BuildDayCalendar();    break;
        }
    }

    private void BuildMonthCalendar()
    {
        CurrentPeriodLabel = CurrentMonth.ToString("MMMM yyyy");
        Days.Clear();

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

    private void BuildWeekCalendar()
    {
        var weekEnd = CurrentWeekStart.AddDays(6);
        CurrentPeriodLabel = $"{CurrentWeekStart:dd. MMM} – {weekEnd:dd. MMM yyyy}";
        WeekDays.Clear();

        for (int i = 0; i < 7; i++)
        {
            var date = CurrentWeekStart.AddDays(i);
            WeekDays.Add(new CalendarDayViewModel
            {
                Date           = date,
                IsToday        = date.Date == DateTime.Today,
                IsCurrentMonth = true,
                IsSelected     = date.Date == DateTime.Today
            });
        }

        SelectedDay = WeekDays.FirstOrDefault(d => d.IsSelected) ?? WeekDays[0];
    }

    private void BuildDayCalendar()
    {
        CurrentPeriodLabel = CurrentDay.ToString("dddd, dd. MMMM yyyy");
        WeekDays.Clear();
        WeekDays.Add(new CalendarDayViewModel
        {
            Date           = CurrentDay,
            IsToday        = CurrentDay.Date == DateTime.Today,
            IsCurrentMonth = true,
            IsSelected     = true
        });
        SelectedDay = WeekDays[0];
    }

    // ── Events laden ─────────────────────────────────────────────────────────
    private async Task LoadEventsAsync()
    {
        if (App.Services is null) return;
        StatusText = "Lade Termine…";

        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider
                          .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

            var allDays = Days.Concat(WeekDays).ToList();
            if (!allDays.Any()) return;

            var from = allDays.Min(d => d.Date);
            var to   = allDays.Max(d => d.Date).AddDays(1);

            var events = db.CalendarEvents
                .Where(e => e.StartUtc < to && e.EndUtc >= from)
                .ToList();

            foreach (var day in allDays) day.Events.Clear();

            foreach (var ev in events)
            {
                var local = ev.StartUtc.ToLocalTime();
                var day   = allDays.FirstOrDefault(d => d.Date.Date == local.Date);
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

    private void OpenEventEditor(CalendarEventViewModel? existing, DateTime date) { }
}