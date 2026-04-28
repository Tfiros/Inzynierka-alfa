namespace ItemTradeApp.Features.Shared.Notifications.Contracts;

public sealed record CreateNotificationRequest(int UserId, string Title, string Message);
