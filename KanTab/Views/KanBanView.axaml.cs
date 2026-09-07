using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KanTab.Models;
using KanTab.ViewModels;

namespace KanTab.Views;

public partial class KanBanView : UserControl
{
    private Point? _dragStartPoint;
    private bool _isDragging;
    private TaskItem? _draggedTask;
    private Border? _highlightedColumn;
    private Border? _originalTaskBorder;
    private const double DragThreshold = 5;

    private readonly DeferredClickHandler<TaskItem> _clickHandler = new(t => t.Id);
    private readonly DispatcherTimer _singleClickTimer;

    public KanBanView()
    {
        InitializeComponent();
        _singleClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DeferredClickHandler<TaskItem>.DefaultSingleClickDelayMs) };
        _singleClickTimer.Tick += (_, _) => OnSingleClickTimerTick();
    }

    private void OnSingleClickTimerTick()
    {
        _singleClickTimer.Stop();
        var pending = _clickHandler.FlushIfDue(DateTime.UtcNow);
        // If still not due (DispatcherTimer granularity), restart briefly
        if (pending == null && _clickHandler.HasPending)
        {
            _singleClickTimer.Start();
            return;
        }
        if (pending != null)
        {
            // Drag is already disqualified: ResetDragState cleared _dragStartPoint/_draggedTask on release,
            // so _isDragging cannot still be true and no further drag can start from a click.
            if (DataContext is KanBanViewModel vm)
                vm.EditTaskCommand.Execute(pending);
        }
    }

    private void TaskCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not TaskItem task) return;

        // KanBan: prefer DoubleTapped as the single authoritative double-click path
        // for platform compatibility. PointerPressed only sets up drag anchors here;
        // it no longer opens Details (prevents ClickCount==2 vs DoubleTapped duplicate).
        _dragStartPoint = e.GetPosition(border);
        _isDragging = false;
        _draggedTask = task;
        _originalTaskBorder = border;
        // Track press for DeferredClickHandler timing so it can cancel the pending Edit
        // when the subsequent DoubleTapped arrives, but do not open Details here.
        _clickHandler.OnPointerPressed(task, e.ClickCount, DateTime.UtcNow);
    }

    private void TaskCard_DoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not TaskItem task) return;
        // Authoritative Details path — idempotent via KanBanViewModel window tracking.
        _clickHandler.OnDoubleTapped(task, DateTime.UtcNow);
        _singleClickTimer.Stop();
        // Ensure drag never starts from a double-click
        _dragStartPoint = null;
        _isDragging = false;
        _draggedTask = null;
        _originalTaskBorder = null;
        DragPreview.IsVisible = false;
        if (DataContext is KanBanViewModel vm)
            vm.OpenTaskDetails(task);
        e.Handled = true;
    }

    private void TaskCard_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedTask == null) return;
        if (_dragStartPoint == null) return;

        if (!_isDragging)
        {
            var currentPosition = e.GetPosition((Control)sender!);
            var delta = currentPosition - _dragStartPoint.Value;

            if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;

            _isDragging = true;
            _clickHandler.OnDragStarted();
            _singleClickTimer.Stop();
            StartDragPreview(e);
        }

        // Update preview position
        UpdateDragPreviewPosition(e);
    }

    private void StartDragPreview(PointerEventArgs e)
    {
        if (_draggedTask == null) return;

        // Populate preview
        DragPreviewTitle.Text = _draggedTask.Title;
        DragPreviewPriority.Fill = GetPriorityBrush(_draggedTask.Priority);

        if (_draggedTask.Tags.Count > 0)
        {
            DragPreviewTag.IsVisible = true;
            DragPreviewTagText.Text = _draggedTask.Tags[0];
        }
        else
        {
            DragPreviewTag.IsVisible = false;
        }

        if (_draggedTask.DueDate.HasValue)
        {
            DragPreviewDueDate.IsVisible = true;
            DragPreviewDueDate.Text = _draggedTask.DueDateDisplay;
        }
        else
        {
            DragPreviewDueDate.IsVisible = false;
        }

        // Show preview and fade original
        DragPreview.IsVisible = true;
        if (_originalTaskBorder != null)
        {
            _originalTaskBorder.Opacity = 0.4f;
        }

        // Position initial preview
        UpdateDragPreviewPosition(e);
    }

    private void UpdateDragPreviewPosition(PointerEventArgs e)
    {
        var mousePos = e.GetPosition(this);
        // Subtract the grab offset (pointer position within the card at drag start)
        // so the grabbed point stays under the cursor throughout the drag.
        var offset = _dragStartPoint ?? default;
        var previewPos = mousePos - offset;
        DragPreview.Margin = new Thickness(previewPos.X, previewPos.Y, 0, 0);
    }

    private IBrush GetPriorityBrush(Priority priority)
    {
        var color = priority switch
        {
            Priority.High => "#E5534B",
            Priority.Medium => "#E5A04B",
            _ => "#6A6A6A"
        };
        return new SolidColorBrush(Color.Parse(color));
    }

    private void TaskCard_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedTask == null) return;

        if (_isDragging)
        {
            // Drop: never open Edit/Details
            _clickHandler.CancelPending();
            _singleClickTimer.Stop();
            var column = FindColumnUnderPointer(e);
            if (column != null && DataContext is KanBanViewModel vm)
                vm.MoveTaskCommand.Execute((_draggedTask, column.Id));
            ResetDragState(disableTimerRestart: true);
            return;
        }

        // Single-click is deferred until double-click window expires so a double-click never opens Edit.
        _clickHandler.ScheduleSingleClick(_draggedTask, DateTime.UtcNow);
        _singleClickTimer.Stop();
        _singleClickTimer.Start();
        // IMPORTANT: clear pointer/drag state immediately so a subsequent PointerMoved
        // (or the timer delay window) cannot spuriously start a drag from a plain click.
        // The pending Edit is kept in _clickHandler, not in the drag fields.
        ResetDragState(disableTimerRestart: false);
    }

    private KanBanColumn? FindColumnUnderPointer(PointerEventArgs e)
    {
        var mousePos = e.GetPosition(this);

        foreach (var border in this.GetVisualDescendants().OfType<Border>())
        {
            if (border.DataContext is not KanBanColumn) continue;

            var transform = border.TransformToVisual(this);
            if (!transform.HasValue) continue;

            var bounds = new Rect(border.Bounds.Size).TransformToAABB(transform.Value);
            if (bounds.Contains(mousePos))
            {
                return border.DataContext as KanBanColumn;
            }
        }

        return null;
    }

    private void ResetDragState(bool disableTimerRestart = false)
    {
        DragPreview.IsVisible = false;
        if (_originalTaskBorder != null)
        {
            _originalTaskBorder.Opacity = 1.0f;
            _originalTaskBorder = null;
        }
        if (_highlightedColumn != null)
        {
            _highlightedColumn.BorderBrush = new SolidColorBrush(Color.Parse("#3C3C3C"));
            _highlightedColumn.BorderThickness = new Thickness(1);
            _highlightedColumn = null;
        }
        _dragStartPoint = null;
        _isDragging = false;
        _draggedTask = null;
        if (disableTimerRestart)
            _singleClickTimer.Stop();
        // When called from PointerReleased for a plain click, the timer must keep running.
        // No-op here: caller controls whether the timer continues.
    }

    // ---- Test helpers (no UI dependency) ----
    public static bool IsDoubleClickByTime(DateTime lastDownUtc, DateTime nowUtc, TaskItem? lastTask, TaskItem? currentTask, double windowMs = DeferredClickHandler<TaskItem>.DefaultDoubleClickMs)
        => DeferredClickHandler<TaskItem>.IsDoubleClickByTimeStatic(lastDownUtc, nowUtc, lastTask, currentTask, t => t.Id, windowMs);
}
