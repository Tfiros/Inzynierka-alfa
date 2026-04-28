namespace ItemTradeApp.Features.Trades.DTOs.Request;

public sealed record CompleteAndMarkTradeRequest(
    int BuyersGrade,
    string BuyersDescription,
    int SellersGrade,
    string SellersDescription
    );