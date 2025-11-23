namespace ItemTradeApp.Features.UsersFeature.UserInfo.DTOs.Response;

public sealed record UserProfileInfoResponse(
    int      Id,
    string   Email,
    DateOnly DateOfBirth,
    int      Tokens,
    int      Experience,
    int      Level,
    DateOnly RegistrationDate,
    string   Nickname,
    string   Description
    );