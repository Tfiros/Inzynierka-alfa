namespace ItemTradeApp.Features.Offers.Internal;

internal sealed record OfferDraft(string Title, string Description,
    Dictionary<int, DictItemQuantity> Offered, Dictionary<int, DictItemQuantity> Wanted, DateOnly ExpDate, int TokenCost, bool IsHighlighted
);