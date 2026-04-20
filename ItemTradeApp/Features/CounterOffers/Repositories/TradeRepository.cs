using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.CounterOffers;

public interface ITradeRepository
{
    Task<bool> TradeExistsForOfferAsync(int offerId, CancellationToken ct);
}

public sealed class TradeRepository(AppDbContext db):ITradeRepository
{
    public async Task<bool> TradeExistsForOfferAsync(int offerId, CancellationToken ct)
    {
        return await db.Trades
            .AsNoTracking()
            .AnyAsync(t => t.Offer_ID == offerId && t.TradeStatus_ID != (int)TradeStatuses.Failed, ct);
    }
}