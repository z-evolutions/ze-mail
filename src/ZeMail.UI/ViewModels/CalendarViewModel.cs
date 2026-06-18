using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.UI.Models;
using ZeMail.UI.Views;

namespace ZeMail.UI.ViewModels;

public enum CalendarViewMode { Month, Week, Day }

public partial class CalendarViewModel : ViewModelBase
{
    [ObservableProperty] private CalendarViewMode _viewMode = CalendarViewMode.Month;

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

    [ObservableProperty] private DateTime _currentMonth     = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime _currentWeekStart = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
    [ObservableProperty] private DateTime _currentDay       = DateTime.Today;
    [ObservableProperty] private string   _currentPeriodLabel = string.Empty;
    [ObservableProperty] private double   _currentTimeOffset  = 0;
    [ObservableProperty] private bool     _showCurrentTimeLine = false;

    private Timer? _clockTimer;

    public ObservableCollection<CalendarDayViewModel> Days     { get; } = [];
    public ObservableCollection<CalendarDayViewModel> WeekDays { get; } = [];
    public ObservableCollection<string> WeekDayHeaders { get; } =
        ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];

    [ObservableProperty] private CalendarDayViewModel? _selectedDay;
    [ObservableProperty] private ObservableCollection<CalendarEventViewModel> _selectedDayEvents = [];

    public ObservableCollection<string> HourLabels { get; } = [];

    [ObservableProperty] private string _statusText = string.Empty;

    public CalendarViewModel()
    {
        for (int h = 0; h < 24; h++)
            HourLabels.Add($"{h:00}:00");

        UpdateCurrentTimeLine();
        _clockTimer = new Timer(_ => UpdateCurrentTimeLine(),
            null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

        BuildCalendar();
        _ = LoadEventsAsync();
    }

    private void UpdateCurrentTimeLine()
    {
        var now = DateTime.Now;
        CurrentTimeOffset   = now.Hour * 60.0 + now.Minute;
        ShowCurrentTimeLine = true;
    }

    [RelayCommand] private void SetMonthView() => ViewMode = CalendarViewMode.Month;
    [RelayCommand] private void SetWeekView()  => ViewMode = CalendarViewMode.Week;
    [RelayCommand] private void SetDayView()   => ViewMode = CalendarViewMode.Day;

    [RelayCommand]
    private void Previous()
    {
        switch (ViewMode)
        {
            case CalendarViewMode.Month: CurrentMonth     = CurrentMonth.AddMonths(-1);   break;
            case CalendarViewMode.Week:  CurrentWeekStart = CurrentWeekStart.AddDays(-7); break;
            case CalendarViewMode.Day:   CurrentDay       = CurrentDay.AddDays(-1);       break;
        }
        BuildCalendar();
        _ = LoadEventsAsync();
    }

    [RelayCommand]
    private void Next()
    {
        switch (ViewMode)
        {
            case CalendarViewMode.Month: CurrentMonth     = CurrentMonth.AddMonths(1);   break;
            case CalendarViewMode.Week:  CurrentWeekStart = CurrentWeekStart.AddDays(7); break;
            case CalendarViewMode.Day:   CurrentDay       = CurrentDay.AddDays(1);       break;
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
        SelectedDay    = day;
        day.IsSelected = true;
        SelectedDayEvents.Clear();
        foreach (var ev in day.Events)
            SelectedDayEvents.Add(ev);
    }

    [RelayCommand]
    private void NewEvent() => OpenEventEditor(null, SelectedDay?.Date ?? DateTime.Today);

    [RelayCommand]
    private void EditEvent(CalendarEventViewModel? ev)
    {
        if (ev is null) return;
        OpenEventEditor(ev, ev.StartUtc.ToLocalTime());
    }

    private void BuildCalendar()
    {
        switch (ViewMode)
        {
            case CalendarViewMode.Month: BuildMonthCalendar(); break;
            case CalendarViewMode.Week:  BuildWeekCalendar();  break;
            case CalendarViewMode.Day:   BuildDayCalendar();   break;
        }
    }

    private void BuildMonthCalendar()
    {
        WeekDays.Clear();
        Days.Clear();
        CurrentPeriodLabel = CurrentMonth.ToString("MMMM yyyy");

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
        Days.Clear();
        WeekDays.Clear();
        var kw      = ISOWeek.GetWeekOfYear(CurrentWeekStart);
        var weekEnd = CurrentWeekStart.AddDays(6);
        CurrentPeriodLabel = $"KW {kw} · {CurrentWeekStart:dd. MMM} – {weekEnd:dd. MMM yyyy}";

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
        Days.Clear();
        WeekDays.Clear();
        var kw = ISOWeek.GetWeekOfYear(CurrentDay);
        CurrentPeriodLabel = $"KW {kw} · {CurrentDay:dddd, dd. MMMM yyyy}";
        WeekDays.Add(new CalendarDayViewModel
        {
            Date           = CurrentDay,
            IsToday        = CurrentDay.Date == DateTime.Today,
            IsCurrentMonth = true,
            IsSelected     = true
        });
        SelectedDay = WeekDays[0];
    }

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

            var fromUtc = allDays.Min(d => d.Date).ToUniversalTime();
            var toUtc   = allDays.Max(d => d.Date).AddDays(1).ToUniversalTime();

            // Kalenderfarben laden
            var calendarColors = await Task.Run(() =>
                db.Calendars
                .Where(c => c.IsVisible)
                .ToDictionary(c => c.Id, c => c.Color));

            var visibleCalendarIds = calendarColors.Keys.ToHashSet();

            var events = await Task.Run(() =>
                db.CalendarEvents
                .Where(e => e.StartUtc < toUtc && e.EndUtc >= fromUtc
                        && (e.CalendarId == null || visibleCalendarIds.Contains(e.CalendarId.Value)))
                .ToList());

            foreach (var day in allDays) day.Events.Clear();

            foreach (var ev in events)
            {
                var localDate = ev.StartUtc.ToLocalTime().Date;
                var day = allDays.FirstOrDefault(d => d.Date.Date == localDate);
                if (day is null) continue;

                var color = ev.CalendarId.HasValue && calendarColors.TryGetValue(ev.CalendarId.Value, out var c)
                    ? c : "#5AC8FA";

                day.Events.Add(new CalendarEventViewModel
                {
                    Id       = ev.Id,
                    Title    = ev.Title,
                    Location = ev.Location,
                    StartUtc = ev.StartUtc,
                    EndUtc   = ev.EndUtc,
                    IsAllDay = ev.IsAllDay,
                    Color    = color,
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

    private void OpenEventEditor(CalendarEventViewModel? existing, DateTime date)
    {
        if (App.Services is null) return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();
        var account = db.Accounts.FirstOrDefault();
        if (account is null) return;

        // Standarddauer + Zeitslot-Schritte aus Settings
        var stepMinutes     = App.Settings.DefaultEventDurationMinutes;
        if (stepMinutes <= 0) stepMinutes = 15;

        EventEditorViewModel vm;

        if (existing is null)
        {
            // Startzeit: aktuelle Uhrzeit auf nächsten Slot runden
            var now      = DateTime.Now;
            int totalMin = now.Hour * 60 + now.Minute;
            int snapped  = (int)Math.Ceiling((double)totalMin / stepMinutes) * stepMinutes;
            if (snapped >= 24 * 60) snapped = 23 * 60;

            var startTime = TimeSpan.FromMinutes(snapped);
            var endTime   = startTime.Add(TimeSpan.FromMinutes(stepMinutes));
            if (endTime.TotalMinutes >= 24 * 60)
                endTime = TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(45));

            vm = new EventEditorViewModel
            {
                AccountId = account.Id,
                StartDate = date.Date,
                EndDate   = date.Date,
            };
            vm.InitTimeSlots(stepMinutes);
            vm.SetStartTime(startTime);
            vm.SetEndTime(endTime);
        }
        else
        {
            vm = new EventEditorViewModel
            {
                EventId   = existing.Id,
                AccountId = account.Id,
                Title     = existing.Title,
                Location  = existing.Location ?? string.Empty,
                StartDate = existing.StartUtc.ToLocalTime().Date,
                EndDate   = existing.EndUtc.ToLocalTime().Date,
                IsAllDay  = existing.IsAllDay,
            };
            vm.InitTimeSlots(stepMinutes);
            vm.SetStartTime(existing.StartUtc.ToLocalTime().TimeOfDay);
            vm.SetEndTime(existing.EndUtc.ToLocalTime().TimeOfDay);
        }

        var win = new EventEditorWindow { DataContext = vm };
        vm.OnSaved     += () => { win.Close(); _ = LoadEventsAsync(); };
        vm.OnDeleted   += () => { win.Close(); _ = LoadEventsAsync(); };
        vm.OnCancelled += () => win.Close();
        win.Show();
    }
}