using ItemTradeApp.Features.Shared.DTOs;

namespace ItemTradeApp.Features.CounterOffers.DTOs;

public sealed record CounterOfferDraftRequest(
    IReadOnlyCollection<OfferItemDTO> Items,
    int TokensOffered = 0
);