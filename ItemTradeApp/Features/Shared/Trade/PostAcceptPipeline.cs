using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Shared;

public interface IPostAcceptPipeline
{
    Task<Trade> RunAsync(CreateTradeDTO context, CancellationToken ct);
}

public sealed class PostAcceptPipeline(ITradeCreation tradeCreationExecutor) : IPostAcceptPipeline
{
    public async Task<Trade> RunAsync(CreateTradeDTO context, CancellationToken ct)
    {
        return await tradeCreationExecutor.ExecuteAsync(context, ct);
    }
}