using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using ZeMail.UI.Models;

namespace ZeMail.UI.Controls;

/// <summary>
/// Custom Panel das CalendarEventViewModels absolut positioniert.
/// 1px = 1 Minute, Höhe = 1440px (24h).
/// Unterstützt später Drag & Drop und Resize.
/// </summary>
public class CalendarDayPanel : Panel
{
    public static readonly StyledProperty<IEnumerable?> EventsProperty =
        AvaloniaProperty.Register<CalendarDayPanel, IEnumerable?>(nameof(Events));

    public static readonly StyledProperty<IDataTemplate?> EventTemplateProperty =
        AvaloniaProperty.Register<CalendarDayPanel, IDataTemplate?>(nameof(EventTemplate));

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

    static CalendarDayPanel()
    {
        EventsProperty.Changed.AddClassHandler<CalendarDayPanel>((p, e) => p.OnEventsChanged(e));
        EventTemplateProperty.Changed.AddClassHandler<CalendarDayPanel>((p, _) => p.RebuildChildren());
        AffectsMeasure<CalendarDayPanel>(EventsProperty, EventTemplateProperty);
    }

    private void OnEventsChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyCollectionChanged oldCol)
            oldCol.CollectionChanged -= OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newCol)
            newCol.CollectionChanged += OnCollectionChanged;
        RebuildChildren();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RebuildChildren();

    private void RebuildChildren()
    {
        Children.Clear();
        if (Events is null || EventTemplate is null) return;

        foreach (var item in Events)
        {
            if (item is not CalendarEventViewModel ev) continue;
            if (!EventTemplate.Match(ev)) continue;
            if (EventTemplate.Build(ev) is not Control child) continue;
            child.DataContext = ev;
            Children.Add(child);
        }

        InvalidateArrange();
    }

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
            if (child.DataContext is not CalendarEventViewModel ev) continue;

            double left   = ev.LeftFraction  * panelWidth;
            double width  = ev.WidthFraction * panelWidth - 2.0;
            double top    = ev.TopOffset;
            double height = ev.EventHeight;

            width  = Math.Max(width,  20);
            height = Math.Max(height, 20);

            child.Arrange(new Rect(left, top, width, height));
        }

        return new Size(panelWidth, 1440);
    }
}