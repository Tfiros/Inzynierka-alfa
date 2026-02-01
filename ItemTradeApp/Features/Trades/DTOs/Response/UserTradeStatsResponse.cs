namespace ItemTradeApp.Features.Trades.DTOs.Response;

public record UserTradeStatsResponse(  
    int All,
    int Completed,
    int MyActive,
    int Created);