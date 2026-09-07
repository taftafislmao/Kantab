using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using KanTab.ViewModels;

namespace KanTab.Views;

public partial class TasksView : UserControl
{
    private readonly DeferredClickHandler<TaskRowViewModel> _clickHandler = new(r => r.Task.Id);
    private readonly DispatcherTimer _singleClickTimer;

    public TasksView()
    {
        InitializeComponent();
        _singleClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DeferredClickHandler<TaskRowViewModel>.DefaultSingleClickDelayMs) };
        _singleClickTimer.Tick += (_, _) => OnSingleClickTimerTick();
    }

    private void OnSingleClickTimerTick()
    {
        _singleClickTimer.Stop();
        var pending = _clickHandler.FlushIfDue(DateTime.UtcNow);
        if (pending == null && _clickHandler.HasPending)
        {
            _singleClickTimer.Start();
            return;
        }
        if (pending != null)
        {
            if (DataContext is TasksViewModel vm)
                vm.EditTaskCommand.Execute(pending);
        }
    }

    private void TaskRow_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not TaskRowViewModel row) return;
        // Tasks rows have no drag; track press only for double-click cancellation timing.
        _clickHandler.OnPointerPressed(row, e.ClickCount, DateTime.UtcNow);
    }

    private void TaskRow_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not TaskRowViewModel row) return;
        // Avoid double-scheduling if already pending for this row; otherwise schedule Edit.
        if (_clickHandler.HasPending && ReferenceEquals(_clickHandler.Pending, row))
            return;
        _clickHandler.ScheduleSingleClick(row, DateTime.UtcNow);
        _singleClickTimer.Stop();
        _singleClickTimer.Start();
    }

    private void TaskRow_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not TaskRowViewModel row) return;
        var now = DateTime.UtcNow;
        if (!_clickHandler.OnDoubleTapped(row, now)) { e.Handled = true; return; }
        _singleClickTimer.Stop();
        if (DataContext is TasksViewModel vm)
            vm.OpenTaskDetails(row);
        e.Handled = true;
    }
}
