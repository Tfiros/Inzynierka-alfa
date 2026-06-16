using ItemTradeApp.Persistence;

namespace ItemTradeApp.Features.Shared;

public class TradeStatusName
{
    public static string ToStringName(TradeStatuses status)
    {
        return status switch
        {
            TradeStatuses.New => "nowy",
            TradeStatuses.InRealization => "w realizacji",
            TradeStatuses.SuccesfulRealization => "zrealizowany",
            TradeStatuses.Failed => "nieudany",
            _ => "nieznany status"
        };
    }
}