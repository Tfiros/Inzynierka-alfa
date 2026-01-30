using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;

namespace ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;

public sealed record OfferListingItemDTO(
     ItemDTO ItemDto,
     int Quantity,
     int GenreId,
     string GenreName,
     int RarityId,
     string RarityName
);
