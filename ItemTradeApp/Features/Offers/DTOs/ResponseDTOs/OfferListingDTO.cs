namespace ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;

public sealed record OfferListingDTO(OfferCoreDTO OfferCoreDto, OfferUserDTO OfferUserDto, int OfferStatusId, List<OfferListingItemDTO> Items);