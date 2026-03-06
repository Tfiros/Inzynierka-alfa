using ItemTradeApp.Features.Chat.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Chat;

public interface IChatRepository
{
    Task<int?> GetUserIdByAuth0UserIdAsync(string auth0UserId, CancellationToken ct);

    Task<bool> IsMemberAsync(int chatId, int userId, CancellationToken ct);
    Task<IReadOnlyList<int>> GetMemberUserIdsAsync(int chatId, CancellationToken ct);
    Task<ConversationMember?> GetMemberAsync(int chatId, int userId, CancellationToken ct);

    Task<IReadOnlyDictionary<int, string>> GetAuth0UserIdsByUserIdsAsync(IEnumerable<int> userIds, CancellationToken ct);
    Task<IReadOnlyList<(int UserId, string Auth0UserId)>> GetMemberAuth0Async(int chatId, CancellationToken ct);

    Task<int> GetOrCreateDmAsync(int userId1, int userId2, CancellationToken ct);
    Task<IReadOnlyList<ChatThreadListItemDto>> GetThreadsAsync(int userId, int page, int pageSize, CancellationToken ct);

    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(int chatId, long? beforeMessageId, int pageSize, CancellationToken ct);

    Task<ChatMessageDto> AddMessageAsync(int chatId, int senderId, string message, CancellationToken ct);
    Task<ChatMessage?> GetMessageForEditAsync(long messageId, int senderId, CancellationToken ct);
    Task SoftDeleteMessageAsync(ChatMessage message, CancellationToken ct);

    Task MarkReadAsync(int chatId, int userId, long lastReadMessageId, CancellationToken ct);
    Task<int> GetUnreadCountAsync(int chatId, int userId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
    Task<int?> GetUserIdByAuth0Async(string auth0UserId, CancellationToken ct);

    Task<bool> ChatExistsAsync(int chatConversationId, CancellationToken ct);
    
}

public sealed class ChatRepository : IChatRepository
{
    private readonly AppDbContext _db;
    public ChatRepository(AppDbContext db) => _db = db;

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    // -------- USERS --------
    public Task<int?> GetUserIdByAuth0Async(string auth0UserId, CancellationToken ct)
        => _db.Users
            .AsNoTracking()
            .Where(u => u.Auth0UserID == auth0UserId)
            .Select(u => (int?)u.ID)
            .FirstOrDefaultAsync(ct);

    public Task<bool> ChatExistsAsync(int chatConversationId, CancellationToken ct)
        => _db.ChatConversations
            .AsNoTracking()
            .AnyAsync(c => c.Id == chatConversationId && !c.IsDeleted, ct);
    public async Task<int?> GetUserIdByAuth0UserIdAsync(string auth0UserId, CancellationToken ct)
        => await _db.Users
            .Where(u => u.Auth0UserID == auth0UserId)
            .Select(u => (int?)u.ID)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<int, string>> GetAuth0UserIdsByUserIdsAsync(IEnumerable<int> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToArray();

        return await _db.Users
            .Where(u => ids.Contains(u.ID))
            .Select(u => new { u.ID, u.Auth0UserID })
            .ToDictionaryAsync(x => x.ID, x => x.Auth0UserID, ct);
    }

    public async Task<IReadOnlyList<(int UserId, string Auth0UserId)>> GetMemberAuth0Async(int chatId, CancellationToken ct)
    {
        var members = await _db.Set<ConversationMember>()
            .Where(m => m.ChatConversationId == chatId)
            .Join(_db.Users,
                m => m.UserId,
                u => u.ID,
                (m, u) => new { m.UserId, u.Auth0UserID })
            .ToListAsync(ct);

        return members.Select(x => (x.UserId, x.Auth0UserID)).ToList();
    }

    // -------- MEMBERSHIP --------

    public Task<bool> IsMemberAsync(int chatId, int userId, CancellationToken ct)
        => _db.Set<ConversationMember>()
            .AnyAsync(m => m.ChatConversationId == chatId && m.UserId == userId, ct);

    public Task<ConversationMember?> GetMemberAsync(int chatId, int userId, CancellationToken ct)
        => _db.Set<ConversationMember>()
            .FirstOrDefaultAsync(m => m.ChatConversationId == chatId && m.UserId == userId, ct);

    public async Task<IReadOnlyList<int>> GetMemberUserIdsAsync(int chatId, CancellationToken ct)
        => await _db.Set<ConversationMember>()
            .Where(m => m.ChatConversationId == chatId)
            .Select(m => m.UserId)
            .ToListAsync(ct);

    // -------- CONVERSATIONS --------

    public async Task<int> GetOrCreateDmAsync(int userId1, int userId2, CancellationToken ct)
    {
        var a = Math.Min(userId1, userId2);
        var b = Math.Max(userId1, userId2);

        var existing = await _db.Set<ChatConversation>()
            .Where(c =>
                !c.IsDeleted &&
                c.Members.Count == 2 &&
                c.Members.Any(m => m.UserId == a) &&
                c.Members.Any(m => m.UserId == b))
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != 0)
            return existing;

        var conv = new ChatConversation
        {
            CreatedAt = DateTime.UtcNow,
            Name = "testing",
            IsDeleted = false
        };

        _db.Add(conv);
        await _db.SaveChangesAsync(ct);

        _db.AddRange(
            new ConversationMember { ChatConversationId = conv.Id, UserId = a, Role = 1 },
            new ConversationMember { ChatConversationId = conv.Id, UserId = b, Role = 1 }
        );

        await _db.SaveChangesAsync(ct);
        return conv.Id;
    }
    record MemberLite(int UserId, string Auth0, string Display, string? Avatar);
    public async Task<IReadOnlyList<ChatThreadListItemDto>> GetThreadsAsync(int userId, int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        // mały typ do projekcji (żadnych dynamic)


        var myChatsQ =
            from m in _db.Set<ConversationMember>()
            where m.UserId == userId
            select new { m.ChatConversationId, m.LastReadMessageId };

        var lastMsgQ =
            from msg in _db.Set<ChatMessage>()
            where msg.DeletedAt == null
            group msg by msg.ChatConversationId into g
            select new { ChatId = g.Key, LastId = (long?)g.Max(x => x.Id) };

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

        var chatIds = pageRows.Select(x => x.ChatConversationId).ToArray();

        var convs = await _db.Set<ChatConversation>()
            .Where(c => chatIds.Contains(c.Id) && !c.IsDeleted)
            .Select(c => new
            {
                c.Id,
                c.Name,
                Members = c.Members.Select(m => new MemberLite(
                    m.UserId,
                    m.User.Auth0UserID,
                    m.User.ProfileInfo.Nickname,
                    m.User.ProfileInfo.ImageUrl
                )).ToList()
            })
            .ToListAsync(ct);

        var convById = convs.ToDictionary(x => x.Id);

        var lastIds = pageRows
            .Select(x => x.LastId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();

        var lastMsgs = lastIds.Length == 0
            ? new Dictionary<int, ChatMessage>()
            : await _db.Set<ChatMessage>()
                .Where(m => lastIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.ChatConversationId, m => m, ct);

        var lastReadByChat = pageRows.ToDictionary(x => x.ChatConversationId, x => x.LastReadMessageId);

        var unreadRaw = await _db.Set<ChatMessage>()
            .Where(m => chatIds.Contains(m.ChatConversationId)
                        && m.DeletedAt == null
                        && m.SenderId != userId)
            .Select(m => new { m.ChatConversationId, m.Id })
            .ToListAsync(ct);

        var unreadByChat = unreadRaw
            .GroupBy(x => x.ChatConversationId)
            .ToDictionary(g => g.Key, g =>
            {
                lastReadByChat.TryGetValue(g.Key, out var lastRead);
                if (lastRead is null) return g.Count();
                return g.Count(x => x.Id > lastRead.Value);
            });

        var result = new List<ChatThreadListItemDto>(pageRows.Count);

        foreach (var r in pageRows)
        {
            convById.TryGetValue(r.ChatConversationId, out var conv);
            lastMsgs.TryGetValue(r.ChatConversationId, out var lm);

            var members = conv?.Members ?? new List<MemberLite>();
            var isGroup = members.Count > 2;

            int? otherId = null;
            string? otherAuth0 = null;
            string? avatar = null;
            string displayName;

            if (isGroup)
            {
                displayName = string.IsNullOrWhiteSpace(conv?.Name) ? "Grupa" : conv!.Name!;
            }
            else
            {
                var other = members.FirstOrDefault(x => x.UserId != userId);
                otherId = other?.UserId;
                otherAuth0 = other?.Auth0;
                avatar = other?.Avatar;
                displayName = other?.Display ?? "Użytkownik";
            }

            var unread = unreadByChat.TryGetValue(r.ChatConversationId, out var cnt) ? cnt : 0;

            result.Add(new ChatThreadListItemDto(
                ChatConversationId: r.ChatConversationId,
                IsGroup: isGroup,
                DisplayName: displayName,
                OtherUserId: otherId,
                OtherUserAuth0UserId: otherAuth0,
                AvatarUrl: avatar,
                IsOnline: null, // uzupełnia service przez PresenceTracker

                LastMessageId: lm?.Id,
                LastMessageText: lm?.DeletedAt != null ? "[deleted]" : lm?.Message,
                LastMessageSenderId: lm?.SenderId,
                LastMessageCreatedAtUtc: lm?.CreatedAt,

                UnreadCount: unread
            ));
        }

        return result;
    }

    // -------- MESSAGES --------

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(int chatId, long? beforeMessageId, int pageSize, CancellationToken ct)
    {
        pageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 200);

        var q = _db.Set<ChatMessage>()
            .Where(m => m.ChatConversationId == chatId);

        if (beforeMessageId.HasValue)
            q = q.Where(m => m.Id < beforeMessageId.Value);

        var rows = await q
            .OrderByDescending(m => m.Id)
            .Take(pageSize)
            .Select(m => new ChatMessageDto(
                m.Id,
                m.ChatConversationId,
                m.SenderId,
                m.DeletedAt != null ? "[deleted]" : m.Message,
                m.CreatedAt,
                m.EditedAt
            ))
            .ToListAsync(ct);

        rows.Reverse();
        return rows;
    }

    public async Task<ChatMessageDto> AddMessageAsync(
        int chatConversationId,
        int senderId,
        string message,
        CancellationToken ct)
    {
        var entity = new ChatMessage
        {
            ChatConversationId = chatConversationId,
            SenderId = senderId,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            EditedAt = null,
        };

        _db.ChatMessages.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new ChatMessageDto(
            Id: entity.Id,
            ChatConversationId: entity.ChatConversationId,
            SenderId: entity.SenderId,
            Message: entity.Message,
            CreatedAt: entity.CreatedAt,
            EditedAt: entity.EditedAt
        );
    }

    public Task<ChatMessage?> GetMessageForEditAsync(long messageId, int senderId, CancellationToken ct)
        => _db.Set<ChatMessage>()
            .FirstOrDefaultAsync(m =>
                m.Id == messageId &&
                m.SenderId == senderId &&
                m.DeletedAt == null, ct);

    public async Task SoftDeleteMessageAsync(ChatMessage message, CancellationToken ct)
    {
        message.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // -------- READ STATE --------

    public async Task MarkReadAsync(int chatId, int userId, long lastReadMessageId, CancellationToken ct)
    {
        var member = await GetMemberAsync(chatId, userId, ct)
            ?? throw new InvalidOperationException("not_member");

        if (member.LastReadMessageId == null || lastReadMessageId > member.LastReadMessageId.Value)
        {
            member.LastReadMessageId = lastReadMessageId;
            member.LastReadMessageChatConversationId = chatId;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetUnreadCountAsync(int chatId, int userId, CancellationToken ct)
    {
        var lastRead = await _db.Set<ConversationMember>()
            .Where(m => m.ChatConversationId == chatId && m.UserId == userId)
            .Select(m => m.LastReadMessageId)
            .FirstOrDefaultAsync(ct);

        return await _db.Set<ChatMessage>()
            .Where(m =>
                m.ChatConversationId == chatId &&
                m.DeletedAt == null &&
                m.SenderId != userId &&
                (lastRead == null || m.Id > lastRead))
            .CountAsync(ct);
    }
}
