using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Trades.Repositories;
public interface ICounterOfferRepository
{
    Task<CounterOffer?> GetByIdAsync(int counterOfferId, CancellationToken ct);
    Task DenyOtherPendingForOfferAsync(int offerId, int acceptedCounterOfferId, CancellationToken ct);
}
public sealed class CounterOfferRepository(AppDbContext db) : ICounterOfferRepository
{
    public async Task<CounterOffer?> GetByIdAsync(int counterOfferId, CancellationToken ct) =>
        await db.CounterOffers.FirstOrDefaultAsync(c => c.ID == counterOfferId, ct);
    public async Task DenyOtherPendingForOfferAsync(int offerId, int acceptedCounterOfferId, CancellationToken ct)
    {
        await db.CounterOffers
            .Where(co =>
                co.Offer_Id == offerId &&
                co.ID != acceptedCounterOfferId &&
                co.CounterOfferStatus_Id == (int)CounterOfferStatuses.Pending)
            .ExecuteUpdateAsync(s =>
                    s.SetProperty(x => x.CounterOfferStatus_Id, (int)CounterOfferStatuses.Denied),
                ct);
    }
}