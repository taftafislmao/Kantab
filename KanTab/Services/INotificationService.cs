namespace KanTab.Services;

public sealed record NotificationRequest(string Title, string Body);

public interface INotificationService
{
    void Show(NotificationRequest request);
}
