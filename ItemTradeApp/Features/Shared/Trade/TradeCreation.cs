using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Shared;

public interface ITradeCreation
{
    Task<Trade> ExecuteAsync(CreateTradeDTO context, CancellationToken ct);
}