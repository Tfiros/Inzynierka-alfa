namespace ItemTradeApp.Features.Shared.Notifications.DTOs;

public sealed record NotificationPushedDTO(
    int Id,
    string Title,
    string Message,
    DateTimeOffset CreatedAt);