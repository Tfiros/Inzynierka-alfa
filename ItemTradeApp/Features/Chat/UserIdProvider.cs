using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace ItemTradeApp.Features.Chat;

public sealed class Auth0UserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirstValue("sub")
           ?? connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}