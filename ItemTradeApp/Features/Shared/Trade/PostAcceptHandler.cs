using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Shared;

public interface IPostAcceptHandler
{
    Task<Trade> RunAsync(CreateTradeDTO context, CancellationToken ct);
}

public sealed class PostAcceptHandler(ITradeCreation tradeCreationExecutor) : IPostAcceptHandler
{
    public async Task<Trade> RunAsync(CreateTradeDTO context, CancellationToken ct)
    {
        return await tradeCreationExecutor.ExecuteAsync(context, ct);
    }
}