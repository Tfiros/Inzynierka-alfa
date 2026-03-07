using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Chat.Services;

public interface IChatDmService
{
    Task<int> GetOrCreateDmAsync(int userId1, int userId2, CancellationToken ct);
}
public sealed class ChatDmService : IChatDmService
{
    private readonly IChatRepository _repo;

    public ChatDmService(IChatRepository repo)
    {
        _repo = repo;
    }

    public async Task<int> GetOrCreateDmAsync(int userId1, int userId2, CancellationToken ct)
    {
        var a = Math.Min(userId1, userId2);
        var b = Math.Max(userId1, userId2);

        var existing = await _repo.FindExistingDmAsync(a, b, ct);
        if (existing.HasValue)
            return existing.Value;

        var chatId = await _repo.CreateConversationAsync("testing", ct);

        await _repo.AddConversationMembersAsync(chatId, new[]
        {
            new ConversationMember
            {
                UserId = a,
                Role = 1
            },
            new ConversationMember
            {
                UserId = b,
                Role = 1
            }
        }, ct);

        return chatId;
    }
}