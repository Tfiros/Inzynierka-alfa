namespace ItemTradeApp.Features.Offers.Internal;

internal sealed record OfferDraft(
    Dictionary<int, DictItemQuantity> Offered, Dictionary<int, DictItemQuantity> Wanted, DateOnly ExpDate, int TokenCost
);