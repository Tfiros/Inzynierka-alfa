using ItemTradeApp.Features.Users.UserManagement.Enums;

namespace ItemTradeApp.Features.Users.UserManagement.DTOs.Request;

public sealed class UserListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public string? SearchText { get; set; }
    public UserListOrderBy OrderBy { get; set; } = UserListOrderBy.RegisteredAtDesc;

    public string? Role { get; set; }
    public DateTime? RegisteredFrom { get; set; }
    public DateTime? RegisteredTo { get; set; }
}