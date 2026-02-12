using ItemTradeApp.Features.Offers.DTOs;

namespace ItemTradeApp.Features.CounterOffers.DTOs;

public sealed record CounterOfferDraftRequest(
    IReadOnlyCollection<OfferItemDTO> OfferedItems,
    IReadOnlyCollection<OfferItemDTO> WantedItems
);