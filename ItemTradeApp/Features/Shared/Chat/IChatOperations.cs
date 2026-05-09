namespace ItemTradeApp.Features.Shared.Chat;

public interface IChatOperations
{
    Task CreateChatsForTradeAsync(CreateChatsForTradeContext context, CancellationToken ct);
    Task CloseChatsForTradeAsync(int tradeId, CancellationToken ct);
    Task PublishChatsClosedAsync(int tradeId, CancellationToken ct);
    Task PublishChatsCreatedAsync(int tradeId, CancellationToken ct);

}