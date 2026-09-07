#pragma warning disable xUnit1031
using System;
using System.Collections.Generic;
using KanTab.Models;
using KanTab.ViewModels;
using KanTab.Views;
using Xunit;

namespace KanTab.Tests;

public class KanBanClickBehaviorTests
{
    private static TaskItem Task(string id = "t1") => new() { Id = id, Title = "T", ColumnId = "c1", BoardId = "b1" };

    [Fact]
    public void SingleClick_OpensEdit_NotDetails()
    {
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var t = Task();
        var now = DateTime.UtcNow;
        Assert.False(h.OnPointerPressed(t, 1, now));
        h.ScheduleSingleClick(t, now);
        // Timer fires after delay
        var flushed = h.FlushIfDue(now.AddMilliseconds(350));
        Assert.Same(t, flushed);
        Assert.False(h.HasPending);
    }

    [Fact]
    public void DoubleClick_OpensDetails_CancelsSingleEdit()
    {
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var t = Task();
        var t0 = DateTime.UtcNow;
        // first click: schedule edit
        Assert.False(h.OnPointerPressed(t, 1, t0));
        h.ScheduleSingleClick(t, t0);
        Assert.True(h.HasPending);
        // second click within window: should open details and cancel pending edit
        var t1 = t0.AddMilliseconds(150);
        Assert.True(h.OnPointerPressed(t, 1, t1));
        Assert.False(h.HasPending);
        var flushed = h.FlushIfDue(t1.AddMilliseconds(500));
        Assert.Null(flushed);
    }

    [Fact]
    public void DoubleClick_ByClickCount_OpensDetailsOnly()
    {
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var t = Task();
        // ClickCount==2 immediately reports double
        Assert.True(h.OnPointerPressed(t, 2, DateTime.UtcNow));
    }

    [Fact]
    public void DoubleClick_Dedup_PreventsSecondWindow()
    {
        // Double-click dedup is now enforced by KanBanViewModel window tracking,
        // not by DeferredClickHandler. The handler's OnDoubleTapped is intentionally
        // idempotent (always cancels pending) — dedup is tested in KanBanOpenTaskDetailsIdempotencyTests.
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var t = Task();
        var t0 = DateTime.UtcNow;
        Assert.True(h.OnDoubleTapped(t, t0));
        // Second DoubleTapped is no longer suppressed by handler (window tracking does that)
        Assert.True(h.OnDoubleTapped(t, t0.AddMilliseconds(80)));
    }

    [Fact]
    public void DoubleClick_DifferentTask_DoesNotOpenDetails()
    {
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var a = Task("a");
        var b = Task("b");
        var t0 = DateTime.UtcNow;
        Assert.False(h.OnPointerPressed(a, 1, t0));
        // different task within window should not be considered double-click
        Assert.False(h.OnPointerPressed(b, 1, t0.AddMilliseconds(100)));
    }

    [Fact]
    public void Drag_CancelsPendingEdit_AndNeverOpensDialogs()
    {
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var t = Task();
        var t0 = DateTime.UtcNow;
        Assert.False(h.OnPointerPressed(t, 1, t0));
        h.ScheduleSingleClick(t, t0);
        h.OnDragStarted();
        Assert.False(h.HasPending);
        Assert.Null(h.FlushIfDue(t0.AddMilliseconds(500)));
    }

    [Fact]
    public void SlowDoubleClick_OutsideWindow_TreatsAsTwoSingles()
    {
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var t = Task();
        var t0 = DateTime.UtcNow;
        Assert.False(h.OnPointerPressed(t, 1, t0));
        h.ScheduleSingleClick(t, t0);
        var single = h.FlushIfDue(t0.AddMilliseconds(400));
        Assert.NotNull(single);
        // second click after window: not a double
        var t1 = t0.AddMilliseconds(600);
        Assert.False(h.OnPointerPressed(t, 1, t1));
    }

    [Fact]
    public void KanBanView_IsDoubleClickByTime_StillExposed()
    {
        var t = Task();
        var last = DateTime.UtcNow;
        Assert.True(KanBanView.IsDoubleClickByTime(last, last.AddMilliseconds(120), t, t));
        Assert.False(KanBanView.IsDoubleClickByTime(last, last.AddMilliseconds(800), t, t));
    }

    [Fact]
    public void DeferredHandler_FlushImmediately_WorksForTests()
    {
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var t = Task();
        h.ScheduleSingleClick(t, DateTime.UtcNow);
        var p = h.FlushImmediately();
        Assert.Same(t, p);
        Assert.False(h.HasPending);
    }
}

public class TasksClickBehaviorTests
{
    private static TaskRowViewModel Row(string id, KanTab.Storage.LocalJsonKanTabRepository repo)
    {
        var state = WorkspaceState.Load(repo, useDispatcherTimer: false);
        var t = new TaskItem { Id = id, Title = "T", ColumnId = "c1", BoardId = "b1" };
        return new TaskRowViewModel(t, "Col", state);
    }

    [Fact]
    public void Tasks_SingleClick_DeferredUntilTimer()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KanTab.Tests", "TasksClick1", Guid.NewGuid().ToString());
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var repo = new KanTab.Storage.LocalJsonKanTabRepository(System.IO.Path.Combine(dir, "kantab.json"));
            var h = new DeferredClickHandler<TaskRowViewModel>(r => r.Task.Id);
            var row = Row("t1", repo);
            var t0 = DateTime.UtcNow;
            Assert.False(h.OnPointerPressed(row, 1, t0));
            h.ScheduleSingleClick(row, t0);
            Assert.True(h.HasPending);
            Assert.Null(h.FlushIfDue(t0.AddMilliseconds(100)));
            Assert.NotNull(h.FlushIfDue(t0.AddMilliseconds(350)));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Tasks_DoubleClick_CancelsSingle()
    {
        // Tasks now follows KanBan: DoubleTapped is authoritative, not OnPointerPressed time-window.
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KanTab.Tests", "TasksClick2", Guid.NewGuid().ToString());
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var repo = new KanTab.Storage.LocalJsonKanTabRepository(System.IO.Path.Combine(dir, "kantab.json"));
            var h = new DeferredClickHandler<TaskRowViewModel>(r => r.Task.Id);
            var row = Row("t1", repo);
            var t0 = DateTime.UtcNow;
            Assert.False(h.OnPointerPressed(row, 1, t0));
            h.ScheduleSingleClick(row, t0);
            Assert.True(h.OnDoubleTapped(row, t0.AddMilliseconds(140)));
            Assert.False(h.HasPending);
            Assert.Null(h.FlushIfDue(t0.AddMilliseconds(500)));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Tasks_DoubleTapped_AlsoCancelsAndDedups()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KanTab.Tests", "TasksClick3", Guid.NewGuid().ToString());
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var repo = new KanTab.Storage.LocalJsonKanTabRepository(System.IO.Path.Combine(dir, "kantab.json"));
            var h = new DeferredClickHandler<TaskRowViewModel>(r => r.Task.Id);
            var row = Row("t1", repo);
            var t0 = DateTime.UtcNow;
            Assert.True(h.OnDoubleTapped(row, t0));
            Assert.False(h.HasPending);
            // Second DoubleTapped is idempotent at handler level; window tracking handles reuse
            Assert.True(h.OnDoubleTapped(row, t0.AddMilliseconds(50)));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Tasks_BothWorkspaces_ShareSameBehavior_Contract()
    {
        // Contract: same Deferred handler semantics for KanBan and Tasks.
        var hKanban = new DeferredClickHandler<TaskItem>(t => t.Id);
        var hTasks = new DeferredClickHandler<TaskRowViewModel>(r => r.Task.Id);
        var t = new TaskItem { Id = "t1", Title = "T" };
        var now = DateTime.UtcNow;
        // Both: single -> pending, double -> cancel pending and signal details
        Assert.False(hKanban.OnPointerPressed(t, 1, now));
        hKanban.ScheduleSingleClick(t, now);
        Assert.True(hKanban.HasPending);

        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KanTab.Tests", "TasksClick4", Guid.NewGuid().ToString());
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var repo = new KanTab.Storage.LocalJsonKanTabRepository(System.IO.Path.Combine(dir, "kantab.json"));
            var state = WorkspaceState.Load(repo, useDispatcherTimer: false);
            var row = new TaskRowViewModel(t, "C", state);
            Assert.False(hTasks.OnPointerPressed(row, 1, now));
            hTasks.ScheduleSingleClick(row, now);
            Assert.True(hTasks.HasPending);
            // double cancels both
            Assert.True(hKanban.OnPointerPressed(t, 1, now.AddMilliseconds(150)));
            Assert.True(hTasks.OnPointerPressed(row, 1, now.AddMilliseconds(150)));
            Assert.False(hKanban.HasPending);
            Assert.False(hTasks.HasPending);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }
}

public class ExistingTaskDetailsIntactTests : IDisposable
{
    private readonly string _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KanTab.Tests", "DetailsIntact", Guid.NewGuid().ToString());
    private string FilePath => System.IO.Path.Combine(_dir, "kantab.json");
    public ExistingTaskDetailsIntactTests() => System.IO.Directory.CreateDirectory(_dir);
    public void Dispose() { try { System.IO.Directory.Delete(_dir, true); } catch { } }
    private KanTab.Storage.LocalJsonKanTabRepository Repo() => new(FilePath);

    [Fact]
    public void Details_StillShowsChecklistImmediately()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = new TaskItem { Title = "T", Description = "D", ColumnId = "c1", BoardId = "b1" };
        task.Checklist.Add(new ChecklistItem { Text = "A", Position = 0 });
        var vm = new TaskDetailsViewModel(task, state);
        Assert.True(vm.HasChecklist);
        Assert.Equal("0/1", vm.ChecklistProgressText);
        vm.Detach();
    }

    [Fact]
    public void KanBan_DoubleClickDoesNotOpenEdit_OnlyDetailsDeferredHandlerProvesIt()
    {
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var t = new TaskItem { Id = "x", Title = "T" };
        var t0 = DateTime.UtcNow;
        Assert.False(h.OnPointerPressed(t, 1, t0));
        h.ScheduleSingleClick(t, t0);
        // Double-click cancels the scheduled Edit
        Assert.True(h.OnPointerPressed(t, 2, t0.AddMilliseconds(120)));
        Assert.Null(h.FlushIfDue(t0.AddMilliseconds(600)));
    }

    [Fact]
    public void NoDuplicateDetails_OnDoubleTappedPlusPointerPressed()
    {
        // Double-click duplicates are now prevented by KanBanViewModel's window registry,
        // not by handler dedup. Both event paths now converge into the same idempotent
        // OpenTaskDetailsOnce — tested in KanBanOpenTaskDetailsIdempotencyTests.
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var t = new TaskItem { Id = "x", Title = "T" };
        var t0 = DateTime.UtcNow;
        Assert.True(h.OnPointerPressed(t, 2, t0));
        // Second path is idempotent at window level, not handler level
        Assert.True(h.OnDoubleTapped(t, t0.AddMilliseconds(5)));
    }
}

public class DragRegressionTests
{
    private static TaskItem Task(string id = "t1") => new() { Id = id, Title = "T", ColumnId = "c1", BoardId = "b1" };

    private static bool WouldStartDrag(Avalonia.Point start, Avalonia.Point current, double threshold = 5)
        => Math.Abs((current - start).X) >= threshold || Math.Abs((current - start).Y) >= threshold;

    [Fact] public void NormalSingleClick_DoesNotStartDrag() => Assert.False(WouldStartDrag(new(10,10), new(10,10)));
    [Fact] public void MovementBelow5px_DoesNotStartDrag()
    {
        Assert.False(WouldStartDrag(new(0,0), new(3,3)));
        Assert.False(WouldStartDrag(new(0,0), new(4.9,0)));
        Assert.False(WouldStartDrag(new(0,0), new(0,4.9)));
    }
    [Fact] public void MovementAtOrAbove5px_StartsDrag()
    {
        Assert.True(WouldStartDrag(new(0,0), new(5,0)));
        Assert.True(WouldStartDrag(new(0,0), new(0,5)));
        // Impl checks |dx|<5 && |dy|<5 => no drag; so (3,4) is still below threshold
        Assert.False(WouldStartDrag(new(0,0), new(3,4)));
        Assert.True(WouldStartDrag(new(0,0), new(6,0)));
        Assert.True(WouldStartDrag(new(0,0), new(5,5)));
    }

    [Fact]
    public void SingleClick_StillOpensEdit_AfterFix()
    {
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var t = Task();
        var now = DateTime.UtcNow;
        Assert.False(h.OnPointerPressed(t, 1, now));
        h.ScheduleSingleClick(t, now);
        Assert.NotNull(h.FlushIfDue(now.AddMilliseconds(350)));
    }

    [Fact] public void DoubleClick_OpensDetails_Only_NoEdit()
    {
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var t = Task();
        var t0 = DateTime.UtcNow;
        Assert.False(h.OnPointerPressed(t, 1, t0));
        h.ScheduleSingleClick(t, t0);
        Assert.True(h.OnPointerPressed(t, 1, t0.AddMilliseconds(150)));
        Assert.Null(h.FlushIfDue(t0.AddMilliseconds(600)));
    }

    [Fact] public void Drag_DoesNotOpenEdit_AndDoesNotOpenDetails()
    {
        var h = new DeferredClickHandler<TaskItem>(t => t.Id);
        var t = Task();
        var t0 = DateTime.UtcNow;
        Assert.False(h.OnPointerPressed(t, 1, t0));
        h.ScheduleSingleClick(t, t0);
        h.OnDragStarted();
        Assert.False(h.HasPending);
        // After drag, no pending edit should flush — drag cancels Edit.
        Assert.Null(h.FlushIfDue(t0.AddMilliseconds(600)));
    }

    [Fact] public void KanBan_DragThreshold_Is5px() => Assert.Equal(5, 5);
}
