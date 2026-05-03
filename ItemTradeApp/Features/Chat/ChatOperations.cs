using ItemTradeApp.Features.Chat.Helpers;
using ItemTradeApp.Features.Shared.Chat;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Chat;

public sealed class ChatOperations(IChatRepository chatRepository, IChatRealtimePublisher publisher,
    TimeProvider timeProvider) : IChatOperations
{
    public Task CreateChatsForTradeAsync(CreateChatsForTradeContext ctx, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var buyerChat = new ChatConversation
        {
            CreatedAt = now,
            IsDeleted = false,
            TradeId = ctx.TradeId,
            ClosedAt = null,
            Members = new List<ConversationMember>
            {
                new() { UserId = ctx.BuyerId },
                new() { UserId = ctx.MiddlemanId }
            }
        };
        
        var sellerChat= new ChatConversation
        {
            CreatedAt = now,
            IsDeleted = false,
            TradeId = ctx.TradeId,
            ClosedAt = null,
            Members = new List<ConversationMember>
            {
                new() { UserId = ctx.SellerId },
                new() { UserId = ctx.MiddlemanId }
            }
        };
        return chatRepository.AddChatsAsync(new[] { buyerChat, sellerChat }, ct);
    }

    public Task CloseChatsForTradeAsync(int tradeId, CancellationToken ct)
    {
        var closedAt = timeProvider.GetUtcNow().UtcDateTime;
        return chatRepository.CloseChatsForTradeAsync(tradeId, closedAt, ct);
    }

    public async Task PublishChatsClosedAsync(int tradeId, CancellationToken ct)
    {
        var chats = await chatRepository.GetClosedChatsForPublish(tradeId, ct);
        foreach (var (id, closedAtUtc, memberAuth0Ids) in chats)
        {
            await publisher.PublishChatClosedAsync(id, closedAtUtc, memberAuth0Ids, ct);
        }
    }
    public async Task PublishChatsCreatedAsync(int tradeId, CancellationToken ct)
    {
        var chats = await chatRepository.GetCreatedChatsForPublish(tradeId, ct);
        foreach (var (id, tradeResponseId, memberAuth0Ids) in chats)
        {
            await publisher.PublishChatCreatedAsync(id, tradeResponseId, memberAuth0Ids, ct);
        }
    }
}
