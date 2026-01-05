namespace ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;


public sealed record OfferResponse(
    OfferCoreDTO OfferCoreDto,
    IReadOnlyCollection<OfferItemDTO> OfferedItems,
    IReadOnlyCollection<OfferItemDTO> WantedItems);
    
