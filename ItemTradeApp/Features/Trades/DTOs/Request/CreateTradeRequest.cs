namespace ItemTradeApp.Features.Trades.DTOs.Request;

public sealed record CreateTradeRequest
(
    int OfferId,
    int CustomerId,
    int? CounterOfferId = null
    );