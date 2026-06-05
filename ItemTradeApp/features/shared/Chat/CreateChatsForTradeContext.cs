namespace ItemTradeApp.Features.Shared.Chat;

public sealed record CreateChatsForTradeContext(
    int TradeId, int BuyerId, int SellerId, int MiddlemanId
    );