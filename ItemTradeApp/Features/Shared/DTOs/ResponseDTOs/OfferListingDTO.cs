using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;

namespace ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;

public sealed record OfferListingDTO(
    OfferCoreDTO OfferCoreDto, OfferUserDTO OfferUserDto, List<OfferListingItemDTO> OfferedItems,
    List<OfferListingItemDTO> WantedItems, int OfferedItemsTotalCount, int WantedItemsTotalCount
    );