namespace ItemTradeApp.Features.Shared.Notifications.DTOs;

public sealed record NotificationDto(
    int Id,
    string Title,
    string Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    bool IsRead);