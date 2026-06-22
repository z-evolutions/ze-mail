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
        if (_pointerHandlersAttached)
        {
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
            panel.DragStartedCallback = (_, state, pressArgs) =>
                OnColumnDragStarted(capturedIndex, state, pressArgs);

            _columns[i] = panel;
            Children.Add(panel);
        }

        PointerMoved += OnWeekGridPointerMoved;
        PointerReleased += OnWeekGridPointerReleased;
        PointerCaptureLost += OnCaptureLost;
        _pointerHandlersAttached = true;
    }

    // ── Drag-Koordination ────────────────────────────────────────────────

    private void OnColumnDragStarted(int colIndex, CalendarEventDragState state, PointerPressedEventArgs pressArgs)
    {
        _activeDrag = state;
        _currentDragColumn = colIndex;

        pressArgs.Pointer.Capture(this);
    }

    private void OnWeekGridPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_activeDrag is null) return;

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

        // Spaltenwechsel (Tag wechseln) ist nur für Move ein gültiger Vorgang.
        // Resize verändert nur Start- oder Endzeit innerhalb derselben Spalte;
        // ein Spaltenwechsel während Resize wird ignoriert, damit die Y-basierte
        // Resize-Berechnung in CalendarDayPanel nicht durch einen Tageswechsel
        // verfälscht wird.
        if (newColIdx != _currentDragColumn && _activeDrag.Mode == DragMode.Move)
        {
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

        var activeColIdx = _activeDrag.Mode == DragMode.Move ? newColIdx : _currentDragColumn;
        var colLeft = GetColumnLeft(activeColIdx);
        var posInPanel = new Point(posInGrid.X - colLeft, posInGrid.Y);
        _columns[activeColIdx].UpdateExternalDrag(_activeDrag, posInPanel);

        e.Handled = true;
    }

    private void OnWeekGridPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_activeDrag is null) return;

        var state = _activeDrag;

        // Spaltenwechsel beim Drop nur für Move neu berechnen. Bei ResizeTop/
        // ResizeBottom hat CalendarDayPanel.UpdateDragPreview bereits die
        // korrekten PreviewStartUtc/PreviewEndUtc-Werte gesetzt (Start ODER Ende
        // verändert, je nach Modus) – das hier erneut zu überschreiben hat bisher
        // bei ResizeTop die Dauer fälschlich auf die Originaldauer zurückgesetzt
        // und bei ResizeBottom das Ende komplett auf den Originalwert zurückgesprungen.
        if (state.Mode == DragMode.Move)
        {
            var posInGrid = e.GetPosition(this);
            var dropColIdx = GetColumnAtX(posInGrid.X);

            var days = WeekDays;
            if (days is not null && dropColIdx >= 0 && dropColIdx < days.Count)
            {
                var duration = (state.OriginalEndUtc - state.OriginalStartUtc).TotalMinutes;
                var timeOfDay = state.PreviewStartUtc.ToLocalTime().TimeOfDay;
                var newStartLocal = days[dropColIdx].Date.Date.Add(timeOfDay);
                state.PreviewStartUtc = newStartLocal.ToUniversalTime();
                state.PreviewEndUtc = state.PreviewStartUtc.AddMinutes(duration);
                state.PreviewDayIndex = dropColIdx;
            }
        }

        state.Event.StartUtc = state.PreviewStartUtc;
        state.Event.EndUtc = state.PreviewEndUtc;
        state.Event.DragOpacity = 1.0;

        for (int i = 0; i < _columns.Length; i++)
            _columns[i]?.ReceiveDragLeave();

        _activeDrag = null;
        _currentDragColumn = -1;

        if (OnDropCompleted is not null)
            _ = OnDropCompleted(state);

        e.Handled = true;
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        => CancelActiveDrag();

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