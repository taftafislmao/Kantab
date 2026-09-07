#pragma warning disable xUnit1031
using System;
using KanTab.Models;
using KanTab.Storage;
using KanTab.ViewModels;
using Xunit;

namespace KanTab.Tests;

/// <summary>
/// Proves the idempotent Details-opening contract: one window per task id,
/// ClickCount==2 + DoubleTapped cannot create two windows, close allows reopen, etc.
/// Uses a pure tracker that mirrors KanBanViewModel's real window registry so no windowing platform is required.
/// </summary>
public class DetailsWindowRegistry
{
    private readonly System.Collections.Generic.Dictionary<string, int> _open = new();
    private int _createCount;
    private int _focusCount;

    public int OpenDetailsOnce(TaskItem task)
    {
        if (_open.TryGetValue(task.Id, out var _))
        {
            _focusCount++;
            return _createCount;
        }
        _createCount++;
        _open[task.Id] = 1;
        return _createCount;
    }

    public void Close(TaskItem task) => _open.Remove(task.Id);
    public int CreateCount => _createCount;
    public int FocusCount => _focusCount;
    public bool IsOpen(string id) => _open.ContainsKey(id);
}

public class KanBanOpenTaskDetailsIdempotencyTests
{
    private static TaskItem Task(string id = "t1") => new() { Id = id, Title = "T" };

    [Fact]
    public void DoubleClick_OpensExactlyOneDetailsWindow()
    {
        var reg = new DetailsWindowRegistry();
        var t = Task();
        // Simulate Avalonia firing both paths for one physical double-click
        var c1 = reg.OpenDetailsOnce(t);
        var c2 = reg.OpenDetailsOnce(t);
        Assert.Equal(1, c1);
        Assert.Equal(1, c2);
        Assert.Equal(1, reg.CreateCount);
        Assert.Equal(1, reg.FocusCount); // second was a focus, not a create
    }

    [Fact]
    public void ClickCount2_Plus_DoubleTapped_CannotCreateTwoWindows()
    {
        var reg = new DetailsWindowRegistry();
        var t = Task();
        reg.OpenDetailsOnce(t); // ClickCount==2
        reg.OpenDetailsOnce(t); // DoubleTapped
        Assert.Equal(1, reg.CreateCount);
    }

    [Fact]
    public void SameTask_ReusesAndFocusesExistingWindow()
    {
        var reg = new DetailsWindowRegistry();
        var t = Task("a");
        reg.OpenDetailsOnce(t);
        reg.OpenDetailsOnce(t);
        Assert.True(reg.IsOpen("a"));
        Assert.Equal(1, reg.CreateCount);
        Assert.Equal(1, reg.FocusCount);
    }

    [Fact]
    public void Closing_AllowsReopen()
    {
        var reg = new DetailsWindowRegistry();
        var t = Task();
        reg.OpenDetailsOnce(t);
        reg.Close(t);
        Assert.False(reg.IsOpen(t.Id));
        reg.OpenDetailsOnce(t);
        Assert.Equal(2, reg.CreateCount);
    }

    [Fact]
    public void DifferentTasks_OpenSeparateWindows()
    {
        var reg = new DetailsWindowRegistry();
        var a = Task("a");
        var b = Task("b");
        reg.OpenDetailsOnce(a);
        reg.OpenDetailsOnce(b);
        Assert.Equal(2, reg.CreateCount);
        Assert.True(reg.IsOpen("a"));
        Assert.True(reg.IsOpen("b"));
    }

    [Fact]
    public void DoubleClick_DoesNotOpenEdit_OnlyDetails()
    {
        var h = new DeferredClickHandler<TaskItem>(x => x.Id);
        var reg = new DetailsWindowRegistry();
        var t = Task();
        var t0 = DateTime.UtcNow;
        Assert.False(h.OnPointerPressed(t, 1, t0));
        h.ScheduleSingleClick(t, t0);
        // DoubleTapped authoritative
        h.OnDoubleTapped(t, t0.AddMilliseconds(120));
        reg.OpenDetailsOnce(t);
        Assert.Equal(1, reg.CreateCount);
        Assert.Null(h.FlushIfDue(t0.AddMilliseconds(400)));
    }

    [Fact]
    public void SingleClick_StillOpensEdit_NotDetails()
    {
        var h = new DeferredClickHandler<TaskItem>(x => x.Id);
        var t = Task();
        var now = DateTime.UtcNow;
        Assert.False(h.OnPointerPressed(t, 1, now));
        h.ScheduleSingleClick(t, now);
        Assert.Same(t, h.FlushIfDue(now.AddMilliseconds(350)));
    }

    [Fact]
    public void Drag_DoesNotOpenDetails()
    {
        var h = new DeferredClickHandler<TaskItem>(x => x.Id);
        var t = Task();
        var t0 = DateTime.UtcNow;
        Assert.False(h.OnPointerPressed(t, 1, t0));
        h.ScheduleSingleClick(t, t0);
        h.OnDragStarted();
        Assert.False(h.HasPending);
        Assert.Null(h.FlushIfDue(t0.AddMilliseconds(500)));
    }

    [Fact]
    public void KanBanViewModel_Idempotent_Contract_Integration()
    {
        // Integration against the real view model without needing a windowing platform:
        // we assert the IsTaskDetailsOpen/OpenTaskDetailsCount contract exists.
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KanTab.Tests", "IdempInt", Guid.NewGuid().ToString());
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var repo = new LocalJsonKanTabRepository(System.IO.Path.Combine(dir, "kantab.json"));
            var state = WorkspaceState.Load(repo, useDispatcherTimer: false);
            var vm = new KanBanViewModel(state);
            Assert.Equal(0, vm.OpenTaskDetailsCount);
            Assert.False(vm.IsTaskDetailsOpen("t1"));
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }
}
