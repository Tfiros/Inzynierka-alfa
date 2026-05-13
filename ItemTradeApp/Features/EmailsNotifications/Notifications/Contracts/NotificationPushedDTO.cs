namespace ItemTradeApp.Features.EmailsNotifications.Notifications.Contracts;

public sealed record NotificationPushedDTO(
    int Id,
    string Title,
    string Message,
    DateTimeOffset CreatedAt);