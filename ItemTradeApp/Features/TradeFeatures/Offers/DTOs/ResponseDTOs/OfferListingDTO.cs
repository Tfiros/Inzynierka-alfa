namespace ItemTradeApp.Features.TradeFeatures.Offers.DTOs.ResponseDTOs;

public sealed record OfferListingDTO(int OfferId, DateTime ExpDate, DateTime CreationDate, int TokenCost, 
    int OfferStatusId, OfferUserDTO User, IReadOnlyList<OfferListingItemDTO> Items);
