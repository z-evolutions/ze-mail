using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using ZeMail.UI.Models;

namespace ZeMail.UI.Controls;

public class CalendarDayPanel : Panel
{
    // ── Debug-Logging (temporär) ─────────────────────────────────────────
    internal static void Log(string msg)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "zemail_debug.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
        }
        catch { }
    }

    // ── Styled Properties ────────────────────────────────────────────────

    public static readonly StyledProperty<IEnumerable?> EventsProperty =
        AvaloniaProperty.Register<CalendarDayPanel, IEnumerable?>(nameof(Events));

    public static readonly StyledProperty<IDataTemplate?> EventTemplateProperty =
        AvaloniaProperty.Register<CalendarDayPanel, IDataTemplate?>(nameof(EventTemplate));

    public static readonly StyledProperty<DateTime> ColumnDateProperty =
        AvaloniaProperty.Register<CalendarDayPanel, DateTime>(nameof(ColumnDate));

    public static readonly StyledProperty<int> ColumnIndexProperty =
        AvaloniaProperty.Register<CalendarDayPanel, int>(nameof(ColumnIndex));

    public static readonly StyledProperty<CalendarEventDragState?> DragStateProperty =
        AvaloniaProperty.Register<CalendarDayPanel, CalendarEventDragState?>(nameof(DragState));

    public IEnumerable? Events
    {
        get => GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    public IDataTemplate? EventTemplate
    {
        get => GetValue(EventTemplateProperty);
        set => SetValue(EventTemplateProperty, value);
    }

    public DateTime ColumnDate
    {
        get => GetValue(ColumnDateProperty);
        set => SetValue(ColumnDateProperty, value);
    }

    public int ColumnIndex
    {
        get => GetValue(ColumnIndexProperty);
        set => SetValue(ColumnIndexProperty, value);
    }

    public CalendarEventDragState? DragState
    {
        get => GetValue(DragStateProperty);
        set => SetValue(DragStateProperty, value);
    }

    // ── Code-Behind-Properties ───────────────────────────────────────────
    public Func<CalendarEventDragState, Task>? OnDropCompleted { get; set; }
    public Action<CalendarEventViewModel>? EditRequested { get; set; }

    // FIX: Callback bekommt jetzt auch die PointerPressedEventArgs mit,
    // damit das WeekGrid das Pointer-Capture direkt setzen kann
    // (Bubbling von PointerPressed zum Parent findet wegen e.Handled=true nicht statt)
    public Action<CalendarDayPanel, CalendarEventDragState, PointerPressedEventArgs>? DragStartedCallback { get; set; }

    // ── Control-Cache ─────────────────────────────────────────────────────
    private readonly Dictionary<Guid, Control> _controlCache = new();

    // ── Interne Felder ───────────────────────────────────────────────────
    private bool _isLocalDragOwner;
    private Border? _ghostOverlay;
    private Point _pressPosition;
    private bool _isDragging;

    private const double TapThreshold = 5.0;
    private const double ResizeHandleSize = 8.0;
    private const double SnapMinutes = 15.0;

    // ── Statische Initialisierung ────────────────────────────────────────

    static CalendarDayPanel()
    {
        EventsProperty.Changed.AddClassHandler<CalendarDayPanel>((p, e) => p.OnEventsChanged(e));
        EventTemplateProperty.Changed.AddClassHandler<CalendarDayPanel>((p, _) => p.FullRebuild());
        AffectsMeasure<CalendarDayPanel>(EventsProperty, EventTemplateProperty);
    }

    // ── Collection-Handling ──────────────────────────────────────────────

    private void OnEventsChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyCollectionChanged oldCol)
            oldCol.CollectionChanged -= OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newCol)
            newCol.CollectionChanged += OnCollectionChanged;

        SyncChildren();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => SyncChildren();

    private void FullRebuild()
    {
        _controlCache.Clear();
        RebuildFromCache();
    }

    private void SyncChildren()
    {
        if (EventTemplate is null)
        {
            _controlCache.Clear();
            Children.Clear();
            if (_ghostOverlay is not null) Children.Add(_ghostOverlay);
            return;
        }

        var currentIds = new HashSet<Guid>();

        if (Events is not null)
        {
            foreach (var item in Events)
            {
                if (item is not CalendarEventViewModel ev) continue;
                currentIds.Add(ev.Id);

                if (!_controlCache.ContainsKey(ev.Id))
                {
                    if (!EventTemplate.Match(ev)) continue;
                    if (EventTemplate.Build(ev) is not Control child) continue;

                    child.DataContext = ev;
                    AttachPointerHandlers(child, ev);
                    _controlCache[ev.Id] = child;
                    Log($"[SyncChildren] Neues Control erzeugt für '{ev.Title}' (Col={ColumnIndex})");
                }
            }
        }

        var toRemove = new List<Guid>();
        foreach (var id in _controlCache.Keys)
            if (!currentIds.Contains(id))
                toRemove.Add(id);
        foreach (var id in toRemove)
            _controlCache.Remove(id);

        RebuildFromCache();
    }

    private void RebuildFromCache()
    {
        Children.Clear();

        if (Events is not null)
        {
            foreach (var item in Events)
            {
                if (item is not CalendarEventViewModel ev) continue;
                if (_controlCache.TryGetValue(ev.Id, out var child))
                    Children.Add(child);
            }
        }

        EnsureGhostOverlay();
        Children.Add(_ghostOverlay!);

        InvalidateArrange();
    }

    // ── Pointer-Handler ──────────────────────────────────────────────────

    private void AttachPointerHandlers(Control child, CalendarEventViewModel ev)
    {
        child.PointerPressed += (_, e) => OnEventPointerPressed(child, ev, e);
        child.PointerMoved += (_, e) => OnEventPointerMoved(ev, e);
        child.PointerReleased += (_, e) => OnEventPointerReleased(ev, e);
        child.PointerCaptureLost += (_, _) => CancelDrag();
        Log($"[AttachPointerHandlers] Handler registriert für '{ev.Title}' (Col={ColumnIndex})");
    }

    private void OnEventPointerPressed(Control child, CalendarEventViewModel ev, PointerPressedEventArgs e)
    {
        Log($"[Pressed] '{ev.Title}' Col={ColumnIndex} IsLeftButton={e.GetCurrentPoint(child).Properties.IsLeftButtonPressed}");

        if (!e.GetCurrentPoint(child).Properties.IsLeftButtonPressed) return;

        _pressPosition = e.GetPosition(this);
        _isDragging = false;

        if (ev.IsAllDay)
        {
            e.Pointer.Capture(child);
            e.Handled = true;
            return;
        }

        var posInChild = e.GetPosition(child);

        DragMode mode;
        if (posInChild.Y <= ResizeHandleSize)
            mode = DragMode.ResizeTop;
        else if (posInChild.Y >= child.Bounds.Height - ResizeHandleSize)
            mode = DragMode.ResizeBottom;
        else
            mode = DragMode.Move;

        Log($"[Pressed] Mode={mode} posInChild.Y={posInChild.Y:F1} childHeight={child.Bounds.Height:F1}");

        var state = new CalendarEventDragState
        {
            Event = ev,
            OriginalStartUtc = ev.StartUtc,
            OriginalEndUtc = ev.EndUtc,
            OriginalDayIndex = ColumnIndex,
            Mode = mode,
            GrabOffsetY = posInChild.Y,
            PreviewStartUtc = ev.StartUtc,
            PreviewEndUtc = ev.EndUtc,
            PreviewDayIndex = ColumnIndex,
            GhostTop = ev.TopOffset,
            GhostHeight = ev.EventHeight,
            GhostLeft = ev.LeftFraction * Bounds.Width,
            GhostWidth = ev.WidthFraction * Bounds.Width - 2,
            GhostDayIndex = ColumnIndex,
        };

        _isLocalDragOwner = true;
        DragState = state;

        if (DragStartedCallback is not null)
        {
            Log("[Pressed] DragStartedCallback wird aufgerufen (WeekGrid-Modus)");
            // FIX: PointerPressedEventArgs direkt mitgeben statt auf Bubbling zu warten
            DragStartedCallback.Invoke(this, state, e);
        }
        else
        {
            Log("[Pressed] Kein DragStartedCallback -> lokales Capture (Day-Modus)");
            e.Pointer.Capture(child);
        }

        e.Handled = true;
    }

    private void OnEventPointerMoved(CalendarEventViewModel ev, PointerEventArgs e)
    {
        var state = DragState;
        if (state is null || state.Event.Id != ev.Id) return;
        if (!_isLocalDragOwner) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Log("[Moved] LeftButton nicht mehr gedrückt -> CancelDrag");
            CancelDrag();
            return;
        }

        var posInPanel = e.GetPosition(this);

        if (!_isDragging)
        {
            var delta = posInPanel - _pressPosition;
            if (Math.Abs(delta.X) < TapThreshold && Math.Abs(delta.Y) < TapThreshold)
                return;

            _isDragging = true;
            ev.DragOpacity = 0.35;
            Log($"[Moved] Drag gestartet! delta=({delta.X:F1},{delta.Y:F1})");
        }

        UpdateDragPreview(state, posInPanel);
        UpdateGhostVisibility();
        e.Handled = true;
    }

    private void OnEventPointerReleased(CalendarEventViewModel ev, PointerReleasedEventArgs e)
    {
        Log($"[Released] '{ev.Title}' _isDragging={_isDragging} _isLocalDragOwner={_isLocalDragOwner}");

        var state = DragState;

        if (!_isDragging)
        {
            DragState = null;
            _isLocalDragOwner = false;
            EditRequested?.Invoke(ev);
            e.Handled = true;
            return;
        }

        if (state is null || state.Event.Id != ev.Id)
        {
            Log("[Released] state ist null oder Event-Id stimmt nicht -> Abbruch ohne Commit");
            return;
        }
        if (!_isLocalDragOwner)
        {
            Log("[Released] Nicht lokaler Drag-Owner -> kein Commit hier (WeekGrid macht das)");
            return;
        }

        CommitDrop(state);
        e.Handled = true;
    }

    // ── Drag-Logik ───────────────────────────────────────────────────────

    public void UpdateDragPreview(CalendarEventDragState state, Point posInPanel)
    {
        const double panelHeight = 1440.0;
        var panelWidth = Bounds.Width;
        var duration = (state.OriginalEndUtc - state.OriginalStartUtc).TotalMinutes;

        switch (state.Mode)
        {
            case DragMode.Move:
                {
                    var rawMinutes = posInPanel.Y - state.GrabOffsetY;
                    var clamped = Math.Clamp(rawMinutes, 0, panelHeight - duration);
                    var snapped = SnapToGrid(clamped);

                    // Referenzdatum ist das fixe Spaltendatum, nicht der wandernde Preview-Wert
                    var referenceDate = ColumnDate == default ? state.OriginalStartUtc.ToLocalTime() : ColumnDate;
                    state.PreviewStartUtc = CalendarEventDragState.MinutesToSnappedUtc(snapped, referenceDate);
                    state.PreviewEndUtc = state.PreviewStartUtc.AddMinutes(duration);

                    state.GhostTop = snapped;
                    state.GhostHeight = Math.Max(20, duration);
                    state.GhostLeft = state.Event.LeftFraction * panelWidth;
                    state.GhostWidth = state.Event.WidthFraction * panelWidth - 2;
                    break;
                }
            case DragMode.ResizeTop:
                {
                    var endMin = state.OriginalEndUtc.ToLocalTime().TimeOfDay.TotalMinutes;
                    var clamped = Math.Clamp(posInPanel.Y, 0, endMin - SnapMinutes);
                    var snapped = SnapToGrid(clamped);

                    state.PreviewStartUtc = CalendarEventDragState.MinutesToSnappedUtc(
                        snapped, state.OriginalStartUtc.ToLocalTime());
                    state.PreviewEndUtc = state.OriginalEndUtc;

                    state.GhostTop = snapped;
                    state.GhostHeight = Math.Max(20, (state.PreviewEndUtc - state.PreviewStartUtc).TotalMinutes);
                    state.GhostLeft = state.Event.LeftFraction * panelWidth;
                    state.GhostWidth = state.Event.WidthFraction * panelWidth - 2;
                    break;
                }
            case DragMode.ResizeBottom:
                {
                    var startMin = state.PreviewStartUtc.ToLocalTime().TimeOfDay.TotalMinutes;
                    var clamped = Math.Clamp(posInPanel.Y, startMin + SnapMinutes, panelHeight);
                    var snapped = SnapToGrid(clamped);

                    state.PreviewStartUtc = state.OriginalStartUtc;
                    state.PreviewEndUtc = CalendarEventDragState.MinutesToSnappedUtc(
                        snapped, state.OriginalEndUtc.ToLocalTime());

                    state.GhostTop = state.Event.TopOffset;
                    state.GhostHeight = Math.Max(20, snapped - startMin);
                    state.GhostLeft = state.Event.LeftFraction * panelWidth;
                    state.GhostWidth = state.Event.WidthFraction * panelWidth - 2;
                    break;
                }
        }
    }

    private void CommitDrop(CalendarEventDragState state)
    {
        Log($"[CommitDrop] '{state.Event.Title}' Preview={state.PreviewStartUtc:yyyy-MM-dd HH:mm}-{state.PreviewEndUtc:HH:mm} Original={state.OriginalStartUtc:yyyy-MM-dd HH:mm}-{state.OriginalEndUtc:HH:mm}");

        HideGhost();
        state.Event.DragOpacity = 1.0;
        state.Event.StartUtc = state.PreviewStartUtc;
        state.Event.EndUtc = state.PreviewEndUtc;

        DragState = null;
        _isLocalDragOwner = false;
        _isDragging = false;

        Log($"[CommitDrop] OnDropCompleted is null: {OnDropCompleted is null}");
        if (OnDropCompleted is not null)
        {
            Log("[CommitDrop] Rufe OnDropCompleted auf...");
            _ = OnDropCompleted(state);
        }
    }

    public void CancelDrag()
    {
        var state = DragState;
        if (state is not null)
        {
            Log($"[CancelDrag] '{state.Event.Title}' wird zurückgesetzt");
            HideGhost();
            state.Event.StartUtc = state.OriginalStartUtc;
            state.Event.EndUtc = state.OriginalEndUtc;
            state.Event.DragOpacity = 1.0;
        }

        DragState = null;
        _isLocalDragOwner = false;
        _isDragging = false;
    }

    // ── WeekGrid-API ─────────────────────────────────────────────────────

    public void TakeOverDrag(CalendarEventDragState state)
    {
        Log($"[TakeOverDrag] Col={ColumnIndex} übernimmt '{state.Event.Title}'");
        _isLocalDragOwner = false;
        DragState = state;
        state.GhostDayIndex = ColumnIndex;
        UpdateGhostVisibility();
        InvalidateArrange();
    }

    public void ReceiveDragLeave()
    {
        HideGhost();
        DragState = null;
    }

    public void UpdateExternalDrag(CalendarEventDragState state, Point posInPanel)
    {
        DragState = state;
        UpdateDragPreview(state, posInPanel);
        UpdateGhostVisibility();
        InvalidateArrange();
    }

    // ── Ghost-Overlay ────────────────────────────────────────────────────

    private void EnsureGhostOverlay()
    {
        if (_ghostOverlay is not null) return;

        _ghostOverlay = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#885AC8FA")),
            BorderBrush = new SolidColorBrush(Color.Parse("#5AC8FA")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            IsHitTestVisible = false,
            IsVisible = false,
            ZIndex = 1000,
        };
    }

    private void UpdateGhostVisibility()
    {
        if (_ghostOverlay is null || DragState is null) return;
        bool showHere = DragState.GhostDayIndex == ColumnIndex;
        _ghostOverlay.IsVisible = showHere;
        if (showHere) InvalidateArrange();
    }

    private void HideGhost()
    {
        if (_ghostOverlay is not null)
            _ghostOverlay.IsVisible = false;
    }

    // ── Measure / Arrange ────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (Control child in Children)
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return new Size(availableSize.Width, 1440);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double panelWidth = finalSize.Width;

        foreach (Control child in Children)
        {
            if (child == _ghostOverlay)
            {
                var state = DragState;
                if (state is not null && _ghostOverlay!.IsVisible)
                    _ghostOverlay.Arrange(new Rect(
                        state.GhostLeft, state.GhostTop,
                        Math.Max(20, state.GhostWidth),
                        Math.Max(20, state.GhostHeight)));
                else
                    _ghostOverlay?.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            if (child.DataContext is not CalendarEventViewModel ev) continue;

            child.Arrange(new Rect(
                ev.LeftFraction * panelWidth,
                ev.TopOffset,
                Math.Max(ev.WidthFraction * panelWidth - 2.0, 20),
                Math.Max(ev.EventHeight, 20)));
        }

        return new Size(panelWidth, 1440);
    }

    private static double SnapToGrid(double minutes)
        => Math.Round(minutes / SnapMinutes) * SnapMinutes;
}