namespace ItemTradeApp.Features.Shared.TradeCreation.DTOs;

public sealed record CreateTradeContext(
    int OfferId,
    int BuyerId,
    int SellerId,
    int TokenCost
);