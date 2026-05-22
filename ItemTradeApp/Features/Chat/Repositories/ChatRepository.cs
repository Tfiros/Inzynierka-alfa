using ItemTradeApp.Features.Chat.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Chat.Repositories;

public interface IChatRepository
{
    Task SaveChangesAsync(CancellationToken ct);

    Task<int?> GetUserIdByAuth0Async(string auth0UserId, CancellationToken ct);
    Task<bool> ChatExistsAsync(int chatConversationId, CancellationToken ct);

    Task<bool> IsMemberAsync(int chatId, int userId, CancellationToken ct);
    Task<bool> IsMemberAsync(int chatId, string auth0UserId, CancellationToken ct);

    Task<ConversationMember?> GetMemberAsync(int chatId, int userId, CancellationToken ct);
    Task<IReadOnlyList<(int UserId, string Auth0UserId)>> GetMemberAuth0Async(int chatId, CancellationToken ct);
    

    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(int chatId, long? beforeMessageId, int pageSize, CancellationToken ct);

    Task<ChatMessageDto> AddMessageAsync(int chatId, int senderId, string message, CancellationToken ct);
    Task<ChatMessage?> GetMessageForEditAsync(long messageId, int senderId, CancellationToken ct);
    Task SoftDeleteMessageAsync(ChatMessage message, CancellationToken ct);

    Task<int> GetUnreadCountForUserAsync(int chatId, int userId, CancellationToken ct);
    Task UpdateLastReadAsync(ConversationMember member, int chatId, long lastReadMessageId, CancellationToken ct);
    Task<ChatMessage?> GetMessageByIdAsync(long messageId, CancellationToken ct);

    Task AddChatsAsync(IEnumerable<ChatConversation> chats, CancellationToken ct);
    Task CloseChatsForTradeAsync(int tradeId, DateTime closedAtUtc, CancellationToken ct);
    Task<IReadOnlyList<(int Id, DateTime ClosedAtUtc, string[] MemberAuth0Ids)>> GetClosedChatsForPublish(int tradeId, CancellationToken ct);
    Task<IReadOnlyList<(int Id, int TradeId, string[] MemberAuth0Ids)>> GetCreatedChatsForPublish(int tradeId, CancellationToken ct);
    Task<bool> IsChatClosedAsync(int chatConversationId, CancellationToken ct);

}

public sealed class ChatRepository : IChatRepository
{
    private readonly AppDbContext _db;

    public ChatRepository(AppDbContext db)
    {
        _db = db;
    }
    public Task<ChatMessage?> GetMessageByIdAsync(long messageId, CancellationToken ct)
        => _db.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId, ct);
    public Task SaveChangesAsync(CancellationToken ct)
        => _db.SaveChangesAsync(ct);

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

    public Task<bool> IsMemberAsync(int chatId, int userId, CancellationToken ct)
        => _db.ConversationMembers
            .AsNoTracking()
            .AnyAsync(m => m.ChatConversationId == chatId && m.UserId == userId, ct);

    public async Task<bool> IsMemberAsync(int chatId, string auth0UserId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Auth0UserID == auth0UserId, ct);
        if (user == null) return false;
        return await _db.ConversationMembers
            .AsNoTracking()
            .AnyAsync(m => m.ChatConversationId == chatId && m.UserId == user.ID, ct);
    }
    public Task<ConversationMember?> GetMemberAsync(int chatId, int userId, CancellationToken ct)
        => _db.ConversationMembers
            .FirstOrDefaultAsync(m => m.ChatConversationId == chatId && m.UserId == userId, ct);

    public async Task<IReadOnlyList<(int UserId, string Auth0UserId)>> GetMemberAuth0Async(int chatId, CancellationToken ct)
    {
        var members = await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.ChatConversationId == chatId)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.ID,
                (m, u) => new { m.UserId, u.Auth0UserID })
            .ToListAsync(ct);

        return members
            .Select(x => (x.UserId, x.Auth0UserID))
            .ToList();
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        int chatId,
        long? beforeMessageId,
        int pageSize,
        CancellationToken ct)
    {
        pageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 200);

        var query = _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChatConversationId == chatId);

        if (beforeMessageId.HasValue)
            query = query.Where(m => m.Id < beforeMessageId.Value);

        var rows = await query
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

    public async Task<ChatMessageDto> AddMessageAsync(int chatId, int senderId, string message, CancellationToken ct)
    {
        var entity = new ChatMessage
        {
            ChatConversationId = chatId,
            SenderId = senderId,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            EditedAt = null
        };

        _db.ChatMessages.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new ChatMessageDto(
            entity.Id,
            entity.ChatConversationId,
            entity.SenderId,
            entity.Message,
            entity.CreatedAt,
            entity.EditedAt
        );
    }

    public Task<ChatMessage?> GetMessageForEditAsync(long messageId, int senderId, CancellationToken ct)
        => _db.ChatMessages
            .FirstOrDefaultAsync(m =>
                m.Id == messageId &&
                m.SenderId == senderId &&
                m.DeletedAt == null, ct);

    public async Task SoftDeleteMessageAsync(ChatMessage message, CancellationToken ct)
    {
        message.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> GetUnreadCountForUserAsync(int chatId, int userId, CancellationToken ct)
    {
        var lastRead = await _db.ConversationMembers
            .AsNoTracking()
            .Where(m => m.ChatConversationId == chatId && m.UserId == userId)
            .Select(m => m.LastReadMessageId)
            .FirstOrDefaultAsync(ct);

        return await _db.ChatMessages
            .AsNoTracking()
            .Where(m =>
                m.ChatConversationId == chatId &&
                m.DeletedAt == null &&
                m.SenderId != userId &&
                (lastRead == null || m.Id > lastRead))
            .CountAsync(ct);
    }

    public async Task UpdateLastReadAsync(
        ConversationMember member,
        int chatId,
        long lastReadMessageId,
        CancellationToken ct)
    {
        member.LastReadMessageId = lastReadMessageId;
        member.LastReadMessageChatConversationId = chatId;

        await _db.SaveChangesAsync(ct);
    }

    public Task AddChatsAsync(IEnumerable<ChatConversation> chats, CancellationToken ct)
    {
        _db.ChatConversations.AddRange(chats);
        return Task.CompletedTask;
    }

    public Task CloseChatsForTradeAsync(int tradeId, DateTime closedAtUtc, CancellationToken ct)
        => _db.ChatConversations
            .Where(c => c.TradeId == tradeId && c.ClosedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAt, _ => closedAtUtc), ct);

    public async Task<IReadOnlyList<(int Id, DateTime ClosedAtUtc, string[] MemberAuth0Ids)>> GetClosedChatsForPublish(
        int tradeId, CancellationToken ct)
    {
        var rows = await _db.ChatConversations
            .AsNoTracking()
            .Where(c => c.TradeId == tradeId && c.ClosedAt != null)
            .Select(c => new
            {
                c.Id,
                ClosedAtUtc = c.ClosedAt!.Value,
                MemberAuth0Ids = c.Members.Select(m => m.User.Auth0UserID).ToArray()
            }).ToListAsync(ct);
        return rows.Select(r => (r.Id, r.ClosedAtUtc, r.MemberAuth0Ids)).ToList();
    }
    
    public Task<bool> IsChatClosedAsync(int chatConversationId, CancellationToken ct)
    => _db.ChatConversations.AsNoTracking().AnyAsync(c => c.Id == chatConversationId && c.ClosedAt != null, ct);

    public async Task<IReadOnlyList<(int Id, int TradeId, string[] MemberAuth0Ids)>> GetCreatedChatsForPublish(
        int tradeId, CancellationToken ct)
    {
        var rows = await _db.ChatConversations
            .AsNoTracking()
            .Where(c => c.TradeId == tradeId && !c.IsDeleted)
            .Select(c => new
            {
                c.Id,
                c.TradeId,
                MemberAuth0Ids = c.Members.Select(m => m.User.Auth0UserID).ToArray()
            }).ToListAsync(ct);
        return rows.Select(r => (r.Id, r.TradeId, r.MemberAuth0Ids)).ToList();
    }

}
