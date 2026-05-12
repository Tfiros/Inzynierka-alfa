namespace ItemTradeApp.Features.Users.UserInfo.DTOs.Request;

public record UpdateProfileRequest
(
    string? Nickname,
    string? Description,
    IFormFile? Image
);