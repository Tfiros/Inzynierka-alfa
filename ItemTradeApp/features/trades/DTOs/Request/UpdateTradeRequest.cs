namespace ItemTradeApp.Features.Trades.DTOs.Request;

public record UpdateTradeRequest(
    bool? HasBuyerItems = null,
    bool? HasSellerItems = null
    );