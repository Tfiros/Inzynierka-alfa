using ItemTradeApp.Features.Chat.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ItemTradeApp.Features.Chat;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly PresenceTracker _presence;
    private readonly IChatService _chatService;

    public ChatHub(
        PresenceTracker presence,
        IChatService service)
    {
        _presence = presence;
        _chatService = service;
    }

    public override async Task OnConnectedAsync()
    {
        var auth0 = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(auth0))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{auth0}");

            var changed = _presence.UserConnected(auth0);
            if (changed)
            {
                await Clients.All.SendAsync("presence.changed", new
                {
                    auth0UserId = auth0,
                    isOnline = true
                });
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var auth0 = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(auth0))
        {
            var changed = _presence.UserDisconnected(auth0);
            if (changed)
            {
                await Clients.All.SendAsync("presence.changed", new
                {
                    auth0UserId = auth0,
                    isOnline = false
                });
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinChat(int chatConversationId)
    {
        var auth0UserId = Context.UserIdentifier;
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;
        if (!await _chatService.IsMemberAsync(chatConversationId, trimmedAuth0UserId, Context.ConnectionAborted))
        {
            throw new HubException("not_a_chat_member");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat:{chatConversationId}");
    }

    public async Task LeaveChat(int chatConversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat:{chatConversationId}");
    }
    public async Task SendMessage(int chatConversationId, string message)
    {
        if (chatConversationId <= 0)
            throw new HubException("invalid_chat_id");

        if (string.IsNullOrWhiteSpace(message))
            throw new HubException("message_empty");

        var auth0UserId = Context.UserIdentifier;
        if (string.IsNullOrWhiteSpace(auth0UserId))
            throw new HubException("unauthorized");

        var dto = await _chatService.AddMessageAsync(
            chatConversationId,
            auth0UserId,
            message.Trim(),
            Context.ConnectionAborted);

        await Clients.Group($"chat:{chatConversationId}")
            .SendAsync("message.new", dto, Context.ConnectionAborted);
    }
}