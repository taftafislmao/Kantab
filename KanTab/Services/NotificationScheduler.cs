using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KanTab.Models;
using KanTab.Storage;
using KanTab.ViewModels;

namespace KanTab.Services;

public enum NotificationKind { Reminder, Overdue }

/// <summary>
/// Background scheduler that polls <see cref="WorkspaceState"/> every 30–60s and
/// delivers desktop notifications via <see cref="INotificationService"/>.
/// Anti-spam is enforced by a per-(taskId, kind, dueInstant) set; changing the
/// due date automatically yields a new key so it becomes eligible again.
/// </summary>
public sealed class NotificationScheduler : IDisposable
{
    private readonly WorkspaceState _state;
    private readonly INotificationService _notifier;
    private readonly Func<DateTime> _nowLocal;
    private readonly TimeSpan _interval;
    private readonly HashSet<string> _sent = new();
    private readonly object _gate = new();
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private int _running; // 0/1 guard against overlapping ticks

    public NotificationScheduler(
        WorkspaceState state,
        INotificationService notifier,
        Func<DateTime>? nowLocal = null,
        TimeSpan? interval = null)
    {
        _state = state;
        _notifier = notifier;
        _nowLocal = nowLocal ?? (() => DateTime.Now);
        _interval = interval ?? TimeSpan.FromSeconds(45);
    }

    public bool IsRunning => _loop != null && !_loop.IsCompleted;

    public void Start()
    {
        if (_loop != null && !_loop.IsCompleted) return;
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(_interval);
        _loop = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        _timer?.Dispose();
        _timer = null;
        // Do not block indefinitely on shutdown; give the loop a moment.
        try { _loop?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    public void Dispose() => Stop();

    /// <summary>Invoked by tests to run a single evaluation synchronously.</summary>
    public int CheckOnce()
        => EvaluateAndNotify(_nowLocal());

    private async Task RunAsync(CancellationToken ct)
    {
        // Fire once shortly after start so an overdue task notifies promptly.
        await SafeTickAsync(ct).ConfigureAwait(false);
        try
        {
            while (_timer != null && await _timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                await SafeTickAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }

    private async Task SafeTickAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
        try { EvaluateAndNotify(_nowLocal()); }
        catch { }
        finally { Volatile.Write(ref _running, 0); }
        await Task.CompletedTask;
    }

    private int EvaluateAndNotify(DateTime nowLocal)
    {
        var settings = CurrentSettings();
        var tasks = SnapshotTasks();
        var sentNow = 0;

        foreach (var task in tasks)
        {
            var due = task.DueDate;
            if (due == null) continue;
            var dueInstant = NotificationEligibility.DueInstantLocal(task);
            var dueKey = dueInstant.ToString("O");

            if (NotificationEligibility.IsReminderEligible(task, settings, nowLocal))
            {
                var key = $"{task.Id}|Reminder|{dueKey}";
                lock (_gate) { if (!_sent.Add(key)) continue; }
                _notifier.Show(new NotificationRequest("Task due soon", $"{task.Title}\n{NotificationEligibility.ReminderBody(task, nowLocal)}"));
                sentNow++;
            }

            if (NotificationEligibility.IsOverdueEligible(task, settings, nowLocal))
            {
                var key = $"{task.Id}|Overdue|{dueKey}";
                lock (_gate) { if (!_sent.Add(key)) continue; }
                _notifier.Show(new NotificationRequest("Task overdue", $"{task.Title}\n{NotificationEligibility.OverdueBody(task, nowLocal)}"));
                sentNow++;
            }
        }

        // Prune keys for tasks that no longer exist or whose due date changed beyond recognition.
        // We keep it cheap: if a key's taskId is gone, drop it. We don't aggressively prune
        // completed tasks because they will naturally not match again and the set stays small.
        var liveIds = new HashSet<string>(tasks.Select(t => t.Id));
        var liveDueKeys = new HashSet<string>(tasks.Where(t => t.DueDate != null).Select(t => $"{t.Id}|{NotificationEligibility.DueInstantLocal(t):O}"));
        lock (_gate)
        {
            _sent.RemoveWhere(k =>
            {
                var bar = k.IndexOf('|');
                if (bar < 0) return true;
                var id = k.Substring(0, bar);
                if (!liveIds.Contains(id)) return true;
                // Keep overdue/reminder keys whose due instant still matches a live task;
                // if the due date changed, the old key remains but will never block the new one
                // (new dueKey differs), so no need to remove it eagerly. Keep for simplicity.
                return false;
            });
        }

        return sentNow;
    }

    private AppSettings CurrentSettings()
    {
        try
        {
            // WorkspaceState.BuildSnapshot is not needed; read the persisted settings object directly.
            // WorkspaceState exposes settings via its snapshot or via repository load; we keep a
            // lightweight path by reading the last saved settings from the state's BuildSnapshot.
            var snap = _state.BuildSnapshot();
            return snap.Settings ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    private List<TaskItem> SnapshotTasks()
    {
        try { return _state.Boards.SelectMany(b => b.Columns).SelectMany(c => c.Tasks).ToList(); }
        catch { return new List<TaskItem>(); }
    }
}
