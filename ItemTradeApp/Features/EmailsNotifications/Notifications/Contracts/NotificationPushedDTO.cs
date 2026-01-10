namespace ItemTradeApp.Features.EmailsNotifications.Notifications;

public sealed record NotificationPushedDTO(
    int Id,
    string Title,
    string Message,
    DateTimeOffset CreatedAt);