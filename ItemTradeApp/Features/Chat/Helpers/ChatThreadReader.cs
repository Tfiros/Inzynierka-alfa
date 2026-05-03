using ItemTradeApp.Features.Chat.DTOs;
using ItemTradeApp.Features.Chat.Helpers;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace ItemTradeApp.Features.Chat.Helpers;

public interface IChatThreadsReader
{
    Task<IReadOnlyList<ChatThreadListItemDto>> GetThreadsAsync(
        int userId,
        int page,
        int pageSize,
        string? search,
        CancellationToken ct);

    Task<IReadOnlyList<ChatThreadListItemDto>> GetChatsForTradeAsync(int userId, int tradeId, CancellationToken ct);
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
        var myChatsQ = _db.ConversationMembers
            .Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.ChatConversationId,
                m.LastReadMessageId
            });

        var lastMsgQ = _db.ChatMessages
            .Where(msg => msg.DeletedAt == null)
            .GroupBy(msg => msg.ChatConversationId)
            .Select(g => new
            {
                ChatId = g.Key,
                LastId = (long?)g.Max(x => x.Id)
            });

        var baseQ = myChatsQ
            .GroupJoin(
                lastMsgQ,
                mc => mc.ChatConversationId,
                lm => lm.ChatId,
                (mc, lmj) => new { mc, lmj }
            )
            .SelectMany(
                x => x.lmj.DefaultIfEmpty(),
                (x,lm) => 
                    new {x.mc.ChatConversationId, 
                        x.mc.LastReadMessageId, 
                        LastId = lm == null ? null : lm.LastId}
            );

        var rows = await baseQ
            .OrderByDescending(x => x.LastId ?? 0L)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var pageRows = rows.Select(x => new ThreadPageRow(x.ChatConversationId, x.LastReadMessageId, x.LastId))
            .ToList();
        return await BuildThreadItemsAsync(userId, pageRows, search, ct);
    }

    public async Task<IReadOnlyList<ChatThreadListItemDto>> GetChatsForTradeAsync(int userId, int tradeId,
        CancellationToken ct)
    {
        if (tradeId <= 0)
        {
            return Array.Empty<ChatThreadListItemDto>();
        }

        var myChatsQ = _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.ChatConversation.TradeId == tradeId && !m.ChatConversation.IsDeleted)
            .Select(m => new {m.ChatConversationId, m.LastReadMessageId});

        var lastMsqQ = _db.ChatMessages
            .AsNoTracking().Where(msg => msg.DeletedAt == null)
            .GroupBy(msg => msg.ChatConversationId)
            .Select(g => new { ChatId = g.Key, LastId = (long?)g.Max(x => x.Id) });

        var rows = await myChatsQ.GroupJoin(lastMsqQ, mc => mc.ChatConversationId, lm => lm.ChatId,
                (mc, lmj) => new { mc, lmj })
            .SelectMany(x => x.lmj.DefaultIfEmpty(),
                (x,lm) => new {x.mc.ChatConversationId, x.mc.LastReadMessageId, LastId = lm == null ? null : lm.LastId})
            .OrderBy(x => x.LastId ?? 0L).ToListAsync(ct);
        var pagedRows = rows.Select(x => new ThreadPageRow(x.ChatConversationId, x.LastReadMessageId, x.LastId))
            .ToList();
        return await BuildThreadItemsAsync(userId, pagedRows, search: null, ct);
    }

    private sealed record ThreadPageRow(int ChatConversationId, long? LastReadMessageId, long? LastId);

    private async Task<IReadOnlyList<ChatThreadListItemDto>> BuildThreadItemsAsync(int userId,
        IReadOnlyList<ThreadPageRow> pageRows, string? search, CancellationToken ct)
    {
        
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
                TradeId = c.TradeId,
                ClosedAt = c.ClosedAt,
                BuyerUserId = c.Trade.Customer_ID,
                SellerUserId = c.Trade.User_ID,
                MiddlemanUserId = c.Trade.MiddlemanUser_ID,
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
                    (c.TradeId.ToString().Contains(searchValue, StringComparison.OrdinalIgnoreCase)) ||
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

            int? otherUserId = null;
            string? otherUserAuth0 = null;
            string? otherUserNickname = null;
            string? otherUserTradeRole = null;
            string? avatarUrl = null;
            bool? isOnline = null;
            var other = members.FirstOrDefault(x => x.UserId != userId);
            otherUserId = other?.UserId;
            otherUserAuth0 = other?.Auth0UserId;
            otherUserNickname = other?.DisplayName;
            otherUserTradeRole = otherUserId switch
            {
                var id when id == conversation.BuyerUserId => "Buyer",
                var id when id == conversation.SellerUserId => "Seller",
                var id when id == conversation.MiddlemanUserId => "Middleman",
                _ => null
            };
            avatarUrl = other?.AvatarUrl;
            if (!string.IsNullOrWhiteSpace(otherUserAuth0))
            {
                isOnline = _presence.IsOnline(otherUserAuth0);
            }
            var unreadCount = unreadByChat.TryGetValue(row.ChatConversationId, out var unread)
                ? unread
                : 0;

            result.Add(new ChatThreadListItemDto(
                ChatConversationId: row.ChatConversationId,
                OtherUserId: otherUserId,
                OtherUserAuth0UserId: otherUserAuth0,
                OtherUserNickname: otherUserNickname,
                OtherUserTradeRole: otherUserTradeRole,
                AvatarUrl: avatarUrl,
                IsOnline: isOnline,
                LastMessageId: lastMessage?.Id,
                LastMessageText: lastMessage?.DeletedAt != null ? "[deleted]" : lastMessage?.Message,
                LastMessageSenderId: lastMessage?.SenderId,
                LastMessageCreatedAtUtc: lastMessage?.CreatedAt,
                UnreadCount: unreadCount,
                TradeId: conversation.TradeId,
                ClosedAtUtc: conversation.ClosedAt
            ));
        }

        return result;
    }
    
    
    private sealed class ConversationProjection
    {
        public int Id { get; init; }
        public int TradeId { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int BuyerUserId { get; set; }
        public int SellerUserId { get; set; }
        public int? MiddlemanUserId { get; set; }
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