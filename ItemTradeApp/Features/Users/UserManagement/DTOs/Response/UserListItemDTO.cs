namespace ItemTradeApp.Features.Users.UserManagement.DTOs.Response;

public class UserListItemDTO
{
    public string Auth0UserId { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Name { get; set; }
    public DateOnly? RegisteredAt { get; set; }
    public List<string> Roles { get; set; } = new();
}