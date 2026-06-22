using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ZeMail.UI.Models;

namespace ZeMail.UI.Controls;

/// <summary>
/// Custom Panel für die Monatsansicht: 42 Tageszellen (6x7-Grid).
/// Jede Zelle zeigt Tagesnummer + Events. Events können per Drag 
/// auf eine andere Zelle gezogen werden (Datum ändern, Uhrzeit bleibt).
/// </summary>
public class CalendarMonthGrid : Panel
{
    // ── Styled Properties ────────────────────────────────────────────────

    public static readonly StyledProperty<ObservableCollection<CalendarDayViewModel>?> DaysProperty =
        AvaloniaProperty.Register<CalendarMonthGrid, ObservableCollection<CalendarDayViewModel>?>(nameof(Days));

    public static readonly StyledProperty<IDataTemplate?> CellTemplateProperty =
        AvaloniaProperty.Register<CalendarMonthGrid, IDataTemplate?>(nameof(CellTemplate));

    public static readonly StyledProperty<IDataTemplate?> EventTemplateProperty =
        AvaloniaProperty.Register<CalendarMonthGrid, IDataTemplate?>(nameof(EventTemplate));

    public ObservableCollection<CalendarDayViewModel>? Days
    {
        get => GetValue(DaysProperty);
        set => SetValue(DaysProperty, value);
    }

    /// <summary>Template für den nicht-interaktiven Teil der Zelle (Hintergrund, Tagesnummer).</summary>
    public IDataTemplate? CellTemplate
    {
        get => GetValue(CellTemplateProperty);
        set => SetValue(CellTemplateProperty, value);
    }

    /// <summary>Template für ein einzelnes Event innerhalb einer Zelle.</summary>
    public IDataTemplate? EventTemplate
    {
        get => GetValue(EventTemplateProperty);
        set => SetValue(EventTemplateProperty, value);
    }

    // ── Code-Behind-Callbacks ────────────────────────────────────────────
    public Func<MonthDragState, Task>? OnDropCompleted { get; set; }
    public Action<CalendarEventViewModel>? EditRequested { get; set; }
    public Action<CalendarDayViewModel>? DaySelected { get; set; }

    // ── Layout-Konstanten ────────────────────────────────────────────────
    private const int Columns = 7;
    private const int Rows = 6;
    private const double HeaderHeight = 28.0;
    private const double EventRowHeight = 18.0;
    private const double TapThreshold = 5.0;

    // Hand-Cursor wird einmalig gecacht statt bei jedem Hover neu erzeugt
    private static readonly Cursor MoveCursor = new(StandardCursorType.Hand);

    // ── Interne Felder ───────────────────────────────────────────────────
    private readonly Dictionary<DateTime, Border> _cellControls = new();
    private readonly Dictionary<Guid, Control> _eventControls = new();
    private ObservableCollection<CalendarDayViewModel>? _subscribedDays;

    // Tage, deren Events.CollectionChanged wir aktuell abonniert haben –
    // damit wir bei jedem Rebuild gezielt alte Subscriptions lösen können,
    // statt uns auf eine einmalige Subscription in OnDaysChanged zu verlassen.
    private readonly List<CalendarDayViewModel> _subscribedDayEventCollections = new();

    private MonthDragState? _activeDrag;
    private DateTime _currentHoverDate;
    private Point _pressPosition;
    private bool _isDragging;
    private Border? _highlightOverlay;

    // ── Statische Initialisierung ────────────────────────────────────────

    static CalendarMonthGrid()
    {
        DaysProperty.Changed.AddClassHandler<CalendarMonthGrid>((g, e) => g.OnDaysChanged(e));
        CellTemplateProperty.Changed.AddClassHandler<CalendarMonthGrid>((g, _) => g.FullRebuild());
        EventTemplateProperty.Changed.AddClassHandler<CalendarMonthGrid>((g, _) => g.FullRebuild());
        AffectsMeasure<CalendarMonthGrid>(DaysProperty, CellTemplateProperty, EventTemplateProperty);
    }

    // ── Days-Wechsel (nur wenn die Collection-INSTANZ selbst wechselt) ────

    private void OnDaysChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (_subscribedDays is not null)
        {
            _subscribedDays.CollectionChanged -= OnDaysCollectionChanged;
            _subscribedDays = null;
        }

        if (e.NewValue is ObservableCollection<CalendarDayViewModel> newDays)
        {
            _subscribedDays = newDays;
            newDays.CollectionChanged += OnDaysCollectionChanged;
        }

        FullRebuild();
    }

    private void OnDaysCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => FullRebuild();

    private void OnDayEventsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => FullRebuild();

    // ── Event-Subscriptions pro Tag verwalten ────────────────────────────
    // Wird bei JEDEM FullRebuild aufgerufen, nicht nur bei Property-Change,
    // damit neu hinzugekommene CalendarDayViewModel-Instanzen (nach Clear()+Add())
    // ihre Events.CollectionChanged-Subscription garantiert bekommen.

    private void ResubscribeDayEvents(IReadOnlyList<CalendarDayViewModel> currentDays)
    {
        foreach (var oldDay in _subscribedDayEventCollections)
            oldDay.Events.CollectionChanged -= OnDayEventsCollectionChanged;

        _subscribedDayEventCollections.Clear();

        foreach (var day in currentDays)
        {
            day.Events.CollectionChanged += OnDayEventsCollectionChanged;
            _subscribedDayEventCollections.Add(day);
        }
    }

    // ── Aufbau ───────────────────────────────────────────────────────────

    private void FullRebuild()
    {
        Children.Clear();
        _cellControls.Clear();
        _eventControls.Clear();

        var days = Days;
        if (days is null || CellTemplate is null || EventTemplate is null)
        {
            ResubscribeDayEvents(Array.Empty<CalendarDayViewModel>());
            return;
        }

        int count = Math.Min(days.Count, Rows * Columns);

        // Subscriptions für die aktuellen Tage neu setzen, BEVOR wir rendern –
        // garantiert dass jede CalendarDayViewModel-Instanz, die gerade in
        // der Days-Collection steckt, beobachtet wird.
        ResubscribeDayEvents(days);

        for (int i = 0; i < count; i++)
        {
            var day = days[i];

            // ── Zellenhintergrund (Tagesnummer, Klick = SelectDay) ──
            Control? cellContent = null;
            if (CellTemplate.Match(day) && CellTemplate.Build(day) is Control built)
            {
                built.DataContext = day;
                cellContent = built;
            }

            var cellBorder = new Border
            {
                Tag = day.Date,
            };
            if (cellContent is not null)
                cellBorder.Child = cellContent;

            cellBorder.PointerPressed += (_, e) => OnCellPointerPressed(day, e);

            _cellControls[day.Date] = cellBorder;
            Children.Add(cellBorder);

            // ── Events in dieser Zelle ──
            foreach (var ev in day.Events)
            {
                if (!EventTemplate.Match(ev)) continue;
                if (EventTemplate.Build(ev) is not Control evControl) continue;

                evControl.DataContext = ev;
                AttachEventPointerHandlers(evControl, ev, day);
                _eventControls[ev.Id] = evControl;
                Children.Add(evControl);
            }
        }

        EnsureHighlightOverlay();
        Children.Add(_highlightOverlay!);

        InvalidateArrange();
    }

    // ── Zellen-Klick (SelectDay) ─────────────────────────────────────────

    private void OnCellPointerPressed(CalendarDayViewModel day, PointerPressedEventArgs e)
    {
        // Nur reagieren wenn der Klick nicht von einem Event-Control kam
        // (Events haben ihre eigenen Handler und markieren e.Handled selbst)
        if (e.Handled) return;
        DaySelected?.Invoke(day);
    }

    // ── Event-Pointer-Handler ────────────────────────────────────────────

    private void AttachEventPointerHandlers(Control control, CalendarEventViewModel ev, CalendarDayViewModel day)
    {
        // Events sind in der Monatsansicht ausschließlich verschiebbar (kein Resize
        // per Press-Position wie in Tag/Woche) → Hand-Cursor ist hier immer korrekt,
        // unabhängig von Hover-Position innerhalb des Controls.
        control.Cursor = MoveCursor;

        control.PointerPressed += (_, e) => OnEventPointerPressed(control, ev, day, e);
        control.PointerMoved += (_, e) => OnEventPointerMoved(ev, e);
        control.PointerReleased += (_, e) => OnEventPointerReleased(ev, e);
        control.PointerCaptureLost += (_, _) => CancelDrag();
    }

    private void OnEventPointerPressed(Control control, CalendarEventViewModel ev, CalendarDayViewModel day, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed) return;

        _pressPosition = e.GetPosition(this);
        _isDragging = false;

        _activeDrag = new MonthDragState
        {
            Event = ev,
            OriginalDate = day.Date,
            PreviewDate = day.Date,
        };
        _currentHoverDate = day.Date;

        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void OnEventPointerMoved(CalendarEventViewModel ev, PointerEventArgs e)
    {
        var drag = _activeDrag;
        if (drag is null || drag.Event.Id != ev.Id) return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            CancelDrag();
            return;
        }

        var posInGrid = e.GetPosition(this);

        if (!_isDragging)
        {
            var delta = posInGrid - _pressPosition;
            if (Math.Abs(delta.X) < TapThreshold && Math.Abs(delta.Y) < TapThreshold)
                return;

            _isDragging = true;
            ev.DragOpacity = 0.35;
        }

        var hoverDate = GetDateAtPosition(posInGrid);
        if (hoverDate is { } newDate && newDate != _currentHoverDate)
        {
            _currentHoverDate = newDate;
            drag.PreviewDate = newDate;
            UpdateHighlight(newDate);
        }

        e.Handled = true;
    }

    private void OnEventPointerReleased(CalendarEventViewModel ev, PointerReleasedEventArgs e)
    {
        var drag = _activeDrag;

        if (!_isDragging)
        {
            // Tap → Edit öffnen
            _activeDrag = null;
            EditRequested?.Invoke(ev);
            e.Handled = true;
            return;
        }

        if (drag is null || drag.Event.Id != ev.Id) return;

        CommitDrop(drag);
        e.Handled = true;
    }

    private void CommitDrop(MonthDragState drag)
    {
        HideHighlight();
        drag.Event.DragOpacity = 1.0;

        if (drag.PreviewDate != drag.OriginalDate)
        {
            // Uhrzeit erhalten, nur Datum wechseln
            var oldStartLocal = drag.Event.StartUtc.ToLocalTime();
            var oldEndLocal = drag.Event.EndUtc.ToLocalTime();
            var duration = oldEndLocal - oldStartLocal;

            var newStartLocal = drag.PreviewDate.Date.Add(oldStartLocal.TimeOfDay);

            drag.Event.StartUtc = newStartLocal.ToUniversalTime();
            drag.Event.EndUtc = newStartLocal.Add(duration).ToUniversalTime();
        }

        _activeDrag = null;
        _isDragging = false;

        if (drag.PreviewDate != drag.OriginalDate && OnDropCompleted is not null)
            _ = OnDropCompleted(drag);
    }

    public void CancelDrag()
    {
        var drag = _activeDrag;
        if (drag is not null)
        {
            HideHighlight();
            drag.Event.DragOpacity = 1.0;
        }

        _activeDrag = null;
        _isDragging = false;
    }

    // ── Zell-Geometrie ───────────────────────────────────────────────────

    private DateTime? GetDateAtPosition(Point posInGrid)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return null;

        var cellWidth = Bounds.Width / Columns;
        var cellHeight = Bounds.Height / Rows;

        var col = (int)Math.Clamp(posInGrid.X / cellWidth, 0, Columns - 1);
        var row = (int)Math.Clamp(posInGrid.Y / cellHeight, 0, Rows - 1);
        var index = row * Columns + col;

        var days = Days;
        if (days is null || index < 0 || index >= days.Count) return null;

        return days[index].Date;
    }

    private Rect GetCellRect(DateTime date)
    {
        var days = Days;
        if (days is null) return default;

        int index = -1;
        for (int i = 0; i < days.Count; i++)
        {
            if (days[i].Date == date) { index = i; break; }
        }
        if (index < 0) return default;

        var cellWidth = Bounds.Width / Columns;
        var cellHeight = Bounds.Height / Rows;

        int row = index / Columns;
        int col = index % Columns;

        return new Rect(col * cellWidth, row * cellHeight, cellWidth, cellHeight);
    }

    // ── Highlight-Overlay (Zielzelle hervorheben) ───────────────────────

    private void EnsureHighlightOverlay()
    {
        if (_highlightOverlay is not null) return;

        _highlightOverlay = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#335AC8FA")),
            BorderBrush = new SolidColorBrush(Color.Parse("#5AC8FA")),
            BorderThickness = new Thickness(2),
            IsHitTestVisible = false,
            IsVisible = false,
            ZIndex = 1000,
        };
    }

    private void UpdateHighlight(DateTime date)
    {
        if (_highlightOverlay is null) return;
        _highlightOverlay.IsVisible = true;
        InvalidateArrange();
    }

    private void HideHighlight()
    {
        if (_highlightOverlay is not null)
            _highlightOverlay.IsVisible = false;
    }

    // ── Measure / Arrange ────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        var cellWidth = availableSize.Width / Columns;
        var cellHeight = availableSize.Height / Rows;

        foreach (Control child in Children)
        {
            if (child == _highlightOverlay) continue;
            child.Measure(new Size(cellWidth, cellHeight));
        }

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cellWidth = finalSize.Width / Columns;
        var cellHeight = finalSize.Height / Rows;

        var days = Days;
        if (days is not null)
        {
            // Zellenhintergründe arrangieren
            for (int i = 0; i < days.Count && i < Rows * Columns; i++)
            {
                var day = days[i];
                if (!_cellControls.TryGetValue(day.Date, out var cellBorder)) continue;

                int row = i / Columns;
                int col = i % Columns;

                cellBorder.Arrange(new Rect(col * cellWidth, row * cellHeight, cellWidth, cellHeight));
            }

            // Events innerhalb ihrer Zelle stapeln
            for (int i = 0; i < days.Count && i < Rows * Columns; i++)
            {
                var day = days[i];
                int row = i / Columns;
                int col = i % Columns;

                double cellLeft = col * cellWidth;
                double cellTop = row * cellHeight;

                int evIndex = 0;
                foreach (var ev in day.Events)
                {
                    if (!_eventControls.TryGetValue(ev.Id, out var evControl)) continue;

                    double evTop = cellTop + HeaderHeight + evIndex * EventRowHeight;
                    double evHeight = EventRowHeight - 2;

                    // Nicht über die Zellgrenze hinaus zeichnen
                    if (evTop + evHeight > cellTop + cellHeight)
                    {
                        evControl.Arrange(new Rect(0, 0, 0, 0));
                    }
                    else
                    {
                        evControl.Arrange(new Rect(
                            cellLeft + 2, evTop,
                            Math.Max(cellWidth - 4, 10), evHeight));
                    }

                    evIndex++;
                }
            }
        }

        // Highlight-Overlay
        if (_highlightOverlay is not null)
        {
            if (_highlightOverlay.IsVisible)
            {
                var rect = GetCellRect(_currentHoverDate);
                _highlightOverlay.Arrange(rect);
            }
            else
            {
                _highlightOverlay.Arrange(new Rect(0, 0, 0, 0));
            }
        }

        return finalSize;
    }
}