using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Offers.Repositories;

public interface ICounterOfferRepository
{
    Task<List<CounterOffer>> GetAllPendingForOfferAsync(int offerId, CancellationToken ct);
    Task<bool> HasPendingForOfferAsync(int offerId, CancellationToken ct);
    Task<bool> DenyAsync(int counterOfferId, CancellationToken ct);
}

public class CounterOfferRepository(AppDbContext db) : ICounterOfferRepository
{
    public async Task<List<CounterOffer>> GetAllPendingForOfferAsync(int offerId, CancellationToken ct)
        => await db.CounterOffers.Where(co =>
                co.Offer_Id == offerId && co.CounterOfferStatus_Id == (int)CounterOfferStatuses.Pending)
            .ToListAsync(ct);

    public async Task<bool> HasPendingForOfferAsync(int offerId, CancellationToken ct)
        => await db.CounterOffers.AnyAsync(
            co => co.Offer_Id == offerId && co.CounterOfferStatus_Id == (int)CounterOfferStatuses.Pending, ct);

    public async Task<bool> DenyAsync(int counterOfferId, CancellationToken ct)
        => await db.CounterOffers.Where(co =>
                co.ID == counterOfferId && co.CounterOfferStatus_Id == (int)CounterOfferStatuses.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(co => co.CounterOfferStatus_Id, (int)CounterOfferStatuses.Denied),
                ct) == 1;
}