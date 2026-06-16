namespace ItemTradeApp.Features.Users.UserInfo.DTOs.Response;

public sealed record UserProfileInfoResponse(
    int Id,
    int Level,
    DateOnly RegistrationDate,
    string Nickname,
    string Description,
    string? ImageUrl,
    int ActiveOffersCount,
    int SuccessTradesCount,
    float Rating,
    float SuccessRate
);