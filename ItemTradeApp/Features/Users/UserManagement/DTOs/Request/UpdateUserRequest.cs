namespace ItemTradeApp.Features.Users.UserManagement.DTOs;

public class UpdateUserRequest
{
    public string? Nickname { get; set; }
    public string? AuthZeroUserId { get; set; }
    public string? Email { get; set; }
    public string? NewPassword { get; set; }
    public List<string>? Roles { get; set; }
}