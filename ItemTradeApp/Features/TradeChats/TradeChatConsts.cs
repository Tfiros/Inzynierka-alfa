namespace ItemTradeApp.Features.TradeChats;

public static class TradeChatConsts
{
    public const int MessageMaxLength = 2000;
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(5);
    public const string MiddlemanRole = "Middleman";

    public static string UserGroup(int userId) => $"user:{userId}";
    public static string ChatGroup(int tradeChatId) => $"tradeChat:{tradeChatId}";
    
}