using ItemTradeApp.Features.Auth;
using ItemTradeApp.Features.Chat.DTOs;
using ItemTradeApp.Features.Trades;
using ItemTradeApp.Persistence.Models;
using Microsoft.AspNetCore.SignalR;

namespace ItemTradeApp.Features.Chat;

public interface IChatService
{
    Task<Result<CreateDmChatResponse>> CreateDmAsync(int otherUserId, string? auth0UserId, CancellationToken ct);

    Task<Result<IReadOnlyList<ChatThreadListItemDto>>> GetThreadsAsync(
        int page,
        int pageSize,
        string? search,
        string? auth0UserId,
        CancellationToken ct);

    Task<Result<IReadOnlyList<ChatMessageDto>>> GetMessagesAsync(
        int chatId,
        long? beforeMessageId,
        int pageSize,
        string? auth0UserId,
        CancellationToken ct);
    

    Task<Result<ChatMessageDto>> EditMessageAsync(
        long messageId,
        EditMessageRequest? request,
        string? auth0UserId,
        CancellationToken ct);

    Task<Result<string>> DeleteMessageAsync(
        long messageId,
        string? auth0UserId,
        CancellationToken ct);

    Task<Result<ChatReadStateDto>> MarkReadAsync(
        int chatId,
        MarkReadRequest? request,
        string? auth0UserId,
        CancellationToken ct);
    Task<ChatMessageDto> AddMessageAsync(int chatConversationId, string auth0UserId, string message, CancellationToken ct = default);

}

public sealed class ChatService(
    IChatRepository repo,
    IUserContext userContext,
    IHubContext<ChatHub> hub,
    PresenceTracker presence
) : IChatService
{
    public async Task<Result<CreateDmChatResponse>> CreateDmAsync(int otherUserId, string? auth0UserId, CancellationToken ct)
    {
        if (otherUserId <= 0)
            return Result<CreateDmChatResponse>.BadRequest("otherUserId must be > 0.");

        var meTry = await TryGetUser(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<CreateDmChatResponse>.Unauthorized(meTry.Error);

        var me = meTry.User!;
        if (me.ID == otherUserId)
            return Result<CreateDmChatResponse>.BadRequest("cannot_dm_self");

        // opcjonalnie: sprawdź czy other user istnieje
        // (u Ciebie IUserContext ma GetRequiredUserAsync, więc bierzemy repo mapping tylko dla 'me')
        // Jeżeli chcesz twardo: możesz pobrać other user przez userContext też, ale to zależy od Twoich metod.

        var chatId = await repo.GetOrCreateDmAsync(me.ID, otherUserId, ct);

        return Result<CreateDmChatResponse>.Success(new CreateDmChatResponse(chatId), "Chat created.");
    }
    public async Task<ChatMessageDto> AddMessageAsync(
        int chatConversationId,
        string auth0UserId,
        string message,
        CancellationToken ct = default)
    {
        if (chatConversationId <= 0)
            throw new ArgumentException("invalid_chat_id");

        message = message?.Trim() ?? "";
        if (message.Length == 0)
            throw new ArgumentException("message_empty");
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;
        var senderId = await repo.GetUserIdByAuth0Async(trimmedAuth0UserId, ct);
        if (!senderId.HasValue)
            throw new InvalidOperationException("sender_not_found");

        var chatExists = await repo.ChatExistsAsync(chatConversationId, ct);
        if (!chatExists)
            throw new KeyNotFoundException("chat_not_found");

        return await repo.AddMessageAsync(chatConversationId, senderId.Value, message, ct);
    }
    public async Task<Result<IReadOnlyList<ChatThreadListItemDto>>> GetThreadsAsync(
        int page,
        int pageSize,
        string? search,
        string? auth0UserId,
        CancellationToken ct)
    {
        var meTry = await TryGetUser(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<IReadOnlyList<ChatThreadListItemDto>>.Unauthorized(meTry.Error);

        var me = meTry.User!;

        // search na razie ignorowane w repo (można dopisać później)
        var threads = await repo.GetThreadsAsync(me.ID, page, pageSize, ct);

        // uzupełnij online dla DM
        var enriched = threads.Select(t =>
        {
            if (!t.IsGroup && !string.IsNullOrWhiteSpace(t.OtherUserAuth0UserId))
                return t with { IsOnline = presence.IsOnline(t.OtherUserAuth0UserId!) };
            return t;
        }).ToList();

        return Result<IReadOnlyList<ChatThreadListItemDto>>.Success(enriched, "Successfully retrieved.");
    }

    public async Task<Result<IReadOnlyList<ChatMessageDto>>> GetMessagesAsync(
        int chatId,
        long? beforeMessageId,
        int pageSize,
        string? auth0UserId,
        CancellationToken ct)
    {
        if (chatId <= 0)
            return Result<IReadOnlyList<ChatMessageDto>>.BadRequest("chatId must be > 0.");

        var meTry = await TryGetUser(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<IReadOnlyList<ChatMessageDto>>.Unauthorized(meTry.Error);

        var me = meTry.User!;

        var isMember = await repo.IsMemberAsync(chatId, me.ID, ct);
        if (!isMember)
            return Result<IReadOnlyList<ChatMessageDto>>.Forbidden("not_member");

        var rows = await repo.GetMessagesAsync(chatId, beforeMessageId, pageSize, ct);
        return Result<IReadOnlyList<ChatMessageDto>>.Success(rows, "Successfully retrieved.");
    }

    /*public async Task<Result<ChatMessageDto>> SendMessageAsync(
        int chatId,
        SendMessageRequest? request,
        string? auth0UserId,
        CancellationToken ct)
    {
        if (chatId <= 0)
            return Result<ChatMessageDto>.BadRequest("chatId must be > 0.");

        if (request is null)
            return Result<ChatMessageDto>.BadRequest("Body is required.");

        if (string.IsNullOrWhiteSpace(request.Message))
            return Result<ChatMessageDto>.BadRequest("message_required");

        var meTry = await TryGetUser(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<ChatMessageDto>.Unauthorized(meTry.Error);

        var me = meTry.User!;

        var isMember = await repo.IsMemberAsync(chatId, me.ID, ct);
        if (!isMember)
            return Result<ChatMessageDto>.Forbidden("not_member");

        var saved = await repo.AddMessageAsync(chatId, me.ID, request.Message.Trim(), ct);

        var dto = new ChatMessageDto(
            saved.Id,
            saved.ChatConversationId,
            saved.SenderId,
            saved.Message,
            saved.CreatedAt,
            saved.EditedAt
            );

        // realtime: widok rozmowy (toast)
        await hub.Clients.Group($"chat:{chatId}")
            .SendAsync("chat.message.created", dto, ct);

        // realtime: dropdown (user stream)
        await PublishThreadUpdatedToMembersAsync(chatId, saved, ct);

        return Result<ChatMessageDto>.Success(dto, "Message sent.");
    }*/

    public async Task<Result<ChatMessageDto>> EditMessageAsync(
        long messageId,
        EditMessageRequest? request,
        string? auth0UserId,
        CancellationToken ct)
    {
        if (messageId <= 0)
            return Result<ChatMessageDto>.BadRequest("messageId must be > 0.");

        if (request is null)
            return Result<ChatMessageDto>.BadRequest("Body is required.");

        if (string.IsNullOrWhiteSpace(request.Message))
            return Result<ChatMessageDto>.BadRequest("message_required");

        var meTry = await TryGetUser(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<ChatMessageDto>.Unauthorized(meTry.Error);

        var me = meTry.User!;

        var msg = await repo.GetMessageForEditAsync(messageId, me.ID, ct);
        if (msg is null)
            return Result<ChatMessageDto>.NotFound("message_not_found");

        msg.Message = request.Message.Trim();
        msg.EditedAt = DateTime.UtcNow;

        await repo.SaveChangesAsync(ct);

        // realtime: update w rozmowie
        await hub.Clients.Group($"chat:{msg.ChatConversationId}")
            .SendAsync("chat.message.updated", new
            {
                id = msg.Id,
                chatConversationId = msg.ChatConversationId,
                message = msg.Message,
                editedAtUtc = msg.EditedAt
            }, ct);

        await PublishThreadUpdatedToMembersAsync(msg.ChatConversationId, msg, ct);

        var dto = new ChatMessageDto(
            msg.Id,
            msg.ChatConversationId,
            msg.SenderId,
            msg.Message,
            msg.CreatedAt,
            msg.EditedAt
            );

        return Result<ChatMessageDto>.Success(dto, "Message updated.");
    }

    public async Task<Result<string>> DeleteMessageAsync(long messageId, string? auth0UserId, CancellationToken ct)
    {
        if (messageId <= 0)
            return Result<string>.BadRequest("messageId must be > 0.");

        var meTry = await TryGetUser(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<string>.Unauthorized(meTry.Error);

        var me = meTry.User!;

        var msg = await repo.GetMessageForEditAsync(messageId, me.ID, ct);
        if (msg is null)
            return Result<string>.NotFound("message_not_found");

        await repo.SoftDeleteMessageAsync(msg, ct);

        await hub.Clients.Group($"chat:{msg.ChatConversationId}")
            .SendAsync("chat.message.deleted", new { messageId }, ct);

        await PublishThreadUpdatedToMembersAsync(msg.ChatConversationId, msg, ct);

        return Result<string>.Success("Message deleted.");
    }

    public async Task<Result<ChatReadStateDto>> MarkReadAsync(
        int chatId,
        MarkReadRequest? request,
        string? auth0UserId,
        CancellationToken ct)
    {
        if (chatId <= 0)
            return Result<ChatReadStateDto>.BadRequest("chatId must be > 0.");

        if (request is null)
            return Result<ChatReadStateDto>.BadRequest("Body is required.");

        if (request.LastReadMessageId <= 0)
            return Result<ChatReadStateDto>.BadRequest("lastReadMessageId must be > 0.");

        var meTry = await TryGetUser(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<ChatReadStateDto>.Unauthorized(meTry.Error);

        var me = meTry.User!;

        var isMember = await repo.IsMemberAsync(chatId, me.ID, ct);
        if (!isMember)
            return Result<ChatReadStateDto>.Forbidden("not_member");

        await repo.MarkReadAsync(chatId, me.ID, request.LastReadMessageId, ct);

        var unread = await repo.GetUnreadCountAsync(chatId, me.ID, ct);

        var dto = new ChatReadStateDto(
            ChatConversationId: chatId,
            LastReadMessageId: request.LastReadMessageId,
            MarkedAtUtc: DateTime.UtcNow,
            UnreadCount: unread);

        // realtime: odśwież dropdown dla tego usera
        if (!string.IsNullOrWhiteSpace(auth0UserId))
        {
            await hub.Clients.Group($"user:{auth0UserId}")
                .SendAsync("chat.thread.read", new
                {
                    chatConversationId = chatId,
                    lastReadMessageId = request.LastReadMessageId,
                    unreadCount = unread
                }, ct);
        }

        return Result<ChatReadStateDto>.Success(dto, "Marked as read.");
    }

    private async Task PublishThreadUpdatedToMembersAsync(int chatId, ChatMessage lastMsg, CancellationToken ct)
    {
        var members = await repo.GetMemberAuth0Async(chatId, ct); // (UserId, Auth0)

        foreach (var (userId, auth0) in members)
        {
            var unread = await repo.GetUnreadCountAsync(chatId, userId, ct);

            var upd = new ChatThreadUpdatedDto(
                ChatConversationId: chatId,
                LastMessageId: lastMsg.Id,
                LastMessageText: lastMsg.DeletedAt != null ? "[deleted]" : lastMsg.Message,
                LastMessageSenderId: lastMsg.SenderId,
                LastMessageCreatedAtUtc: lastMsg.CreatedAt,
                UnreadCount: unread
            );

            await hub.Clients.Group($"user:{auth0}")
                .SendAsync("chat.thread.updated", upd, ct);
        }
    }

    private async Task<(Persistence.Models.User? User, string? Error)> TryGetUser(string? auth0UserId, CancellationToken ct)
    {
        try
        {
            var user = await userContext.GetRequiredUserAsync(auth0UserId, ct);
            return user is null ? (null, "User not found") : (user, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }
}
