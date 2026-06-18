using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.Core.Entities;

namespace ZeMail.UI.ViewModels;

// ── Mini-Kalendertag ──────────────────────────────────────────────────────────
public class MiniCalendarDay : ObservableObject
{
    public DateTime Date           { get; init; }
    public string   Label          { get; init; } = string.Empty;
    public bool     IsCurrentMonth { get; init; }
    public bool     IsToday        { get; init; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

// ── Kalender-Auswahl Item ────────────────────────────────────────────────────
public class CalendarPickerItem
{
    public Guid   Id    { get; init; }
    public string Name  { get; init; } = string.Empty;
    public string Color { get; init; } = "#3a3aff";
}

public partial class EventEditorViewModel : ViewModelBase
{
    public Guid?  EventId   { get; init; }
    public Guid   AccountId { get; init; }
    public bool   IsEditMode => EventId.HasValue;

    [ObservableProperty] private string _title         = string.Empty;
    [ObservableProperty] private string _description   = string.Empty;
    [ObservableProperty] private string _location      = string.Empty;
    [ObservableProperty] private bool   _isAllDay      = false;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool   _isSaving      = false;

    // ── Kalender-Auswahl ──────────────────────────────────────────────────────
    public ObservableCollection<CalendarPickerItem> AvailableCalendars { get; } = [];
    [ObservableProperty] private CalendarPickerItem? _selectedCalendar;

    public Guid? SelectedCalendarId => SelectedCalendar?.Id;

    public void LoadCalendars(Guid? currentCalendarId = null)
    {
        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();
        AvailableCalendars.Clear();
        var calendars = Task.Run(() => db.Calendars.ToList()).Result;
        foreach (var c in calendars)
        {
            AvailableCalendars.Add(new CalendarPickerItem
            {
                Id    = c.Id,
                Name  = c.Name,
                Color = c.Color,
            });
        }
        SelectedCalendar = currentCalendarId.HasValue
            ? AvailableCalendars.FirstOrDefault(c => c.Id == currentCalendarId.Value)
            : AvailableCalendars.FirstOrDefault(c => c.Id == GetDefaultCalendarId())
              ?? AvailableCalendars.FirstOrDefault();
    }

    private Guid? GetDefaultCalendarId()
    {
        if (App.Services is null) return null;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();
        return Task.Run(() => db.Calendars.FirstOrDefault(c => c.IsDefault)?.Id).Result;
    }

    // ── Datum ────────────────────────────────────────────────────────────────
    private DateTime _startDate = DateTime.Today;
    private DateTime _endDate   = DateTime.Today;

    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value))
            {
                OnPropertyChanged(nameof(StartDateLabel));
                RebuildStartCalendar();
            }
        }
    }

    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            if (SetProperty(ref _endDate, value))
            {
                OnPropertyChanged(nameof(EndDateLabel));
                RebuildEndCalendar();
            }
        }
    }

    public string StartDateLabel => _startDate.ToString("dd. MMMM yyyy");
    public string EndDateLabel   => _endDate.ToString("dd. MMMM yyyy");

    // ── Minikalender Start ───────────────────────────────────────────────────
    [ObservableProperty] private bool   _startCalendarOpen  = false;
    [ObservableProperty] private string _startCalendarTitle = string.Empty;
    private DateTime _startCalendarMonth;
    public ObservableCollection<MiniCalendarDay> StartCalendarDays { get; } = [];
    public ObservableCollection<string> StartWeekHeaders { get; } =
        ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];

    // ── Minikalender Ende ────────────────────────────────────────────────────
    [ObservableProperty] private bool   _endCalendarOpen  = false;
    [ObservableProperty] private string _endCalendarTitle = string.Empty;
    private DateTime _endCalendarMonth;
    public ObservableCollection<MiniCalendarDay> EndCalendarDays { get; } = [];
    public ObservableCollection<string> EndWeekHeaders { get; } =
        ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];

    // ── Zeitauswahl ──────────────────────────────────────────────────────────
    public ObservableCollection<string> TimeSlots { get; } = [];

    [ObservableProperty] private string? _selectedStartTime;
    [ObservableProperty] private string? _selectedEndTime;

    private int _stepMinutes = 15;

    public void InitTimeSlots(int stepMinutes)
    {
        _stepMinutes = stepMinutes > 0 ? stepMinutes : 15;
        TimeSlots.Clear();
        for (int total = 0; total < 24 * 60; total += _stepMinutes)
            TimeSlots.Add($"{total / 60:00}:{total % 60:00}");
    }

    public void SetStartTime(TimeSpan t)
    {
        var snapped = SnapToSlot(t);
        SelectedStartTime = $"{snapped.Hours:00}:{snapped.Minutes:00}";
    }

    public void SetEndTime(TimeSpan t)
    {
        var snapped = SnapToSlot(t);
        SelectedEndTime = $"{snapped.Hours:00}:{snapped.Minutes:00}";
    }

    private TimeSpan SnapToSlot(TimeSpan t)
    {
        int totalMin = (int)t.TotalMinutes;
        int snapped  = (int)Math.Round((double)totalMin / _stepMinutes) * _stepMinutes;
        snapped      = Math.Clamp(snapped, 0, 23 * 60 + (60 - _stepMinutes));
        return TimeSpan.FromMinutes(snapped);
    }

    private TimeSpan ParseSelectedTime(string? s)
    {
        if (s is null) return TimeSpan.Zero;
        var parts = s.Split(':');
        if (parts.Length == 2
            && int.TryParse(parts[0], out int h)
            && int.TryParse(parts[1], out int m))
            return new TimeSpan(h, m, 0);
        return TimeSpan.Zero;
    }

    // ── Minikalender Logik ───────────────────────────────────────────────────
    private void RebuildStartCalendar()
    {
        BuildCalendarDays(_startCalendarMonth, StartDate, StartCalendarDays);
        StartCalendarTitle = _startCalendarMonth.ToString("MMMM yyyy");
    }

    private void RebuildEndCalendar()
    {
        BuildCalendarDays(_endCalendarMonth, EndDate, EndCalendarDays);
        EndCalendarTitle = _endCalendarMonth.ToString("MMMM yyyy");
    }

    private static void BuildCalendarDays(DateTime month, DateTime selected,
        ObservableCollection<MiniCalendarDay> target)
    {
        target.Clear();
        var first     = new DateTime(month.Year, month.Month, 1);
        var startDate = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        for (int i = 0; i < 42; i++)
        {
            var d = startDate.AddDays(i);
            target.Add(new MiniCalendarDay
            {
                Date           = d,
                Label          = d.Day.ToString(),
                IsCurrentMonth = d.Month == month.Month,
                IsToday        = d.Date == DateTime.Today,
                IsSelected     = d.Date == selected.Date,
            });
        }
    }

    [RelayCommand]
    private void ToggleStartCalendar()
    {
        EndCalendarOpen   = false;
        StartCalendarOpen = !StartCalendarOpen;
        if (StartCalendarOpen)
        {
            _startCalendarMonth = new DateTime(StartDate.Year, StartDate.Month, 1);
            RebuildStartCalendar();
        }
    }

    [RelayCommand]
    private void ToggleEndCalendar()
    {
        StartCalendarOpen = false;
        EndCalendarOpen   = !EndCalendarOpen;
        if (EndCalendarOpen)
        {
            _endCalendarMonth = new DateTime(EndDate.Year, EndDate.Month, 1);
            RebuildEndCalendar();
        }
    }

    [RelayCommand]
    private void StartCalendarPrev()
    {
        _startCalendarMonth = _startCalendarMonth.AddMonths(-1);
        RebuildStartCalendar();
    }

    [RelayCommand]
    private void StartCalendarNext()
    {
        _startCalendarMonth = _startCalendarMonth.AddMonths(1);
        RebuildStartCalendar();
    }

    [RelayCommand]
    private void EndCalendarPrev()
    {
        _endCalendarMonth = _endCalendarMonth.AddMonths(-1);
        RebuildEndCalendar();
    }

    [RelayCommand]
    private void EndCalendarNext()
    {
        _endCalendarMonth = _endCalendarMonth.AddMonths(1);
        RebuildEndCalendar();
    }

    [RelayCommand]
    private void SelectStartDay(MiniCalendarDay? day)
    {
        if (day is null) return;
        var previousStart = StartDate;
        StartDate = day.Date;
        if (EndDate < StartDate || EndDate == previousStart)
            EndDate = StartDate;
        StartCalendarOpen = false;
    }

    [RelayCommand]
    private void SelectEndDay(MiniCalendarDay? day)
    {
        if (day is null) return;
        EndDate         = day.Date;
        EndCalendarOpen = false;
    }

    public string WindowTitle => IsEditMode ? "Termin bearbeiten" : "Neuer Termin";

    public event Action? OnSaved;
    public event Action? OnCancelled;
    public event Action? OnDeleted;

    [RelayCommand]
    private void Cancel() => OnCancelled?.Invoke();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            StatusMessage = "Bitte Titel eingeben.";
            return;
        }

        IsSaving      = true;
        StatusMessage = "Speichern…";

        try
        {
            if (App.Services is null)
                throw new InvalidOperationException("Services nicht verfügbar.");

            using var scope = App.Services.CreateScope();
            var svc = scope.ServiceProvider
                           .GetRequiredService<ZeMail.Core.Interfaces.ICalendarService>();

            DateTime startLocal, endLocal;

            if (IsAllDay)
            {
                startLocal = StartDate.Date;
                endLocal   = EndDate.Date.AddDays(1);
            }
            else
            {
                startLocal = StartDate.Date.Add(ParseSelectedTime(SelectedStartTime));
                endLocal   = EndDate.Date.Add(ParseSelectedTime(SelectedEndTime));
            }

            var startUtc = startLocal.ToUniversalTime();
            var endUtc   = endLocal.ToUniversalTime();

            if (endUtc <= startUtc)
            {
                StatusMessage = "Ende muss nach dem Start liegen.";
                IsSaving = false;
                return;
            }

            if (IsEditMode)
            {
                var existing = await svc.GetEventAsync(EventId!.Value);
                if (existing is null)
                {
                    StatusMessage = "Termin nicht gefunden.";
                    IsSaving = false;
                    return;
                }
                existing.Title       = Title;
                existing.Description = Description;
                existing.Location    = Location;
                existing.StartUtc    = startUtc;
                existing.EndUtc      = endUtc;
                existing.IsAllDay    = IsAllDay;
                existing.CalendarId  = SelectedCalendarId;
                await svc.UpdateEventAsync(existing);
            }
            else
            {
                await svc.CreateEventAsync(new CalendarEvent
                {
                    AccountId   = AccountId,
                    CalendarId  = SelectedCalendarId,
                    Title       = Title,
                    Description = Description,
                    Location    = Location,
                    StartUtc    = startUtc,
                    EndUtc      = endUtc,
                    IsAllDay    = IsAllDay,
                });
            }

            StatusMessage = "✓ Gespeichert";
            await Task.Delay(500);
            OnSaved?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Fehler: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!IsEditMode || App.Services is null) return;

        IsSaving      = true;
        StatusMessage = "Löschen…";

        try
        {
            using var scope = App.Services.CreateScope();
            var svc = scope.ServiceProvider
                           .GetRequiredService<ZeMail.Core.Interfaces.ICalendarService>();
            await svc.DeleteEventAsync(EventId!.Value);
            OnDeleted?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Fehler: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }
}