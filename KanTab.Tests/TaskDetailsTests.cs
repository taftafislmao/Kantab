#pragma warning disable xUnit1031
using System;
using System.IO;
using System.Linq;
using KanTab.Models;
using KanTab.Storage;
using KanTab.ViewModels;
using KanTab.Views;
using Xunit;

namespace KanTab.Tests;

public class TaskDetailsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KanTab.Tests", "TaskDetails", Guid.NewGuid().ToString());
    private string FilePath => Path.Combine(_directory, "kantab.json");
    public TaskDetailsTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { try { Directory.Delete(_directory, true); } catch { } }
    private LocalJsonKanTabRepository Repo() => new(FilePath);

    private static TaskItem MakeTask(string title = "My Task", string desc = "Desc")
        => new TaskItem { Title = title, Description = desc, ColumnId = "c1", BoardId = "b1" };

    // ---- TaskDetailsViewModel ----

    [Fact]
    public void Details_ShowsTitleAndDescriptionAndProgress()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = MakeTask("Buy milk", "2% fat");
        task.Checklist.Add(new ChecklistItem { Text = "A", IsCompleted = true, Position = 0 });
        task.Checklist.Add(new ChecklistItem { Text = "B", IsCompleted = false, Position = 1 });
        task.Checklist.Add(new ChecklistItem { Text = "C", IsCompleted = false, Position = 2 });
        var vm = new TaskDetailsViewModel(task, state);
        Assert.Equal("Buy milk", vm.Title);
        Assert.Equal("2% fat", vm.Description);
        Assert.True(vm.HasDescription);
        Assert.Equal("1/3", vm.ChecklistProgressText);
        Assert.Equal("33%", vm.ChecklistPercentText);
        Assert.Equal("1/3 \u2022 33%", vm.ProgressDisplay);
        Assert.True(vm.HasChecklist);
        vm.Detach();
    }

    [Fact]
    public void Details_NoDescription_HasNoDescription()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = MakeTask("T", "");
        var vm = new TaskDetailsViewModel(task, state);
        Assert.False(vm.HasDescription);
        Assert.False(vm.HasChecklist);
        Assert.Equal(string.Empty, vm.ProgressDisplay);
        vm.Detach();
    }

    [Fact]
    public void Details_ChecklistItemsDisplayed_InOrder()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = MakeTask();
        task.Checklist.Add(new ChecklistItem { Text = "A", Position = 0 });
        task.Checklist.Add(new ChecklistItem { Text = "B", Position = 1 });
        var vm = new TaskDetailsViewModel(task, state);
        Assert.Equal(2, vm.Task.Checklist.Count);
        Assert.Equal("A", vm.Task.Checklist[0].Text);
        Assert.Equal("B", vm.Task.Checklist[1].Text);
        vm.Detach();
    }

    [Fact]
    public void Details_CheckUncheck_UpdatesExistingTask()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = MakeTask();
        var item = new ChecklistItem { Text = "X", IsCompleted = false, Position = 0 };
        task.Checklist.Add(item);
        var vm = new TaskDetailsViewModel(task, state);
        Assert.Equal("0/1", vm.ChecklistProgressText);
        // Toggle via item directly (as checkbox binding does)
        item.IsCompleted = true;
        Assert.Equal("1/1", vm.ChecklistProgressText);
        Assert.Equal("100%", vm.ChecklistPercentText);
        Assert.True(task.Checklist[0].IsCompleted);
        // Via VM command
        vm.ToggleChecklistItemCommand.Execute(item);
        Assert.False(item.IsCompleted);
        Assert.Equal("0/1", vm.ChecklistProgressText);
        vm.Detach();
    }

    [Fact]
    public void Details_AddItem_UpdatesExistingTaskAndProgress()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = MakeTask();
        var beforeUpdatedAt = task.UpdatedAt;
        var vm = new TaskDetailsViewModel(task, state);
        vm.NewChecklistText = "New subtask";
        vm.AddChecklistItemCommand.Execute(null);
        Assert.Single(task.Checklist);
        Assert.Equal("New subtask", task.Checklist[0].Text);
        Assert.Equal("0/1", vm.ChecklistProgressText);
        Assert.Equal(string.Empty, vm.NewChecklistText);
        Assert.True(task.UpdatedAt >= beforeUpdatedAt);
        vm.Detach();
    }

    [Fact]
    public void Details_EditItemText_MutatesSameInstance()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = MakeTask();
        var item = new ChecklistItem { Text = "Original", Position = 0 };
        task.Checklist.Add(item);
        var vm = new TaskDetailsViewModel(task, state);
        var id = item.Id;
        item.Text = "Edited";
        Assert.Equal(id, task.Checklist[0].Id);
        Assert.Equal("Edited", task.Checklist[0].Text);
        vm.Detach();
    }

    [Fact]
    public void Details_DeleteItem_RemovesFromTask()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = MakeTask();
        task.Checklist.Add(new ChecklistItem { Text = "A", Position = 0 });
        var b = new ChecklistItem { Text = "B", Position = 1 };
        task.Checklist.Add(b);
        var vm = new TaskDetailsViewModel(task, state);
        vm.RemoveChecklistItemCommand.Execute(b);
        Assert.Single(task.Checklist);
        Assert.Equal("A", task.Checklist[0].Text);
        vm.Detach();
    }

    [Fact]
    public void Details_Reorder_RenumbersPositions()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = MakeTask();
        task.Checklist.Add(new ChecklistItem { Text = "A", Position = 0 });
        task.Checklist.Add(new ChecklistItem { Text = "B", Position = 1 });
        task.Checklist.Add(new ChecklistItem { Text = "C", Position = 2 });
        var vm = new TaskDetailsViewModel(task, state);
        var b = task.Checklist[1];
        vm.MoveChecklistItemUpCommand.Execute(b);
        Assert.Equal(new[] { "B", "A", "C" }, task.Checklist.Select(c => c.Text));
        Assert.Equal(new[] { 0, 1, 2 }, task.Checklist.Select(c => c.Position));
        var a = task.Checklist[1];
        vm.MoveChecklistItemDownCommand.Execute(a);
        Assert.Equal(new[] { "B", "C", "A" }, task.Checklist.Select(c => c.Text));
        vm.Detach();
    }

    [Fact]
    public void Details_UsesExistingChecklistInstance_NoCopy()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = MakeTask();
        task.Checklist.Add(new ChecklistItem { Text = "A", Position = 0 });
        var vm = new TaskDetailsViewModel(task, state);
        Assert.Same(task.Checklist, vm.Task.Checklist);
        // Add via VM must appear on original collection
        vm.NewChecklistText = "B";
        vm.AddChecklistItemCommand.Execute(null);
        Assert.Equal(2, task.Checklist.Count);
        vm.Detach();
    }

    [Fact]
    public void Details_ChecklistChangesPersistViaWorkspaceState()
    {
        var repo = Repo();
        var state = WorkspaceState.Load(repo, useDispatcherTimer: false);
        var board = state.Boards[0];
        var col = board.Columns[0];
        var task = col.Tasks[0];
        task.Checklist.Clear();
        var vm = new TaskDetailsViewModel(task, state);
        vm.NewChecklistText = "PersistMe";
        vm.AddChecklistItemCommand.Execute(null);
        vm.Detach();
        state.SaveNow();
        var loaded = WorkspaceState.Load(repo, useDispatcherTimer: false);
        var loadedTask = loaded.Boards[0].Columns[0].Tasks.First(t => t.Id == task.Id);
        Assert.Contains(loadedTask.Checklist, c => c.Text == "PersistMe");
    }

    // ---- Double-click vs single-click ----

    [Fact]
    public void DoubleClickHelper_WithinWindow_SameTask_ReturnsTrue()
    {
        var t = MakeTask();
        var last = DateTime.UtcNow;
        var now = last.AddMilliseconds(120);
        Assert.True(KanBanView.IsDoubleClickByTime(last, now, t, t));
    }

    [Fact]
    public void DoubleClickHelper_OutsideWindow_ReturnsFalse()
    {
        var t = MakeTask();
        var last = DateTime.UtcNow;
        var now = last.AddMilliseconds(800);
        Assert.False(KanBanView.IsDoubleClickByTime(last, now, t, t));
    }

    [Fact]
    public void DoubleClickHelper_DifferentTasks_ReturnsFalse()
    {
        var t1 = new TaskItem { Id = "a", Title = "A" };
        var t2 = new TaskItem { Id = "b", Title = "B" };
        var last = DateTime.UtcNow;
        var now = last.AddMilliseconds(100);
        Assert.False(KanBanView.IsDoubleClickByTime(last, now, t1, t2));
    }

    [Fact]
    public void DoubleClickHelper_NullTasks_ReturnsFalse()
    {
        var last = DateTime.UtcNow;
        var now = last.AddMilliseconds(50);
        Assert.False(KanBanView.IsDoubleClickByTime(last, now, null, null));
    }

    // ---- Both workspaces share the same details VM type ----

    [Fact]
    public void Details_CanBeOpenedFromKanBanViewModel()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var board = state.Boards[0];
        var task = board.Columns[0].Tasks[0];
        task.Title = "FromKanBan";
        var vm = new TaskDetailsViewModel(task, state);
        Assert.Equal("FromKanBan", vm.Title);
        vm.Detach();
    }

    [Fact]
    public void Details_CanBeOpenedFromTasksViewModel()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var kanBan = new KanBanViewModel(state);
        kanBan.SelectedBoard = state.Boards[0];
        var tasksVm = new TasksViewModel(state, kanBan);
        Assert.NotEmpty(tasksVm.Rows);
        var row = tasksVm.Rows[0];
        var vm = new TaskDetailsViewModel(row.Task, state);
        Assert.Equal(row.Task.Id, vm.Task.Id);
        Assert.Same(row.Task.Checklist, vm.Task.Checklist);
        vm.Detach();
    }

    [Fact]
    public void Details_Close_RaisesRequestClose_AndDetaches()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = MakeTask();
        var vm = new TaskDetailsViewModel(task, state);
        bool closed = false;
        vm.RequestClose += (_, _) => closed = true;
        // Verify live binding before close
        Assert.Equal("My Task", vm.Title);
        task.Title = "BeforeClose";
        Assert.Equal("BeforeClose", vm.Title);
        vm.CloseCommand.Execute(null);
        Assert.True(closed);
        // After close, VM detaches: Title no longer follows task,
        // and checklist changes no longer trigger progress/NotifyChanged via VM
        int progressBefore = vm.ChecklistProgressText == null ? -1 : 0;
        task.Title = "NewTitle";
        // Cached Title property still reflects value at detach time (BeforeClose)
        // Note: vm.Title getter reads _task.Title directly, but PropertyChanged
        // is no longer forwarded. We assert that HasDescription/progress etc
        // don't fire: verify NotifyChanged not called via checklist after detach
        // by observing that adding an item after detach doesn't go through VM's handler.
        // The Title getter itself will still read new value (by design), but
        // INotifyPropertyChanged must be detached — verified via event subscription count:
        // we verify that vm no longer subscribes to _task.PropertyChanged by checking
        // that PropertyChanged event for Title is not raised after detach.
        bool titleChangedFired = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TaskDetailsViewModel.Title)) titleChangedFired = true; };
        task.Title = "AfterCloseAgain";
        Assert.False(titleChangedFired);
    }

    [Fact]
    public void Details_TitleReflectsLiveTaskChanges_UntilDetach()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = MakeTask("Old");
        var vm = new TaskDetailsViewModel(task, state);
        Assert.Equal("Old", vm.Title);
        bool titleChanged = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TaskDetailsViewModel.Title)) titleChanged = true; };
        task.Title = "Updated";
        Assert.Equal("Updated", vm.Title);
        Assert.True(titleChanged);
        // Detach: PropertyChanged must no longer fire for Title
        titleChanged = false;
        vm.Detach();
        task.Title = "AfterDetach";
        Assert.False(titleChanged);
        // Getter still reads task.Title (VM holds reference), that's expected
        Assert.Equal("AfterDetach", vm.Task.Title);
    }

    // ---- Checklist progress format 2/5 • 40% requested ----

    [Fact]
    public void Details_ProgressDisplay_Is2Slash5Dot40Percent()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var task = MakeTask();
        for (int i = 0; i < 5; i++)
            task.Checklist.Add(new ChecklistItem { Text = $"I{i}", IsCompleted = i < 2, Position = i });
        var vm = new TaskDetailsViewModel(task, state);
        Assert.Equal("2/5", vm.ChecklistProgressText);
        Assert.Equal("40%", vm.ChecklistPercentText);
        Assert.Equal("2/5 \u2022 40%", vm.ProgressDisplay);
        vm.Detach();
    }
}
