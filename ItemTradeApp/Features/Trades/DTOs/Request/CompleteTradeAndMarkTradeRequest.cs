namespace ItemTradeApp.Features.Trades.DTOs.Request;

public sealed record CompleteTradeAndMarkTradeRequest(
    int BuyersID,
    int BuyersGrade,
    String BuyersDescription,
    int SellersID,
    int SellersGrade,
    String SellersDescription
    );