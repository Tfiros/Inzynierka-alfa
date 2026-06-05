namespace ItemTradeApp.Features.Users.UserInfo.DTOs.Request;

public sealed class UpdateAvatarRequest
{
    public IFormFile Image { get; set; } = default!;
}