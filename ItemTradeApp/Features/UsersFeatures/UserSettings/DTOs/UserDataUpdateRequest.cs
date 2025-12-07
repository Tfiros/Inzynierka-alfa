namespace ItemTradeApp.Features.UsersFeature.UserSettings.DTOs;

public sealed class UserDataUpdateRequest
{
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}