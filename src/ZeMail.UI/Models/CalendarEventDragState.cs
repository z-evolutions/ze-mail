using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ZeMail.UI.Models;

public enum DragMode { Move, ResizeTop, ResizeBottom }

/// <summary>
/// Hält den vollständigen Zustand eines laufenden Drag-Vorgangs.
/// Wird von CalendarDayPanel und CalendarWeekGrid geteilt.
/// </summary>
public partial class CalendarEventDragState : ObservableObject
{
    // Das gezogene Event
    public CalendarEventViewModel Event { get; init; } = null!;

    // Originalwerte für Abbruch (Escape / ungültiger Drop)
    public DateTime OriginalStartUtc { get; init; }
    public DateTime OriginalEndUtc   { get; init; }
    public int      OriginalDayIndex { get; init; }   // 0–6 in WeekGrid

    // Drag-Modus
    public DragMode Mode { get; init; } = DragMode.Move;

    // Y-Offset innerhalb des Events beim Klick (für Move: kein Sprung zur Maus)
    public double GrabOffsetY { get; init; }

    // Ghost-Position (live während Drag, in Panel-Koordinaten)
    [ObservableProperty] private double _ghostTop;
    [ObservableProperty] private double _ghostHeight;
    [ObservableProperty] private double _ghostLeft;
    [ObservableProperty] private double _ghostWidth;
    [ObservableProperty] private int    _ghostDayIndex;   // Wochenansicht: aktuelle Spalte

    // Snapped Preview-Zeiten (werden bei Pointer-Move gesetzt, bei Drop übernommen)
    [ObservableProperty] private DateTime _previewStartUtc;
    [ObservableProperty] private DateTime _previewEndUtc;
    [ObservableProperty] private int      _previewDayIndex;

    // Hilfsmethode: 15-Minuten-Snap
    public static DateTime SnapTo15Min(DateTime dt)
    {
        var totalMin = dt.Hour * 60 + dt.Minute;
        var snapped  = (int)Math.Round(totalMin / 15.0) * 15;
        snapped      = Math.Clamp(snapped, 0, 23 * 60 + 45);
        return dt.Date.AddMinutes(snapped);
    }

    // Hilfsmethode: Minuten → snapped DateTime auf Basis eines Referenzdatums
    public static DateTime MinutesToSnappedUtc(double minutes, DateTime dateLocal)
    {
        var totalMin = (int)Math.Round(minutes / 15.0) * 15;
        totalMin     = Math.Clamp(totalMin, 0, 23 * 60 + 45);
        return dateLocal.Date.AddMinutes(totalMin).ToUniversalTime();
    }
}