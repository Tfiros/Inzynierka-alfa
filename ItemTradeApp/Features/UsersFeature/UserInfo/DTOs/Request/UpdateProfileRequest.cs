namespace ItemTradeApp.Features.UsersFeature.UserInfo.DTOs.Request;

public record UpdateProfileRequest
(
    string? Nickname,
    string? Description
);