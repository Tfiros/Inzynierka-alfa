namespace ItemTradeApp.Features.Shared.Notifications.DTOs;

public sealed record GetNotificationsResponse(
    List<NotificationDto> Items,
    DateTimeOffset? NextCursorCreatedAt,
    int? NextCursorId,
    bool HasMore);