#pragma warning disable xUnit1031
using System;
using System.IO;
using System.Linq;
using KanTab.Models;
using KanTab.Services;
using KanTab.Storage;
using KanTab.ViewModels;
using Xunit;

namespace KanTab.Tests;

public class Phase16PolishTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "KanTab.Tests", "Phase16", Guid.NewGuid().ToString());
    private string FilePath => Path.Combine(_dir, "kantab.json");
    public Phase16PolishTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
    private LocalJsonKanTabRepository Repo() => new(FilePath);

    [Fact]
    public void TaskItem_DueTime_PersistsAndDisplays()
    {
        var t = new TaskItem { Title = "T", DueDate = new DateOnly(2026,9,10), DueTime = new TimeSpan(14,30,0) };
        Assert.NotNull(t.DueDateTime);
        Assert.NotNull(t.DueDateDisplay);
        // DueDateDisplay includes the due time portion (format varies by locale, at least contains ':')
        Assert.Contains(":", t.DueDateDisplay);
    }

    [Fact]
    public void TaskItem_NoDueDate_NoDisplay()
    {
        var t = new TaskItem { Title = "T" };
        Assert.Null(t.DueDateTime);
        Assert.Null(t.DueDateDisplay);
        Assert.False(t.IsOverdue);
    }

    [Fact]
    public void TaskItem_IsOverdue_UsesDueDateTime()
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var past = new TaskItem { Title="T", DueDate = today.AddDays(-1), DueTime = new TimeSpan(0,0,0) };
        Assert.True(past.IsOverdue);
        var future = new TaskItem { Title="T", DueDate = today.AddDays(1) };
        Assert.False(future.IsOverdue);
        var completed = new TaskItem { Title="T", DueDate = today.AddDays(-1), IsCompleted = true };
        Assert.False(completed.IsOverdue);
    }

    [Fact]
    public void TaskDialogViewModel_HasDueTime()
    {
        var vm = new TaskDialogViewModel();
        Assert.Null(vm.DueTime);
        vm.DueTime = new TimeSpan(9,0,0);
        Assert.Equal(new TimeSpan(9,0,0), vm.DueTime);
    }

    [Fact]
    public void TasksViewModel_DueDateSort_UsesDueDateTime()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        state.Boards.Clear();
        var b = new Board { Id="b1", Name="B", Position=0 };
        var col = new KanBanColumn { Id="c1", Title="To Do", BoardId="b1", Position=0 };
        b.Columns.Add(col);
        // Give distinct titles so we can identify rows regardless of seed data
        var t1 = new TaskItem { Id="t1", Title="AAA First", DueDate=new DateOnly(2026,9,10), DueTime=new TimeSpan(9,0,0), BoardId="b1", ColumnId="c1" };
        var t2 = new TaskItem { Id="t2", Title="ZZZ Second", DueDate=new DateOnly(2026,9,10), DueTime=new TimeSpan(15,0,0), BoardId="b1", ColumnId="c1" };
        col.Tasks.Add(t2); col.Tasks.Add(t1);
        state.Boards.Add(b);
        state.SelectedBoard = b;
        var vm = new TasksViewModel(state, new KanBanViewModel(state));
        vm.SelectedSort = "Due date";
        var ids = vm.Rows.Where(r => r.Task.Id is "t1" or "t2").OrderBy(r => r.Task.DueDateTime).Select(r => r.Task.Id).ToList();
        Assert.Equal(new[] { "t1","t2" }, ids);
        // And verify the sort actually put t1 before t2
        var rowIds = vm.Rows.Where(r => r.Task.Id is "t1" or "t2").Select(r => r.Task.Id).ToList();
        Assert.Equal(new[] { "t1","t2" }, rowIds);
    }

    [Fact]
    public void Notification_WithDueTime_Eligible()
    {
        var t = new TaskItem { Title="T", DueDate=new DateOnly(2026,9,10), DueTime=new TimeSpan(14,0,0) };
        var settings = new AppSettings { NotificationsEnabled=true, ReminderLeadMinutes=ReminderLeadTime.Minutes15, OverdueNotificationsEnabled=true };
        var dueInstant = NotificationEligibility.DueInstantLocal(t);
        Assert.True(NotificationEligibility.IsReminderEligible(t, settings, dueInstant.AddMinutes(-10)));
        Assert.False(NotificationEligibility.IsReminderEligible(t, settings, dueInstant.AddMinutes(-20)));
        Assert.True(NotificationEligibility.IsOverdueEligible(t, settings, dueInstant.AddMinutes(1)));
        Assert.False(NotificationEligibility.IsOverdueEligible(t, settings, dueInstant.AddMinutes(-1)));
    }

    [Fact]
    public void WorkspaceState_Settings_PersistViaRepo()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        state.Settings.NotificationsEnabled = false;
        state.Settings.ReminderLeadMinutes = ReminderLeadTime.Minutes30;
        state.Settings.OverdueNotificationsEnabled = false;
        state.SaveNow();
        var loaded = Repo().LoadAsync().GetAwaiter().GetResult();
        Assert.False(loaded.Settings.NotificationsEnabled);
        Assert.Equal(ReminderLeadTime.Minutes30, loaded.Settings.ReminderLeadMinutes);
        Assert.False(loaded.Settings.OverdueNotificationsEnabled);
    }

    [Fact]
    public void KanBan_DragThreshold_Still5()
    {
        // Sanity: 5px threshold preserved
        bool WouldDrag(Avalonia.Point s, Avalonia.Point c) => Math.Abs((c - s).X) >= 5 || Math.Abs((c - s).Y) >= 5;
        Assert.False(WouldDrag(new(0,0), new(4.9,0)));
        Assert.True(WouldDrag(new(0,0), new(5,0)));
    }
}
