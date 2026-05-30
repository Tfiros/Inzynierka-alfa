using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;

namespace ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;

public sealed record OfferDetailsDTO(
    OfferCoreDTO OfferCoreDto, OfferUserDTO OfferUserDto, List<OfferListingItemDTO> OfferedItems,
    List<OfferListingItemDTO> WantedItems);