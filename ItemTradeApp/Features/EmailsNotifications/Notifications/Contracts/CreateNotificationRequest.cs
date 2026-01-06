namespace ItemTradeApp.Features.EmaillsNotifications.Notifications.Contracts;

public sealed record CreateNotificationRequest(int UserId, string Title, string Message);
