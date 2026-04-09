using ItemTradeApp.Features.Shared.DTOs;

namespace ItemTradeApp.Features.Shared.Trade;

public interface ITradeCreation
{
    Task<Persistence.Models.Trade> ExecuteAsync(CreateTradeContext context, CancellationToken ct);
}