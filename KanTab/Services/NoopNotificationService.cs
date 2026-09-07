namespace KanTab.Services;

public sealed class NoopNotificationService : INotificationService
{
    public void Show(NotificationRequest request) { }
}
