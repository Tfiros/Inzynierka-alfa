namespace ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;

public sealed record OfferListingDTO(
    OfferCoreDTO OfferCoreDto, OfferUserDTO OfferUserDto, List<OfferListingItemDTO> OfferedItems,
    List<OfferListingItemDTO> WantedItems, int OfferedItemsTotalCount, int WantedItemTotalCount
    );