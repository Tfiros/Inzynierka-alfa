namespace ItemTradeApp.Features.CounterOffers.DTOs;

public sealed record AcceptCounterOfferResponse(
    int TradeId,
    int OfferId,
    int AcceptedCounterOfferId
);