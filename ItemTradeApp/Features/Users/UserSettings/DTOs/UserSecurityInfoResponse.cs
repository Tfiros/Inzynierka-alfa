namespace ItemTradeApp.Features.Users.UserSettings.DTOs;

public sealed record UserSecurityInfoResponse(
    int      Id,
    DateOnly   dateOfBirth,
    string   email
    );
        