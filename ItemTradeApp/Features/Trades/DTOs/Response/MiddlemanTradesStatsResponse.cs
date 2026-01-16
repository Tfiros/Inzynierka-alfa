namespace ItemTradeApp.Features.Trades.DTOs.Response;

public record MiddlemanTradesStatsResponse(  
    int All,
    int Completed,
    int MyActive,
    int Available);
    
public record UserTradesStatsResponse(
    int All,
    int Completed,
    int MyActive,
    int Created);