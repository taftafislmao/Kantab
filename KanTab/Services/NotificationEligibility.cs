using System;
using KanTab.Models;
using KanTab.Storage;

namespace KanTab.Services;

/// <summary>
/// Pure helpers for notification eligibility. No I/O, no timers — easy to unit test.
/// Due dates in KanTab are <see cref="DateOnly"/> (day precision). We interpret
/// the due instant as local midnight at the start of the due date (00:00).
/// A reminder with lead time L is eligible when now ∈ [dueInstant − L, dueInstant).
/// Overdue is eligible when now ≥ dueInstant + 1 day (i.e. strictly after the due date)
/// and the task is incomplete. This mirrors the existing KanTab overdue semantics
/// (due date strictly before today) while also being precise enough for reminder testing.
/// </summary>
public static class NotificationEligibility
{
    public static DateTime DueInstantLocal(TaskItem task)
    {
        if (task.DueDate == null) return DateTime.MaxValue;
        return task.DueDate.Value.ToDateTime(
            task.DueTime == null ? TimeOnly.MinValue : TimeOnly.FromTimeSpan(task.DueTime.Value),
            DateTimeKind.Local);
    }

    // Legacy overload for tests that pass DateOnly directly
    public static DateTime DueInstantLocal(DateOnly dueDate)
        => dueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);

    public static bool IsReminderEligible(TaskItem task, AppSettings settings, DateTime nowLocal)
    {
        if (!settings.NotificationsEnabled) return false;
        if (task.IsCompleted) return false;
        if (task.DueDate == null) return false;
        var due = DueInstantLocal(task);
        var lead = TimeSpan.FromMinutes((int)settings.ReminderLeadMinutes);
        var windowStart = due - lead;
        return nowLocal >= windowStart && nowLocal < due;
    }

    public static bool IsOverdueEligible(TaskItem task, AppSettings settings, DateTime nowLocal)
    {
        if (!settings.OverdueNotificationsEnabled) return false;
        if (task.IsCompleted) return false;
        if (task.DueDate == null) return false;
        var due = DueInstantLocal(task);
        // Overdue when now >= due instant (supports 5-min reminders on same day) and was already
        // past the due instant. Using due instant directly is correct now that DueTime exists.
        return nowLocal >= due;
    }

    public static string ReminderBody(TaskItem task, DateTime nowLocal)
    {
        if (task.DueDate == null) return task.Title;
        var due = DueInstantLocal(task);
        var remaining = due - nowLocal;
        if (remaining <= TimeSpan.Zero) return $"Due now — {task.DueDate.Value:MMM d}";
        if (remaining.TotalHours >= 24) return $"Due in {(int)Math.Ceiling(remaining.TotalDays)} day(s) — {task.DueDate.Value:MMM d}";
        if (remaining.TotalHours >= 1) return $"Due in {(int)Math.Ceiling(remaining.TotalHours)} hour(s)";
        return $"Due in {(int)Math.Ceiling(remaining.TotalMinutes)} minute(s)";
    }

    public static string OverdueBody(TaskItem task, DateTime nowLocal)
    {
        if (task.DueDate == null) return "Was due recently";
        var due = DueInstantLocal(task);
        var overdue = nowLocal - due;
        if (overdue.TotalDays >= 2) return $"Was due {task.DueDate.Value:MMM d} ({(int)overdue.TotalDays} days ago)";
        if (overdue.TotalDays >= 1) return "Was due yesterday";
        return "Is overdue";
    }
}
