namespace ItemTradeApp.Features.Shared.DTOs;

public sealed record CreateTradeDTO(
    int OfferId,
    int BuyerId,
    int SellerId,
    int TokenCost
);