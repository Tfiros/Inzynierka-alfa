namespace ItemTradeApp.Features.TradeFeatures.Offers.DTOs.ResponseDTOs;

public sealed record OfferListingItemDTO
(int ItemId, string Name, int GameId, string PhotoUrl, int Quantity, 
        string GameName, int GenreId, string GenreName);