using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Offers.Repositories;

public interface ICounterOfferRepository
{
    Task DenyAllPendingForOfferAsync(int offerId, CancellationToken ct);
}

public class CounterOfferRepository(AppDbContext db) : ICounterOfferRepository
{
    public Task DenyAllPendingForOfferAsync(int offerId, CancellationToken ct)
        => db.CounterOffers.Where(co =>
                co.Offer_Id == offerId && co.CounterOfferStatus_Id == (int)CounterOfferStatuses.Pending)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.CounterOfferStatus_Id, (int)CounterOfferStatuses.Denied), ct);
}