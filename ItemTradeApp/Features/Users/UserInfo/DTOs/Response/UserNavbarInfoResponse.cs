namespace ItemTradeApp.Features.Users.UserInfo.DTOs.Response;

public sealed record UserNavbarInfoResponse(
    int    Id,
    string Nickname,
    string Email,
    int    Tokens,
    int    Experience,
    int    Level
);