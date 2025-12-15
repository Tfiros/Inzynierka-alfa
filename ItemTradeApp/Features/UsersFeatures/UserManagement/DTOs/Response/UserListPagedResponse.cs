using ItemTradeApp.Features.Shared.DTOs;

namespace ItemTradeApp.Features.UsersFeature.UserManagement.DTOs.Response;

public sealed class UserListPagedResponse : PagedResponse<UserListItemDTO>
{
    public int RegisteredLastMonthCount { get; set; }
    public int MiddlemenCount { get; set; }
}