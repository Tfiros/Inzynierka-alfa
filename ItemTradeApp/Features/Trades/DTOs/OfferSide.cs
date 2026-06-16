namespace ItemTradeApp.Features.Trades.DTOs;

public sealed record OfferSide(bool HasOfferedItems, 
    bool HasWantedItems, 
    int TokensOffered, 
    int TokensWanted);