namespace ItemTradeApp.Features.CounterOffers.DTOs.ResponseDTOs;

public sealed record AcceptCounterOfferResponse(
    int TradeId,
    int OfferId,
    int AcceptedCounterOfferId
);