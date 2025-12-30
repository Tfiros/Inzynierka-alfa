namespace ItemTradeApp.Features.UsersFeature.UserSettings.DTOs;

public sealed record UserSecurityInfoResponse(
    int      Id,
    DateOnly   dateOfBirth,
    string   email
    );
        