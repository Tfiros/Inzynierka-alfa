namespace ItemTradeApp.Features.Trades.DTOs.Response;

public record MiddlemanTradesStatsResponse(  
    int All,
    int Completed,
    int MyActive,
    int Available);