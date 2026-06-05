using ItemTradeApp.Features.Shared.DTOs;

namespace ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;

public sealed record CounterOfferDraftRequest(
    IReadOnlyCollection<OfferItemDTO> Items,
    int TokensOffered = 0
);