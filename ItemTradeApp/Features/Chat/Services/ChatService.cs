using ItemTradeApp.Features.Chat.DTOs;
using ItemTradeApp.Features.Chat.Helpers;
using ItemTradeApp.Features.Chat.Services;
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

public sealed class ChatService : IChatService
{
    private readonly IChatRepository _repo;
    private readonly IChatDmService _chatDmService;
    private readonly IChatThreadsReader _chatThreadsReader;
    private readonly IChatReadStateService _chatReadStateService;
    private readonly IChatRealtimePublisher _chatRealtimePublisher;
    private readonly IChatUserResolver _chatUserResolver;

    public ChatService(
        IChatRepository repo,
        IChatDmService chatDmService,
        IChatThreadsReader chatThreadsReader,
        IChatReadStateService chatReadStateService,
        IChatRealtimePublisher chatRealtimePublisher,
        IChatUserResolver chatUserResolver)
        
    {
        _repo = repo;
        _chatDmService = chatDmService;
        _chatThreadsReader = chatThreadsReader;
        _chatReadStateService = chatReadStateService;
        _chatRealtimePublisher = chatRealtimePublisher;
        _chatUserResolver = chatUserResolver;
    }

    public async Task<Result<CreateDmChatResponse>> CreateDmAsync(int otherUserId, string? auth0UserId, CancellationToken ct)
    {
        if (otherUserId <= 0)
            return Result<CreateDmChatResponse>.BadRequest("otherUserId must be > 0.");

        var meTry = await _chatUserResolver.TryGetUserAsync(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<CreateDmChatResponse>.Unauthorized(meTry.Error);

        var me = meTry.User!;
        if (me.ID == otherUserId)
            return Result<CreateDmChatResponse>.BadRequest("cannot_dm_self");

        var chatId = await _chatDmService.GetOrCreateDmAsync(me.ID, otherUserId, ct);

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

        message = message?.Trim() ?? string.Empty;
        if (message.Length == 0)
            throw new ArgumentException("message_empty");

        var trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;

        var senderId = await _repo.GetUserIdByAuth0Async(trimmedAuth0UserId, ct);
        if (!senderId.HasValue)
            throw new InvalidOperationException("sender_not_found");

        var chatExists = await _repo.ChatExistsAsync(chatConversationId, ct);
        if (!chatExists)
            throw new KeyNotFoundException("chat_not_found");

        var dto = await _repo.AddMessageAsync(chatConversationId, senderId.Value, message, ct);

        var msg = await _repo.GetMessageByIdAsync(dto.Id, ct);
        if (msg is not null)
        {
            await _chatRealtimePublisher.PublishThreadUpdatedToMembersAsync(chatConversationId, msg, ct);
        }

        return dto;
    }

    public async Task<Result<IReadOnlyList<ChatThreadListItemDto>>> GetThreadsAsync(
        int page,
        int pageSize,
        string? search,
        string? auth0UserId,
        CancellationToken ct)
    {
        var meTry = await _chatUserResolver.TryGetUserAsync(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<IReadOnlyList<ChatThreadListItemDto>>.Unauthorized(meTry.Error);

        var me = meTry.User!;

        var threads = await _chatThreadsReader.GetThreadsAsync(me.ID, page, pageSize, search, ct);

        return Result<IReadOnlyList<ChatThreadListItemDto>>.Success(threads, "Successfully retrieved.");
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

        var meTry = await _chatUserResolver.TryGetUserAsync(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<IReadOnlyList<ChatMessageDto>>.Unauthorized(meTry.Error);

        var me = meTry.User!;

        var isMember = await _repo.IsMemberAsync(chatId, me.ID, ct);
        if (!isMember)
            return Result<IReadOnlyList<ChatMessageDto>>.Forbidden("not_member");

        var rows = await _repo.GetMessagesAsync(chatId, beforeMessageId, pageSize, ct);
        return Result<IReadOnlyList<ChatMessageDto>>.Success(rows, "Successfully retrieved.");
    }

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

        var meTry = await _chatUserResolver.TryGetUserAsync(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<ChatMessageDto>.Unauthorized(meTry.Error);

        var me = meTry.User!;

        var msg = await _repo.GetMessageForEditAsync(messageId, me.ID, ct);
        if (msg is null)
            return Result<ChatMessageDto>.NotFound("message_not_found");

        msg.Message = request.Message.Trim();
        msg.EditedAt = DateTime.UtcNow;

        await _repo.SaveChangesAsync(ct);

        await _chatRealtimePublisher.PublishMessageUpdatedAsync(msg, ct);
        await _chatRealtimePublisher.PublishThreadUpdatedToMembersAsync(msg.ChatConversationId, msg, ct);

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

        var meTry = await _chatUserResolver.TryGetUserAsync(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<string>.Unauthorized(meTry.Error);

        var me = meTry.User!;

        var msg = await _repo.GetMessageForEditAsync(messageId, me.ID, ct);
        if (msg is null)
            return Result<string>.NotFound("message_not_found");

        await _repo.SoftDeleteMessageAsync(msg, ct);

        await _chatRealtimePublisher.PublishMessageDeletedAsync(messageId, msg.ChatConversationId, ct);
        await _chatRealtimePublisher.PublishThreadUpdatedToMembersAsync(msg.ChatConversationId, msg, ct);

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

        var meTry = await _chatUserResolver.TryGetUserAsync(auth0UserId, ct);
        if (meTry.Error is not null)
            return Result<ChatReadStateDto>.Unauthorized(meTry.Error);

        var me = meTry.User!;

        var isMember = await _repo.IsMemberAsync(chatId, me.ID, ct);
        if (!isMember)
            return Result<ChatReadStateDto>.Forbidden("not_member");

        var unread = await _chatReadStateService.MarkReadAsync(chatId, me.ID, request.LastReadMessageId, ct);

        var dto = new ChatReadStateDto(
            ChatConversationId: chatId,
            LastReadMessageId: request.LastReadMessageId,
            MarkedAtUtc: DateTime.UtcNow,
            UnreadCount: unread);

        if (!string.IsNullOrWhiteSpace(auth0UserId))
        {
            await _chatRealtimePublisher.PublishThreadReadAsync(
                auth0UserId,
                chatId,
                request.LastReadMessageId,
                unread,
                ct);
        }

        return Result<ChatReadStateDto>.Success(dto, "Marked as read.");
    }
}
