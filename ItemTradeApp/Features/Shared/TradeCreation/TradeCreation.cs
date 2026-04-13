using ItemTradeApp.Features.Shared.TradeCreation.DTOs;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Shared.TradeCreation;

public interface ITradeCreation
{
    Task<Trade> ExecuteAsync(CreateTradeContext context, CancellationToken ct);
}