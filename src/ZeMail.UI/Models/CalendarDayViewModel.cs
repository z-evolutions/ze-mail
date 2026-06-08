using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ZeMail.UI.Models;

public partial class CalendarEventViewModel : ObservableObject
{
    public Guid     Id          { get; init; }
    public string   Title       { get; init; } = string.Empty;
    public string?  Location    { get; init; }
    public DateTime StartUtc    { get; init; }
    public DateTime EndUtc      { get; init; }
    public bool     IsAllDay    { get; init; }
    public string   Color       { get; init; } = "#5AC8FA";

    public string StartTime => IsAllDay ? "Ganztägig" : StartUtc.ToLocalTime().ToString("HH:mm");
    public string DisplayTitle => IsAllDay ? Title : $"{StartTime} {Title}";
}

public partial class CalendarDayViewModel : ObservableObject
{
    public DateTime Date        { get; init; }
    public bool     IsToday     { get; init; }
    public bool     IsCurrentMonth { get; init; }
    public bool     IsSelected  { get; set; }

    public int DayNumber => Date.Day;
    public ObservableCollection<CalendarEventViewModel> Events { get; } = [];
}