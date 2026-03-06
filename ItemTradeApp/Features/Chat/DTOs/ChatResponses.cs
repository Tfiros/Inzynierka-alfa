namespace ItemTradeApp.Features.Chat.DTOs;

public sealed record ChatThreadListItemDto(
    int ChatConversationId,

    bool IsGroup,
    string DisplayName,            // DM: druga osoba, Group: conversation.name
    int? OtherUserId,              // tylko DM
    string? OtherUserAuth0UserId,  // tylko DM (do presence / user-stream)
    string? AvatarUrl,             // DM: druga osoba, Group: null lub group avatar
    bool? IsOnline,                // tylko DM (może null jeśli nie wyliczamy)

    long? LastMessageId,
    string? LastMessageText,
    int? LastMessageSenderId,
    DateTime? LastMessageCreatedAtUtc,

    int UnreadCount
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
public sealed record CreateDmChatResponse(int ChatConversationId);

public sealed record ChatThreadUpdatedDto(
    int ChatConversationId,
    long LastMessageId,
    string LastMessageText,
    int LastMessageSenderId,
    DateTime LastMessageCreatedAtUtc,
    int UnreadCount
);