namespace ItemTradeApp.Features.CounterOffers.DTOs;

public sealed record AcceptCounterOfferAsyncDTO(
    int TradeId,
    int OfferId,
    int AcceptedCounterOfferId
);