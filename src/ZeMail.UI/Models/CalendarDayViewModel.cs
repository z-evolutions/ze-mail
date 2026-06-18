using System;
using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ZeMail.UI.Models;

public partial class CalendarEventViewModel : ObservableObject
{
    public Guid     Id       { get; init; }
    public string   Title    { get; init; } = string.Empty;
    public string?  Location { get; init; }
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc   { get; init; }
    public bool     IsAllDay { get; init; }
    public string   Color    { get; init; } = "#5AC8FA";

    public string StartTime    => IsAllDay ? "Ganztägig" : StartUtc.ToLocalTime().ToString("HH:mm");
    public string EndTime      => IsAllDay ? string.Empty : EndUtc.ToLocalTime().ToString("HH:mm");
    public string TimeRange    => IsAllDay ? "Ganztägig" : $"{StartTime} – {EndTime}";
    public string DisplayTitle => IsAllDay ? Title : $"{TimeRange} {Title}";

    public double TopOffset
    {
        get
        {
            if (IsAllDay) return 0;
            var local = StartUtc.ToLocalTime();
            return local.Hour * 60.0 + local.Minute;
        }
    }

    public double EventHeight
    {
        get
        {
            if (IsAllDay) return 60;
            var duration = (EndUtc - StartUtc).TotalMinutes;
            return Math.Max(30, duration);
        }
    }

    public Thickness TopMargin => new Thickness(0, TopOffset, 0, 0);
}

public partial class CalendarDayViewModel : ObservableObject
{
    public DateTime Date           { get; init; }
    public bool     IsToday        { get; init; }
    public bool     IsCurrentMonth { get; init; }
    public bool     IsSelected     { get; set; }

    public int    DayNumber      => Date.Day;
    public string DayNumberColor => IsCurrentMonth ? "#e0e0ff" : "#404060";
    public string DayBackground  => IsCurrentMonth ? "Transparent" : "#08081a";

    public ObservableCollection<CalendarEventViewModel> Events { get; } = [];
}