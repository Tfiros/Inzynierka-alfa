using ItemTradeApp.Features.Chat.DTOs;
using ItemTradeApp.Features.Chat.Helpers;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Chat.Helpers;

public interface IChatThreadsReader
{
    Task<IReadOnlyList<ChatThreadListItemDto>> GetThreadsAsync(
        int userId,
        int page,
        int pageSize,
        string? search,
        CancellationToken ct);
}
public sealed class ChatThreadsReader : IChatThreadsReader
{
    private readonly AppDbContext _db;
    private readonly PresenceTracker _presence;

    public ChatThreadsReader(AppDbContext db, PresenceTracker presence)
    {
        _db = db;
        _presence = presence;
    }

    public async Task<IReadOnlyList<ChatThreadListItemDto>> GetThreadsAsync(
        int userId,
        int page,
        int pageSize,
        string? search,
        CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var myChatsQ =
            from m in _db.ConversationMembers
            where m.UserId == userId
            select new
            {
                m.ChatConversationId,
                m.LastReadMessageId
            };

        var lastMsgQ =
            from msg in _db.ChatMessages
            where msg.DeletedAt == null
            group msg by msg.ChatConversationId into g
            select new
            {
                ChatId = g.Key,
                LastId = (long?)g.Max(x => x.Id)
            };

        var baseQ =
            from mc in myChatsQ
            join lm in lastMsgQ on mc.ChatConversationId equals lm.ChatId into lmj
            from lm in lmj.DefaultIfEmpty()
            select new
            {
                mc.ChatConversationId,
                mc.LastReadMessageId,
                LastId = lm == null ? (long?)null : lm.LastId
            };

        var pageRows = await baseQ
            .OrderByDescending(x => x.LastId ?? 0L)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var chatIds = pageRows
            .Select(x => x.ChatConversationId)
            .Distinct()
            .ToArray();

        if (chatIds.Length == 0)
            return Array.Empty<ChatThreadListItemDto>();

        var conversations = await _db.ChatConversations
            .AsNoTracking()
            .Where(c => chatIds.Contains(c.Id) && !c.IsDeleted)
            .Select(c => new ConversationProjection
            {
                Id = c.Id,
                Name = c.Name,
                Members = c.Members.Select(m => new ConversationMemberProjection
                {
                    UserId = m.UserId,
                    Auth0UserId = m.User.Auth0UserID,
                    DisplayName = m.User.ProfileInfo.Nickname,
                    AvatarUrl = m.User.ProfileInfo.ImageUrl
                }).ToList()
            })
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchValue = search.Trim();

            conversations = conversations
                .Where(c =>
                    (!string.IsNullOrWhiteSpace(c.Name) &&
                     c.Name.Contains(searchValue, StringComparison.OrdinalIgnoreCase)) ||
                    c.Members.Any(m =>
                        (!string.IsNullOrWhiteSpace(m.DisplayName) &&
                         m.DisplayName.Contains(searchValue, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(m.Auth0UserId) &&
                         m.Auth0UserId.Contains(searchValue, StringComparison.OrdinalIgnoreCase))))
                .ToList();
        }

        var allowedChatIds = conversations.Select(c => c.Id).ToHashSet();

        var filteredPageRows = pageRows
            .Where(x => allowedChatIds.Contains(x.ChatConversationId))
            .ToList();

        if (filteredPageRows.Count == 0)
            return Array.Empty<ChatThreadListItemDto>();

        var filteredChatIds = filteredPageRows
            .Select(x => x.ChatConversationId)
            .Distinct()
            .ToArray();

        var convById = conversations.ToDictionary(x => x.Id);

        var lastIds = filteredPageRows
            .Select(x => x.LastId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();

        var lastMessages = lastIds.Length == 0
            ? new Dictionary<int, ChatMessage>()
            : await _db.ChatMessages
                .AsNoTracking()
                .Where(m => lastIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.ChatConversationId, m => m, ct);

        var lastReadByChat = filteredPageRows.ToDictionary(
            x => x.ChatConversationId,
            x => x.LastReadMessageId);

        var unreadRaw = await _db.ChatMessages
            .AsNoTracking()
            .Where(m =>
                filteredChatIds.Contains(m.ChatConversationId) &&
                m.DeletedAt == null &&
                m.SenderId != userId)
            .Select(m => new
            {
                m.ChatConversationId,
                m.Id
            })
            .ToListAsync(ct);

        var unreadByChat = unreadRaw
            .GroupBy(x => x.ChatConversationId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    lastReadByChat.TryGetValue(g.Key, out var lastRead);

                    if (lastRead is null)
                        return g.Count();

                    return g.Count(x => x.Id > lastRead.Value);
                });

        var result = new List<ChatThreadListItemDto>(filteredPageRows.Count);

        foreach (var row in filteredPageRows)
        {
            if (!convById.TryGetValue(row.ChatConversationId, out var conversation))
                continue;

            lastMessages.TryGetValue(row.ChatConversationId, out var lastMessage);

            var members = conversation.Members;
            var isGroup = members.Count > 2;

            int? otherUserId = null;
            string? otherUserAuth0 = null;
            string? avatarUrl = null;
            bool? isOnline = null;
            string displayName;

            if (isGroup)
            {
                displayName = string.IsNullOrWhiteSpace(conversation.Name)
                    ? "Grupa"
                    : conversation.Name!;
            }
            else
            {
                var other = members.FirstOrDefault(x => x.UserId != userId);

                otherUserId = other?.UserId;
                otherUserAuth0 = other?.Auth0UserId;
                avatarUrl = other?.AvatarUrl;
                displayName = other?.DisplayName ?? "Użytkownik";

                if (!string.IsNullOrWhiteSpace(otherUserAuth0))
                    isOnline = _presence.IsOnline(otherUserAuth0);
            }

            var unreadCount = unreadByChat.TryGetValue(row.ChatConversationId, out var unread)
                ? unread
                : 0;

            result.Add(new ChatThreadListItemDto(
                ChatConversationId: row.ChatConversationId,
                IsGroup: isGroup,
                DisplayName: displayName,
                OtherUserId: otherUserId,
                OtherUserAuth0UserId: otherUserAuth0,
                AvatarUrl: avatarUrl,
                IsOnline: isOnline,
                LastMessageId: lastMessage?.Id,
                LastMessageText: lastMessage?.DeletedAt != null ? "[deleted]" : lastMessage?.Message,
                LastMessageSenderId: lastMessage?.SenderId,
                LastMessageCreatedAtUtc: lastMessage?.CreatedAt,
                UnreadCount: unreadCount
            ));
        }

        return result;
    }

    private sealed class ConversationProjection
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public List<ConversationMemberProjection> Members { get; init; } = new();
    }

    private sealed class ConversationMemberProjection
    {
        public int UserId { get; init; }
        public string Auth0UserId { get; init; } = string.Empty;
        public string? DisplayName { get; init; }
        public string? AvatarUrl { get; init; }
    }
}