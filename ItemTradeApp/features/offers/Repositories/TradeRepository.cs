using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Offers.Repositories;

public interface ITradeRepository
{
    Task<bool> TradeExistsForOfferAsync(int offerId, CancellationToken ct);
}

public class TradeRepository(AppDbContext db) : ITradeRepository
{
    public Task<bool> TradeExistsForOfferAsync(int offerId, CancellationToken ct)
        => db.Trades.AsNoTracking()
            .AnyAsync(t => t.Offer_ID == offerId && t.TradeStatus_ID != (int)TradeStatuses.Failed, ct);
}