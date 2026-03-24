namespace ItemTradeApp.Features.CounterOffers.DTOs;

public sealed record CounterOfferItemsDto(
    int ItemId,
    string Name,
    string? PhotoUrl,
    int GameId,
    string GameName,
    int Quantity
);
