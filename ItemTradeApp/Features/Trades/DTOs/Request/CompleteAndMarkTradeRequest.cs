namespace ItemTradeApp.Features.Trades.DTOs.Request;

public sealed record CompleteAndMarkTradeRequest(
    int BuyersID,
    int BuyersGrade,
    string BuyersDescription,
    int SellersID,
    int SellersGrade,
    string SellersDescription
    );