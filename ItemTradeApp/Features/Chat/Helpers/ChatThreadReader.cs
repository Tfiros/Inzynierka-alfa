using ItemTradeApp.Features.Chat.DTOs;
using ItemTradeApp.Features.Shared;
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


        var baseChats = _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            if (s.Length > 200)
            {
                s = s[..200];
            }

            foreach (var word in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var pattern = $"%{EscapePattern.Escape(word)}%";
                
                var searchHashNumber = word.StartsWith("#") ? word[1..] : word;
                int? searchNumber = int.TryParse(searchHashNumber, out var wordNumber) ? wordNumber : null;


                baseChats = baseChats.Where(cm =>
                    cm.ChatConversation.Members.Any(m =>
                        m.UserId != userId && m.User.ProfileInfo.Nickname != null &&
                        EF.Functions.ILike(m.User.ProfileInfo.Nickname, pattern, "!"))
                    || (searchNumber != null && cm.ChatConversation.TradeId == searchNumber)
                );
            }
        }

        var rows = await baseChats
            .Select(cm => new
            {
                cm.ChatConversationId,
                cm.LastReadMessageId,
                cm.ChatConversation.ClosedAt,
                LastId = _db.ChatMessages
                    .Where(x => x.ChatConversationId == cm.ChatConversationId && x.DeletedAt == null)
                    .Max(x => (long?)x.Id)

            })
            .OrderBy(x => x.ClosedAt != null)
            .ThenByDescending(x => x.LastId ?? 0L)
            .ThenBy(x => x.ChatConversationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        
        var pageRows = rows.Select(x => new ThreadPageRow(x.ChatConversationId, x.LastReadMessageId, x.LastId))
            .ToList();
        return await BuildThreadItemsAsync(userId, pageRows, ct);
    }

    public async Task<IReadOnlyList<ChatThreadListItemDto>> GetChatsForTradeAsync(int userId, int tradeId,
        CancellationToken ct)
    {
        if (tradeId <= 0)
        {
            return Array.Empty<ChatThreadListItemDto>();
        }
        
        var rows = await _db.ConversationMembers
            .AsNoTracking()
            .Where(cm => cm.UserId == userId && cm.ChatConversation.TradeId == tradeId)
            .Select(cm => new
            {
                cm.ChatConversationId,
                cm.LastReadMessageId,
                LastId = _db.ChatMessages
                    .Where(x => x.ChatConversationId == cm.ChatConversationId && x.DeletedAt == null)
                    .Max(x => (long?)x.Id)

            })
            .OrderBy(x => x.LastId ?? 0L)
            
            .ToListAsync(ct);
        
        var pagedRows = rows.Select(x => new ThreadPageRow(x.ChatConversationId, x.LastReadMessageId, x.LastId))
            .ToList();
        return await BuildThreadItemsAsync(userId, pagedRows, ct);
    }

    private sealed record ThreadPageRow(int ChatConversationId, long? LastReadMessageId, long? LastId);

    private async Task<IReadOnlyList<ChatThreadListItemDto>> BuildThreadItemsAsync(int userId,
        IReadOnlyList<ThreadPageRow> pageRows, CancellationToken ct)
    {
        
        var chatIds = pageRows
            .Select(x => x.ChatConversationId)
            .Distinct()
            .ToArray();

        if (chatIds.Length == 0)
            return Array.Empty<ChatThreadListItemDto>();

        var conversations = await _db.ChatConversations
            .AsNoTracking()
            .Where(c => chatIds.Contains(c.Id))
            .Select(c => new ConversationProjection
            {
                Id = c.Id,
                TradeId = c.TradeId,
                ClosedAt = c.ClosedAt,
                BuyerUserId = c.Trade.Customer_ID,
                SellerUserId = c.Trade.Seller_ID,
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

        var unreadByChat = await _db.ConversationMembers
            .AsNoTracking()
            .Where(cm =>
                filteredChatIds.Contains(cm.ChatConversationId) &&
                cm.UserId == userId)
            .Select(cm => new
            {
                cm.ChatConversationId,
                UnreadCount = _db.ChatMessages.Count(m => m.ChatConversationId == cm.ChatConversationId &&
                                                          m.DeletedAt == null && m.SenderId != userId
                                                          && (cm.LastReadMessageId == null || m.Id > cm.LastReadMessageId))
            })
            .ToDictionaryAsync(x => x.ChatConversationId, x => x.UnreadCount, ct);

        

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