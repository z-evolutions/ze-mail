using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using ZeMail.UI.Models;

namespace ZeMail.UI.Controls;

public class CalendarWeekGrid : Panel
{
    // ── Styled Properties ────────────────────────────────────────────────

    public static readonly StyledProperty<ObservableCollection<CalendarDayViewModel>?> WeekDaysProperty =
        AvaloniaProperty.Register<CalendarWeekGrid, ObservableCollection<CalendarDayViewModel>?>(nameof(WeekDays));

    public static readonly StyledProperty<IDataTemplate?> EventTemplateProperty =
        AvaloniaProperty.Register<CalendarWeekGrid, IDataTemplate?>(nameof(EventTemplate));

    public ObservableCollection<CalendarDayViewModel>? WeekDays
    {
        get => GetValue(WeekDaysProperty);
        set => SetValue(WeekDaysProperty, value);
    }

    public IDataTemplate? EventTemplate
    {
        get => GetValue(EventTemplateProperty);
        set => SetValue(EventTemplateProperty, value);
    }

    public Func<CalendarEventDragState, Task>? OnDropCompleted { get; set; }
    public Action<CalendarEventViewModel>? EditRequested { get; set; }

    // ── Interne Felder ───────────────────────────────────────────────────

    private readonly CalendarDayPanel[] _columns = new CalendarDayPanel[7];
    private CalendarEventDragState? _activeDrag;
    private int _currentDragColumn = -1;
    private bool _pointerHandlersAttached = false;
    private ObservableCollection<CalendarDayViewModel>? _subscribedCollection;

    // ── Statische Initialisierung ────────────────────────────────────────

    static CalendarWeekGrid()
    {
        WeekDaysProperty.Changed.AddClassHandler<CalendarWeekGrid>((g, e) => g.OnWeekDaysChanged(e));
        EventTemplateProperty.Changed.AddClassHandler<CalendarWeekGrid>((g, _) => g.Rebuild());
        AffectsMeasure<CalendarWeekGrid>(WeekDaysProperty, EventTemplateProperty);
    }

    // ── WeekDays-Wechsel ─────────────────────────────────────────────────

    private void OnWeekDaysChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (_subscribedCollection is not null)
        {
            _subscribedCollection.CollectionChanged -= OnWeekDaysCollectionChanged;
            _subscribedCollection = null;
        }
        if (e.NewValue is ObservableCollection<CalendarDayViewModel> newCol)
        {
            _subscribedCollection = newCol;
            newCol.CollectionChanged += OnWeekDaysCollectionChanged;
        }
        Rebuild();
    }

    private void OnWeekDaysCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Rebuild();

    // ── Aufbau ───────────────────────────────────────────────────────────

    private void Rebuild()
    {
        CalendarDayPanel.Log($"[WeekGrid.Rebuild] Wird ausgeführt. WeekDays-Anzahl={WeekDays?.Count ?? -1}");

        if (_pointerHandlersAttached)
        {
            // FIX: PointerPressed-Handler nicht mehr nötig (kein Bubbling-Workaround mehr)
            PointerMoved -= OnWeekGridPointerMoved;
            PointerReleased -= OnWeekGridPointerReleased;
            PointerCaptureLost -= OnCaptureLost;
            _pointerHandlersAttached = false;
        }

        Children.Clear();
        Array.Clear(_columns);

        var days = WeekDays;
        if (days is null || EventTemplate is null) return;

        int count = Math.Min(days.Count, 7);
        for (int i = 0; i < count; i++)
        {
            var day = days[i];
            var panel = new CalendarDayPanel
            {
                Events = day.Events,
                EventTemplate = EventTemplate,
                ColumnDate = day.Date,
                ColumnIndex = i,
                Height = 1440,
                OnDropCompleted = OnDropCompleted,
                EditRequested = EditRequested,
            };

            var capturedIndex = i;
            // FIX: Callback bekommt jetzt direkt die PointerPressedEventArgs vom Panel mit
            panel.DragStartedCallback = (_, state, pressArgs) =>
                OnColumnDragStarted(capturedIndex, state, pressArgs);

            _columns[i] = panel;
            Children.Add(panel);
        }

        // FIX: PointerPressed-Subscription auf WeekGrid-Ebene entfernt – feuert ohnehin nie
        // wegen e.Handled=true im Kind-Control. Capture läuft jetzt direkt über den
        // PointerPressedEventArgs, den das Panel im DragStartedCallback mitgibt.
        PointerMoved += OnWeekGridPointerMoved;
        PointerReleased += OnWeekGridPointerReleased;
        PointerCaptureLost += OnCaptureLost;
        _pointerHandlersAttached = true;

        CalendarDayPanel.Log($"[WeekGrid.Rebuild] Fertig. {count} Spalten erzeugt, Pointer-Handler attached={_pointerHandlersAttached}");
    }

    // ── Drag-Koordination ────────────────────────────────────────────────

    private void OnColumnDragStarted(int colIndex, CalendarEventDragState state, PointerPressedEventArgs pressArgs)
    {
        CalendarDayPanel.Log($"[WeekGrid] DragStarted column={colIndex}");
        _activeDrag = state;
        _currentDragColumn = colIndex;

        // FIX: Capture direkt vom übergebenen PressArgs setzen, kein Bubbling nötig
        pressArgs.Pointer.Capture(this);
        CalendarDayPanel.Log("[WeekGrid] Capture auf WeekGrid gesetzt (direkt vom PressArgs)");
    }

    private void OnWeekGridPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_activeDrag is null) return;

        CalendarDayPanel.Log($"[WeekGrid] PointerMoved X={e.GetPosition(this).X:F0} Y={e.GetPosition(this).Y:F0}");

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            CancelActiveDrag();
            return;
        }

        var posInGrid = e.GetPosition(this);
        var newColIdx = GetColumnAtX(posInGrid.X);

        if (newColIdx < 0 || newColIdx >= _columns.Length || _columns[newColIdx] is null)
        {
            e.Handled = true;
            return;
        }

        if (newColIdx != _currentDragColumn)
        {
            CalendarDayPanel.Log($"[WeekGrid] Spaltenwechsel {_currentDragColumn} -> {newColIdx}");

            if (_currentDragColumn >= 0 && _currentDragColumn < _columns.Length)
                _columns[_currentDragColumn]?.ReceiveDragLeave();

            _currentDragColumn = newColIdx;
            _activeDrag.GhostDayIndex = newColIdx;
            _activeDrag.PreviewDayIndex = newColIdx;

            var days = WeekDays;
            if (days is not null && newColIdx < days.Count)
            {
                var duration = (_activeDrag.OriginalEndUtc - _activeDrag.OriginalStartUtc).TotalMinutes;
                var timeOfDay = _activeDrag.PreviewStartUtc.ToLocalTime().TimeOfDay;
                var newStartLocal = days[newColIdx].Date.Date.Add(timeOfDay);
                _activeDrag.PreviewStartUtc = newStartLocal.ToUniversalTime();
                _activeDrag.PreviewEndUtc = _activeDrag.PreviewStartUtc.AddMinutes(duration);
            }

            _columns[newColIdx].TakeOverDrag(_activeDrag);
        }

        var colLeft = GetColumnLeft(newColIdx);
        var posInPanel = new Point(posInGrid.X - colLeft, posInGrid.Y);
        _columns[newColIdx].UpdateExternalDrag(_activeDrag, posInPanel);

        e.Handled = true;
    }

    private void OnWeekGridPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        CalendarDayPanel.Log($"[WeekGrid] PointerReleased _activeDrag is null: {_activeDrag is null}");

        if (_activeDrag is null) return;

        var posInGrid = e.GetPosition(this);
        var dropColIdx = GetColumnAtX(posInGrid.X);

        var days = WeekDays;
        if (days is not null && dropColIdx >= 0 && dropColIdx < days.Count)
        {
            var duration = (_activeDrag.OriginalEndUtc - _activeDrag.OriginalStartUtc).TotalMinutes;
            var timeOfDay = _activeDrag.PreviewStartUtc.ToLocalTime().TimeOfDay;
            var newStartLocal = days[dropColIdx].Date.Date.Add(timeOfDay);
            _activeDrag.PreviewStartUtc = newStartLocal.ToUniversalTime();
            _activeDrag.PreviewEndUtc = _activeDrag.PreviewStartUtc.AddMinutes(duration);
            _activeDrag.PreviewDayIndex = dropColIdx;
        }

        var state = _activeDrag;
        state.Event.StartUtc = state.PreviewStartUtc;
        state.Event.EndUtc = state.PreviewEndUtc;
        state.Event.DragOpacity = 1.0;

        for (int i = 0; i < _columns.Length; i++)
            _columns[i]?.ReceiveDragLeave();

        _activeDrag = null;
        _currentDragColumn = -1;

        CalendarDayPanel.Log($"[WeekGrid] Commit -> OnDropCompleted is null: {OnDropCompleted is null}");
        if (OnDropCompleted is not null)
            _ = OnDropCompleted(state);

        e.Handled = true;
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        CalendarDayPanel.Log("[WeekGrid] OnCaptureLost gefeuert!");
        CancelActiveDrag();
    }

    private void CancelActiveDrag()
    {
        if (_activeDrag is null) return;

        _activeDrag.Event.StartUtc = _activeDrag.OriginalStartUtc;
        _activeDrag.Event.EndUtc = _activeDrag.OriginalEndUtc;
        _activeDrag.Event.DragOpacity = 1.0;

        for (int i = 0; i < _columns.Length; i++)
            _columns[i]?.ReceiveDragLeave();

        _activeDrag = null;
        _currentDragColumn = -1;
    }

    // ── Spalten-Geometrie ────────────────────────────────────────────────

    private int GetColumnAtX(double x)
    {
        if (Children.Count == 0) return -1;
        var colWidth = Bounds.Width / Children.Count;
        return (int)Math.Clamp(x / colWidth, 0, Children.Count - 1);
    }

    private double GetColumnLeft(int colIndex)
    {
        if (Children.Count == 0) return 0;
        return Bounds.Width / Children.Count * colIndex;
    }

    // ── Measure / Arrange ────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        int count = Children.Count;
        if (count == 0) return new Size(availableSize.Width, 1440);

        var colWidth = availableSize.Width / count;
        foreach (Control child in Children)
            child.Measure(new Size(colWidth, 1440));

        return new Size(availableSize.Width, 1440);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = Children.Count;
        if (count == 0) return new Size(finalSize.Width, 1440);

        var colWidth = finalSize.Width / count;
        for (int i = 0; i < count; i++)
            Children[i].Arrange(new Rect(i * colWidth, 0, colWidth, 1440));

        return new Size(finalSize.Width, 1440);
    }
}