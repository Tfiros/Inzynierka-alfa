using System.Security.Claims;
using ItemTradeApp.Features.Shared.Notifications.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace ItemTradeApp.Features.Shared.Notifications;

public sealed class NotificationsHub(IUserIdentityRepository identityRepo,
    ILogger<NotificationsHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var auth0UserId = Context.User?.FindFirst("sub")?.Value
                          ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrWhiteSpace(auth0UserId))
        {
            var trimmedAuth0UserId = Auth0IdHandler.Trim(auth0UserId);
            
            var userId = await identityRepo.GetUserIdByAuth0IdAsync(trimmedAuth0UserId, Context.ConnectionAborted);
            if (userId is not null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId.Value}");
            }
        }

        await base.OnConnectedAsync();
    }
}