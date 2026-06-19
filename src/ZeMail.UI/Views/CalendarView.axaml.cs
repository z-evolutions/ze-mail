using Avalonia.Controls;
using ZeMail.UI.ViewModels;

namespace ZeMail.UI.Views;

public partial class CalendarView : UserControl
{
    public CalendarView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is not CalendarViewModel vm) return;

        DayPanel.OnDropCompleted  = vm.DropCompletedHandler;
        DayPanel.EditRequested    = vm.EditEventCommand.Execute;

        WeekGrid.OnDropCompleted  = vm.DropCompletedHandler;
        WeekGrid.EditRequested    = ev => vm.EditEventCommand.Execute(ev);
    }
}