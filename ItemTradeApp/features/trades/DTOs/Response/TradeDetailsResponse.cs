namespace ItemTradeApp.Features.Trades.DTOs.Response;

public record InTradeUserPhotos(
    int UserId,
    string Nickname,
    string Email,
    List<string> Photos);

public record TradeDetailsResponse
(bool HasBuyersItems, bool HasSellersItems, InTradeUserPhotos BuyingUserPhotos, InTradeUserPhotos SellingUserPhotos);