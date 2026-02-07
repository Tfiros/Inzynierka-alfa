using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;

namespace ItemTradeApp.Features.CounterOffers.DTOs;

public sealed record OfferInformationDTO(
    int OfferId,
    int OwnerId,
    string Title,
    string? Description,
    int TokenCost,
    DateOnly ExpDate,
    int OfferStatusId,
    DateTime CreationDate,
    IReadOnlyList<OfferListingItemDTO> Items
    );