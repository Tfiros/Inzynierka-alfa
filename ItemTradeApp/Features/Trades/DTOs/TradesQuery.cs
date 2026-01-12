public enum TradeSortBy
{
    CreationDateDesc = 1,
    CreationDateAsc = 2,

    TokenCostDesc = 3,
    TokenCostAsc = 4,

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

    public int? MinTokenCost { get; init; }
    public int? MaxTokenCost { get; init; }

    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }

    public bool? IsCounterOfferTrade { get; init; }
    public bool? ReadyForCompletion { get; init; }

    public TradeSortBy SortBy { get; init; } = TradeSortBy.CreationDateDesc;
}
