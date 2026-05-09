using ItemTradeApp.Features.Chat.DTOs;
using ItemTradeApp.Persistence.Models;
using Microsoft.AspNetCore.SignalR;

namespace ItemTradeApp.Features.Chat.Helpers;
public interface IChatRealtimePublisher
{
    Task PublishMessageUpdatedAsync(ChatMessage message, CancellationToken ct);
    Task PublishMessageDeletedAsync(long messageId, int chatId, CancellationToken ct);
    Task PublishThreadReadAsync(string auth0UserId, int chatId, long lastReadMessageId, int unreadCount, CancellationToken ct);
    Task PublishThreadUpdatedToMembersAsync(int chatId, ChatMessage lastMessage, CancellationToken ct);

    Task PublishChatClosedAsync(int chatConversationId, DateTime closedAtUtc,
        IReadOnlyCollection<string> memberAuth0Ids, CancellationToken ct);
    Task PublishChatCreatedAsync(int chatConversationId, int tradeId,
        IReadOnlyCollection<string> memberAuth0Ids, CancellationToken ct);
}
public sealed class ChatRealtimePublisher : IChatRealtimePublisher
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly IChatRepository _repo;

    public ChatRealtimePublisher(IHubContext<ChatHub> hub, IChatRepository repo)
    {
        _hub = hub;
        _repo = repo;
    }

    public async Task PublishMessageUpdatedAsync(ChatMessage message, CancellationToken ct)
    {
        await _hub.Clients.Group($"chat:{message.ChatConversationId}")
            .SendAsync("chat.message.updated", new
            {
                id = message.Id,
                chatConversationId = message.ChatConversationId,
                message = message.Message,
                editedAtUtc = message.EditedAt
            }, ct);
    }

    public async Task PublishMessageDeletedAsync(long messageId, int chatId, CancellationToken ct)
    {
        await _hub.Clients.Group($"chat:{chatId}")
            .SendAsync("chat.message.deleted", new { messageId }, ct);
    }

    public async Task PublishThreadReadAsync(
        string auth0UserId,
        int chatId,
        long lastReadMessageId,
        int unreadCount,
        CancellationToken ct)
    {
        await _hub.Clients.Group($"user:{auth0UserId}")
            .SendAsync("chat.thread.read", new
            {
                chatConversationId = chatId,
                lastReadMessageId,
                unreadCount
            }, ct);
    }

    public async Task PublishThreadUpdatedToMembersAsync(
        int chatId,
        ChatMessage lastMessage,
        CancellationToken ct)
    {
        var members = await _repo.GetMemberAuth0Async(chatId, ct);

        foreach (var (userId, auth0) in members)
        {
            var unread = await _repo.GetUnreadCountForUserAsync(chatId, userId, ct);

            var dto = new ChatThreadUpdatedDto(
                ChatConversationId: chatId,
                LastMessageId: lastMessage.Id,
                LastMessageText: lastMessage.DeletedAt != null ? "[deleted]" : lastMessage.Message,
                LastMessageSenderId: lastMessage.SenderId,
                LastMessageCreatedAtUtc: lastMessage.CreatedAt,
                UnreadCount: unread
            );

            await _hub.Clients.Group($"user:{auth0}")
                .SendAsync("chat.thread.updated", dto, ct);
        }
    }

    public Task PublishChatClosedAsync(int chatConversationId, DateTime closedAtUtc,
        IReadOnlyCollection<string> memberAuth0Ids, CancellationToken ct)
    {
        var groups = memberAuth0Ids.Select(m => $"user:{m}").ToArray();
        var response = new { chatConversationId, closedAtUtc };
        return _hub.Clients.Groups(groups).SendAsync("chat.closed", response, ct);
    }

    public Task PublishChatCreatedAsync(int chatConversationId, int tradeId,
        IReadOnlyCollection<string> memberAuth0Ids, CancellationToken ct)
    {
        var groups = memberAuth0Ids.Select(m => $"user:{m}").ToArray();
        var response = new { chatConversationId, tradeId };
        return _hub.Clients.Groups(groups).SendAsync("chat.created", response, ct);
    }
}