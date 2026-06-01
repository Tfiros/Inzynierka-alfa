namespace ItemTradeApp.Features.Users.UserManagement.DTOs.Request;

public class UpdateUserRequest
{
    public string? Nickname { get; set; }
    public string? AuthZeroUserId { get; set; }
    public string? ProfileDescription { get; set; }
    public string? Email { get; set; }
    public string? NewPassword { get; set; }
    public int? Tokens { get; set; }
    public List<string>? Roles { get; set; }
}