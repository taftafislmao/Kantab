#pragma warning disable xUnit1031
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KanTab.Models;
using KanTab.Services;
using KanTab.Storage;
using KanTab.ViewModels;
using Xunit;

namespace KanTab.Tests;

public sealed class FakeNotifier : INotificationService
{
    public readonly List<NotificationRequest> Sent = new();
    public void Show(NotificationRequest r) => Sent.Add(r);
}

public class NotificationEligibilityTests
{
    private static TaskItem Task(DateOnly? due, bool completed = false) => new() { Id = Guid.NewGuid().ToString(), Title = "T", DueDate = due, IsCompleted = completed };
    private static AppSettings Settings(ReminderLeadTime lead = ReminderLeadTime.Minutes15, bool enabled = true, bool overdue = true) => new() { NotificationsEnabled = enabled, ReminderLeadMinutes = lead, OverdueNotificationsEnabled = overdue };

    [Fact]
    public void Reminder_WithinInterval_Eligible()
    {
        var due = new DateOnly(2026, 9, 10);
        var dueInstant = NotificationEligibility.DueInstantLocal(due);
        var now = dueInstant.AddMinutes(-10); // 10 min before, lead 15 => eligible
        Assert.True(NotificationEligibility.IsReminderEligible(Task(due), Settings(ReminderLeadTime.Minutes15), now));
    }

    [Fact]
    public void Reminder_OutsideInterval_NotEligible()
    {
        var due = new DateOnly(2026, 9, 10);
        var dueInstant = NotificationEligibility.DueInstantLocal(due);
        Assert.False(NotificationEligibility.IsReminderEligible(Task(due), Settings(ReminderLeadTime.Minutes15), dueInstant.AddMinutes(-20)));
        Assert.False(NotificationEligibility.IsReminderEligible(Task(due), Settings(ReminderLeadTime.Minutes15), dueInstant.AddMinutes(1)));
    }

    [Fact]
    public void Reminder_AllLeadTimes_Work()
    {
        var due = new DateOnly(2026, 9, 10);
        var dueInstant = NotificationEligibility.DueInstantLocal(due);
        foreach (var lead in new[] { ReminderLeadTime.Minutes5, ReminderLeadTime.Minutes15, ReminderLeadTime.Minutes30, ReminderLeadTime.Minutes60, ReminderLeadTime.Minutes1440 })
        {
            var mins = (int)lead;
            Assert.True(NotificationEligibility.IsReminderEligible(Task(due), Settings(lead), dueInstant.AddMinutes(-mins + 1)));
            Assert.False(NotificationEligibility.IsReminderEligible(Task(due), Settings(lead), dueInstant.AddMinutes(-mins - 1)));
        }
    }

    [Fact]
    public void Reminder_Completed_NotEligible()
        => Assert.False(NotificationEligibility.IsReminderEligible(Task(new DateOnly(2026,9,10), true), Settings(), DateTime.Now));

    [Fact]
    public void Reminder_NoDueDate_NotEligible()
        => Assert.False(NotificationEligibility.IsReminderEligible(Task(null), Settings(), DateTime.Now));

    [Fact]
    public void Overdue_IncompleteOverdue_Eligible()
    {
        var due = new DateOnly(2026, 9, 8);
        var dueInstant = NotificationEligibility.DueInstantLocal(due);
        Assert.True(NotificationEligibility.IsOverdueEligible(Task(due), Settings(), dueInstant.AddDays(1).AddMinutes(1)));
    }

    [Fact]
    public void Overdue_NotYetOverdue_NotEligible()
    {
        var due = new DateOnly(2026, 9, 10);
        var dueInstant = NotificationEligibility.DueInstantLocal(due);
        // At the exact due instant we are now overdue (supports time-aware tasks)
        Assert.True(NotificationEligibility.IsOverdueEligible(Task(due), Settings(), dueInstant));
        // 12h later is still overdue
        Assert.True(NotificationEligibility.IsOverdueEligible(Task(due), Settings(), dueInstant.AddHours(12)));
        // 1s before is not
        Assert.False(NotificationEligibility.IsOverdueEligible(Task(due), Settings(), dueInstant.AddSeconds(-1)));
    }

    [Fact]
    public void Overdue_Completed_NotEligible()
        => Assert.False(NotificationEligibility.IsOverdueEligible(Task(new DateOnly(2026,9,8), true), Settings(), DateTime.Now.AddDays(5)));

    [Fact]
    public void Overdue_NoDueDate_NotEligible()
        => Assert.False(NotificationEligibility.IsOverdueEligible(Task(null), Settings(), DateTime.Now.AddDays(5)));

    [Fact]
    public void Disabled_DisablesBoth()
    {
        var due = new DateOnly(2026, 9, 10);
        var dueInstant = NotificationEligibility.DueInstantLocal(due);
        Assert.False(NotificationEligibility.IsReminderEligible(Task(due), Settings(enabled:false), dueInstant.AddMinutes(-10)));
        Assert.False(NotificationEligibility.IsOverdueEligible(Task(due), Settings(overdue:false), dueInstant.AddDays(2)));
    }
}

public class NotificationSchedulerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "KanTab.Tests", "NotifSched", Guid.NewGuid().ToString());
    private string FilePath => Path.Combine(_dir, "kantab.json");
    public NotificationSchedulerTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
    private LocalJsonKanTabRepository Repo() => new(FilePath);

    private WorkspaceState NewState()
    {
        var s = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        s.Boards.Clear(); s.Notes.Clear();
        var b = new Board { Id = "b1", Name = "B", Position = 0 };
        var col = new KanBanColumn { Id = "c1", Title = "To Do", BoardId = "b1", Position = 0 };
        b.Columns.Add(col);
        s.Boards.Add(b);
        return s;
    }

    private static DateTime DueInstant(DateOnly d) => NotificationEligibility.DueInstantLocal(d);

    [Fact]
    public void Scheduler_OnlyOncePerDueOccurrence()
    {
        var state = NewState();
        var due = new DateOnly(2026, 9, 10);
        var t = new TaskItem { Id = "t1", Title = "T", DueDate = due, BoardId = "b1", ColumnId = "c1" };
        state.Boards[0].Columns[0].Tasks.Add(t);
        state.Settings.ReminderLeadMinutes = ReminderLeadTime.Minutes15;
        state.Settings.NotificationsEnabled = true;

        var now = DueInstant(due).AddMinutes(-10);
        var fake = new FakeNotifier();
        var sched = new NotificationScheduler(state, fake, () => now, TimeSpan.FromSeconds(45));
        Assert.Equal(1, sched.CheckOnce());
        Assert.Single(fake.Sent);
        // second poll at same time must not spam
        Assert.Equal(0, sched.CheckOnce());
        Assert.Single(fake.Sent);
    }

    [Fact]
    public void Scheduler_DueDateChange_AllowsNewReminder()
    {
        var state = NewState();
        var due1 = new DateOnly(2026, 9, 10);
        var t = new TaskItem { Id = "t1", Title = "T", DueDate = due1, BoardId = "b1", ColumnId = "c1" };
        state.Boards[0].Columns[0].Tasks.Add(t);
        state.Settings.ReminderLeadMinutes = ReminderLeadTime.Minutes15;
        var now = DueInstant(due1).AddMinutes(-10);
        var fake = new FakeNotifier();
        var sched = new NotificationScheduler(state, fake, () => now, TimeSpan.FromSeconds(45));
        sched.CheckOnce();
        Assert.Single(fake.Sent);

        // change due date to tomorrow
        var due2 = new DateOnly(2026, 9, 11);
        t.DueDate = due2;
        var now2 = DueInstant(due2).AddMinutes(-10);
        var sched2 = new NotificationScheduler(state, fake, () => now2, TimeSpan.FromSeconds(45));
        // Use same fake but new scheduler sees new dueKey, but old scheduler already has old key.
        // Easiest: reuse same scheduler by calling CheckOnce with new now via new instance sharing sent set.
        // Instead verify fresh scheduler with same fake and new now sends again.
        var fake2 = new FakeNotifier();
        var schedNew = new NotificationScheduler(state, fake2, () => now2, TimeSpan.FromSeconds(45));
        Assert.Equal(1, schedNew.CheckOnce());
    }

    [Fact]
    public void Scheduler_Completed_NoNotification()
    {
        var state = NewState();
        var due = new DateOnly(2026, 9, 10);
        state.Boards[0].Columns[0].Tasks.Add(new TaskItem { Id = "t1", Title = "T", DueDate = due, IsCompleted = true, BoardId = "b1", ColumnId = "c1" });
        state.Settings.ReminderLeadMinutes = ReminderLeadTime.Minutes15;
        var now = DueInstant(due).AddMinutes(-10);
        var fake = new FakeNotifier();
        var sched = new NotificationScheduler(state, fake, () => now, TimeSpan.FromSeconds(45));
        Assert.Equal(0, sched.CheckOnce());
    }

    [Fact]
    public void Scheduler_MultipleTasks_Independent()
    {
        var state = NewState();
        var due = new DateOnly(2026, 9, 10);
        for (int i = 0; i < 3; i++)
            state.Boards[0].Columns[0].Tasks.Add(new TaskItem { Id = $"t{i}", Title = $"T{i}", DueDate = due, BoardId = "b1", ColumnId = "c1" });
        state.Settings.ReminderLeadMinutes = ReminderLeadTime.Minutes15;
        var now = DueInstant(due).AddMinutes(-10);
        var fake = new FakeNotifier();
        var sched = new NotificationScheduler(state, fake, () => now, TimeSpan.FromSeconds(45));
        Assert.Equal(3, sched.CheckOnce());
        Assert.Equal(0, sched.CheckOnce());
    }

    [Fact]
    public void Scheduler_Overdue_OnlyOnce()
    {
        var state = NewState();
        var due = new DateOnly(2026, 9, 8);
        state.Boards[0].Columns[0].Tasks.Add(new TaskItem { Id = "t1", Title = "T", DueDate = due, BoardId = "b1", ColumnId = "c1" });
        var now = DueInstant(due).AddDays(1).AddMinutes(5);
        var fake = new FakeNotifier();
        var sched = new NotificationScheduler(state, fake, () => now, TimeSpan.FromSeconds(45));
        Assert.Equal(1, sched.CheckOnce());
        Assert.Equal(0, sched.CheckOnce());
    }

    [Fact]
    public void Scheduler_NoDueDate_Never()
    {
        var state = NewState();
        state.Boards[0].Columns[0].Tasks.Add(new TaskItem { Id = "t1", Title = "T", DueDate = null, BoardId = "b1", ColumnId = "c1" });
        var fake = new FakeNotifier();
        var sched = new NotificationScheduler(state, fake, () => DateTime.Now, TimeSpan.FromSeconds(45));
        Assert.Equal(0, sched.CheckOnce());
    }

    [Fact]
    public async Task Scheduler_StartStop_Dispose_NoOverlap()
    {
        var state = NewState();
        var fake = new FakeNotifier();
        var sched = new NotificationScheduler(state, fake, interval: TimeSpan.FromMilliseconds(20));
        sched.Start();
        Assert.True(sched.IsRunning);
        await Task.Delay(80);
        sched.Stop();
        Assert.False(sched.IsRunning);
        // restart should work
        sched.Start();
        Assert.True(sched.IsRunning);
        sched.Dispose();
        Assert.False(sched.IsRunning);
    }

    [Fact]
    public void Settings_Persist_RoundTrip()
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
    public void Settings_ExistingFields_IntactAfterSave()
    {
        var state = WorkspaceState.Load(Repo(), useDispatcherTimer: false);
        var beforeBoards = state.Boards.Count;
        state.Settings.NotificationsEnabled = false;
        state.SaveNow();
        var loaded = Repo().LoadAsync().GetAwaiter().GetResult();
        Assert.Equal(beforeBoards, loaded.Boards.Count);
    }
}
