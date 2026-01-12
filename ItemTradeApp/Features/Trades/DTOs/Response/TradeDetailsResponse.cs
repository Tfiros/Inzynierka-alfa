namespace ItemTradeApp.Features.Trades.DTOs.Response;

public record InTradeUserPhotos(
    int UserId,
    string Nickname,
    string Email,
    List<string> Photos);

public record TradeDetailsResponse
(bool hasBuyersItems, bool hasSellersItems, InTradeUserPhotos buyingUserPhotos, InTradeUserPhotos sellingUserPhotos);