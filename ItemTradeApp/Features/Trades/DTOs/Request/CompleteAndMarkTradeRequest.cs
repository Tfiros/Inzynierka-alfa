namespace ItemTradeApp.Features.Trades.DTOs.Request;

public sealed record CompleteAndMarkTradeRequest(
    int BuyersID,
    int BuyersGrade,
    String BuyersDescription,
    int SellersID,
    int SellersGrade,
    String SellersDescription
    );