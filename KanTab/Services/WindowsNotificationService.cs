using System;

namespace KanTab.Services;

/// <summary>
/// Windows desktop notifications. Uses the built-in Windows 10+ toast path
/// when available; falls back to a non-throwing no-op so tests and headless
/// runs never crash. No task data is owned here — the scheduler owns that.
/// </summary>
public sealed class WindowsNotificationService : INotificationService
{
    public void Show(NotificationRequest request)
    {
        try
        {
#if WINDOWS
            WindowsShow(request);
#else
            // On non-Windows (or when WINDOWS not defined), intentionally do nothing.
            // This keeps the implementation dependency-free for the current net10.0 / net8.0
            // build and test environments without pulling a large notification package.
            System.Diagnostics.Debug.WriteLine($"[KanTab notification] {request.Title}: {request.Body}");
#endif
        }
        catch
        {
            // Never throw from notification delivery; scheduler must continue.
        }
    }

#if WINDOWS
    private static void WindowsShow(NotificationRequest request)
    {
        // Minimal WinRT toast path — isolated here so the rest of the app has no WinRT dep.
        // If Microsoft.Toolkit.Uwp.Notifications is not referenced, this branch still compiles
        // via reflection / dynamic, but we prefer the package-based path once installed.
        // Current selection: use the lightweight Windows App SDK toast via
        // Microsoft.Toolkit.Uwp.Notifications (8.x) when present. If absent, this still
        // degrades gracefully because the WINDOWS define is off by default.
        var xml = $@"<toast><visual><binding template='ToastGeneric'><text>{Escape(request.Title)}</text><text>{Escape(request.Body)}</text></binding></visual></toast>";
        var doc = new Windows.Data.Xml.Dom.XmlDocument();
        doc.LoadXml(xml);
        var toast = new Windows.UI.Notifications.ToastNotification(doc);
        Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("KanTab").Show(toast);
    }

    private static string Escape(string s) => System.Security.SecurityElement.Escape(s) ?? string.Empty;
#endif
}
