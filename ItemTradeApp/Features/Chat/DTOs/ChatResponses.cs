namespace ItemTradeApp.Features.Chat.DTOs;

public sealed record ChatThreadListItemDto(
    int ChatConversationId,
    int? OtherUserId,
    string? OtherUserAuth0UserId,
    string? OtherUserNickname,
    string? OtherUserTradeRole,
    string? AvatarUrl,
    bool? IsOnline, 

    long? LastMessageId,
    string? LastMessageText,
    int? LastMessageSenderId,
    DateTime? LastMessageCreatedAtUtc,

    int UnreadCount,
    int TradeId,
    DateTime? ClosedAtUtc
);

public sealed record ChatMessageDto(
    long Id,
    int ChatConversationId,
    int SenderId,
    string Message,
    DateTime CreatedAt,
    DateTime? EditedAt
);
public sealed record ChatReadStateDto(
    int ChatConversationId,
    long LastReadMessageId,
    DateTime MarkedAtUtc,
    int UnreadCount
);

public sealed record ChatThreadUpdatedDto(
    int ChatConversationId,
    long LastMessageId,
    string LastMessageText,
    int LastMessageSenderId,
    DateTime LastMessageCreatedAtUtc,
    int UnreadCount
);