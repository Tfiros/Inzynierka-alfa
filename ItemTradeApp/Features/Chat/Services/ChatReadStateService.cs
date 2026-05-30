using ItemTradeApp.Features.Chat.Repositories;

namespace ItemTradeApp.Features.Chat.Services;

public interface IChatReadStateService
{
    Task<int> MarkReadAsync(int chatId, int userId, long lastReadMessageId, CancellationToken ct);
    Task<int> GetUnreadCountAsync(int chatId, int userId, CancellationToken ct);
}


public sealed class ChatReadStateService : IChatReadStateService
{
    private readonly IChatRepository _repo;

    public ChatReadStateService(IChatRepository repo)
    {
        _repo = repo;
    }

    public async Task<int> MarkReadAsync(int chatId, int userId, long lastReadMessageId, CancellationToken ct)
    {
        var member = await _repo.GetMemberAsync(chatId, userId, ct)
                     ?? throw new InvalidOperationException("not_member");

        if (member.LastReadMessageId == null || lastReadMessageId > member.LastReadMessageId.Value)
        {
            await _repo.UpdateLastReadAsync(member, chatId, lastReadMessageId, ct);
        }

        return await _repo.GetUnreadCountForUserAsync(chatId, userId, ct);
    }

    public Task<int> GetUnreadCountAsync(int chatId, int userId, CancellationToken ct)
        => _repo.GetUnreadCountForUserAsync(chatId, userId, ct);
}