namespace ItemTradeApp.Features.Users.UserManagement.DTOs;

public sealed class UserListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public string? SearchText { get; set; }
    public byte OrderBy { get; set; }

    public string? Role { get; set; }
    public DateTime? RegisteredFrom { get; set; }
    public DateTime? RegisteredTo { get; set; }
}