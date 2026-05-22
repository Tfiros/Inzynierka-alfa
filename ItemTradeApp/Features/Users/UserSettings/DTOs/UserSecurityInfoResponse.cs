namespace ItemTradeApp.Features.Users.UserSettings.DTOs;

public sealed record UserSecurityInfoResponse(
    int      Id,
    DateOnly   DateOfBirth,
    string   Email
    );
        