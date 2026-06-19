using System;
using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
 
namespace ZeMail.UI.Models;

public partial class CalendarEventViewModel : ObservableObject
{
    public Guid    Id         { get; init; }
    public Guid?   CalendarId { get; init; }
    public string  Title      { get; init; } = string.Empty;
    public string? Location   { get; init; }
    public bool    IsAllDay   { get; init; }
    public string  Color      { get; init; } = "#5AC8FA";

    // Mutable für Drag & Drop
    [ObservableProperty] private DateTime _startUtc;
    [ObservableProperty] private DateTime _endUtc;

    // Drag-State
    [ObservableProperty] private bool   _isDragging   = false;
    [ObservableProperty] private double _dragOpacity  = 1.0;

    public string StartTime    => IsAllDay ? "Ganztägig" : StartUtc.ToLocalTime().ToString("HH:mm");
    public string EndTime      => IsAllDay ? string.Empty : EndUtc.ToLocalTime().ToString("HH:mm");
    public string TimeRange    => IsAllDay ? "Ganztägig" : $"{StartTime} – {EndTime}";
    public string DisplayTitle => IsAllDay ? Title : $"{TimeRange} {Title}";

    partial void OnStartUtcChanged(DateTime value)
    {
        OnPropertyChanged(nameof(StartTime));
        OnPropertyChanged(nameof(TimeRange));
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(TopOffset));
        OnPropertyChanged(nameof(EventHeight));
        OnPropertyChanged(nameof(TopMargin));
    }

    partial void OnEndUtcChanged(DateTime value)
    {
        OnPropertyChanged(nameof(EndTime));
        OnPropertyChanged(nameof(TimeRange));
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(EventHeight));
    }

    // 1px pro Minute, Höhe 1440 = 24h
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
            return Math.Max(20, duration);
        }
    }

    // ── Überlappungs-Layout ──────────────────────────────────────────────
    [ObservableProperty] private double _leftFraction  = 0.0;
    [ObservableProperty] private double _widthFraction = 1.0;

    partial void OnLeftFractionChanged(double value)
    {
        OnPropertyChanged(nameof(LeftOffset));
        OnPropertyChanged(nameof(EventWidth));
    }

    partial void OnWidthFractionChanged(double value)
    {
        OnPropertyChanged(nameof(LeftOffset));
        OnPropertyChanged(nameof(EventWidth));
    }

    private const double ReferenceWidth = 1000.0;
    public double LeftOffset => LeftFraction  * ReferenceWidth;
    public double EventWidth => WidthFraction * ReferenceWidth - 2.0;
    public Thickness TopMargin => new(0, TopOffset, 0, 0);
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