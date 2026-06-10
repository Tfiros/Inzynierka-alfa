namespace ItemTradeApp.Features.Users.UserInfo.DTOs.Response;

public sealed record UserNavbarInfoResponse(
    int    Id,
    string Nickname,
    string Email,
    int    Tokens,
    int    EscrowedTokens,
    int    Experience,
    int    Level,
    List<int> ChatIds,
    List<int> ChatUnreadIds,
    int NotificationsUnreadTotal,
    string? ImageUrl
);
public sealed record ChatInfos(int ChatId, int UnreadCount);