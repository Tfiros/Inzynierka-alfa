namespace ItemTradeApp.Features.Trades.DTOs;

public enum TradeSortBy
{
    CreationDateDesc = 1,
    CreationDateAsc = 2,

    TradeIdDesc= 5,
    TradeIdAsc=6
}

public enum TradeSearchBy
{
    TradeId,
    OfferId,

    CustomerNickname,
    CustomerEmail,

    PostingUserNickname,
    PostingUserEmail,
}

public sealed class TradesQuery
{
    public string? SearchText { get; init; }
    public TradeSearchBy? SearchBy { get; init; }

    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }

    public bool? IsCounterOfferTrade { get; init; }
    public bool? ReadyForCompletion { get; init; }
    
    public bool OnlyMine { get; init; }

    public TradeSortBy SortBy { get; init; } = TradeSortBy.CreationDateDesc;
}