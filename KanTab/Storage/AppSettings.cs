using System.Text.Json.Serialization;

namespace KanTab.Storage;

/// <summary>Supported lead-time before a due date when a reminder fires.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReminderLeadTime
{
    Minutes5 = 5,
    Minutes15 = 15,
    Minutes30 = 30,
    Minutes60 = 60,
    Minutes1440 = 1440 // 1 day
}

/// <summary>
/// User-level application preferences persisted with the rest of KanTab data.
/// Never contains secrets, API keys, or credentials.
/// </summary>
public class AppSettings
{
    public string? LastLaunchedVersion { get; set; }

    /// <summary>Master switch for due-soon reminders.</summary>
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>How early before the due instant the reminder is eligible.</summary>
    public ReminderLeadTime ReminderLeadMinutes { get; set; } = ReminderLeadTime.Minutes15;

    /// <summary>Whether overdue notifications fire once a task becomes overdue.</summary>
    public bool OverdueNotificationsEnabled { get; set; } = true;

    /// <summary>First-launch setup completed (Welcome/Auth done). Reset on log out; NOT set by Continue Offline (transient offline).</summary>
    public bool InitialSetupCompleted { get; set; } = false;
}
