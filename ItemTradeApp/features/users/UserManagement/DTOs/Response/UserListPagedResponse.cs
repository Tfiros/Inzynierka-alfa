using ItemTradeApp.Features.Shared.DTOs;

namespace ItemTradeApp.Features.Users.UserManagement.DTOs.Response;

public sealed class UserListPagedResponse : PagedResponse<UserListItemDTO>
{
    public int RegisteredLastMonthCount { get; set; }
    public int MiddlemenCount { get; set; }
    public int TotalUsers { get; set; }
}