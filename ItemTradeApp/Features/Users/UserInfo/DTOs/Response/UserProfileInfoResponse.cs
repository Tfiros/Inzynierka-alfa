namespace ItemTradeApp.Features.Users.UserInfo.DTOs.Response;

public sealed record UserProfileInfoResponse(
    int      Id,
    int      Experience,
    int      Level,
    DateOnly RegistrationDate,
    string   Nickname,
    string   Description
    );