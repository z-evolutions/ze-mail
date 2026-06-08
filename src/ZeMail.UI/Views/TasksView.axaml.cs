using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ZeMail.UI.ViewModels;

namespace ZeMail.UI.Views;

public partial class TasksView : UserControl
{
    public TasksView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is TasksViewModel vm)
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(TasksViewModel.IsAddingTask) && vm.IsAddingTask)
                        Dispatcher.UIThread.Post(() =>
                            this.FindControl<TextBox>("NewTaskTextBox")?.Focus(),
                            DispatcherPriority.Loaded);
                };
        };

        AddHandler(DragDrop.DropEvent,     OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private async void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not TasksViewModel vm) return;
        if (sender is not TextBlock { DataContext: TaskItemViewModel task }) return;

        vm.DraggedTask = task;

        // Avalonia 12: DataTransferItem + DataTransfer.Add()
        var item = new DataTransferItem();
        item.Set(DataFormat.Text, vm.CurrentTasks.IndexOf(task).ToString());
        var transfer = new DataTransfer();
        transfer.Add(item);

        await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move);

        vm.DraggedTask = null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.Text)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not TasksViewModel vm) return;
        if (!e.DataTransfer.Formats.Contains(DataFormat.Text)) return;

        var pos    = e.GetPosition(this);
        var target = this.GetVisualsAt(pos)
                         .OfType<Border>()
                         .Select(b => b.DataContext)
                         .OfType<TaskItemViewModel>()
                         .FirstOrDefault();

        if (target is not null && vm.DraggedTask is not null && target != vm.DraggedTask)
            vm.MoveTaskCommand.Execute(target);

        e.Handled = true;
    }
}