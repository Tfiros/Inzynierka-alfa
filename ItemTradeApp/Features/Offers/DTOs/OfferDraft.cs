namespace ItemTradeApp.Features.Offers.DTOs;

public sealed record OfferDraft(
    Dictionary<int, int> Offered, Dictionary<int, int> Wanted, DateOnly ExpDate, int TokenCost
    );
