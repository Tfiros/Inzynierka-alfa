namespace ItemTradeApp.Features.CounterOffers.DTOs.ResponseDTO;

public sealed record AcceptCounterOfferResponse(
    int TradeId,
    int OfferId,
    int AcceptedCounterOfferId
);