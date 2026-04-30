namespace ItemTradeApp.Features.Shared.Notifications.DTOs;

public sealed record CreateNotificationRequest(int UserId, string Title, string Message);
