namespace ItemTradeApp.Features.EmailsNotifications.Notifications.Contracts;

public sealed record CreateNotificationRequest(int UserId, string Title, string Message);
