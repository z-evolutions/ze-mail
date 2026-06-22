using System;

namespace ZeMail.UI.Models;

/// <summary>
/// Einfacher Drag-State für die Monatsansicht.
/// Keine Pixel-Achse nötig wie bei CalendarEventDragState – nur die Zielzelle
/// (Tag) ist relevant, die Uhrzeit des Events bleibt beim Verschieben erhalten.
/// </summary>
public class MonthDragState
{
    public CalendarEventViewModel Event { get; init; } = null!;
    public DateTime OriginalDate { get; init; }
    public DateTime PreviewDate { get; set; }
}