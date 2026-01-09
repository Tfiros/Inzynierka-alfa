using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ItemTradeApp.Features.EmailsNotifications.Notifications;

public sealed class NotificationsHub(IUserIdentityRepository identityRepo,
    ILogger<NotificationsHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var auth0UserId = Context.User?.FindFirst("sub")?.Value
                          ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;


        if (!string.IsNullOrWhiteSpace(auth0UserId))
        {
            string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
                ? auth0UserId.Substring("auth0|".Length)
                : auth0UserId;
            
            var userId = await identityRepo.GetUserIdByAuth0IdAsync(trimmedAuth0UserId, Context.ConnectionAborted);
            if (userId is not null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId.Value}");
            }
        }

        await base.OnConnectedAsync();
    }
}