namespace ItemTradeApp.Features.Users.UserSettings.DTOs;

public sealed class UserDataUpdateRequest
{
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}