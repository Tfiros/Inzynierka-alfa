using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Offers.Repositories;

public interface ICounterOfferRepository
{
    Task<List<CounterOffer>> GetAllPendingForOfferAsync(int offerId, CancellationToken ct);
}

public class CounterOfferRepository(AppDbContext db) : ICounterOfferRepository
{
    public Task<List<CounterOffer>> GetAllPendingForOfferAsync(int offerId, CancellationToken ct)
        => db.CounterOffers.Where(co =>
                co.Offer_Id == offerId && co.CounterOfferStatus_Id == (int)CounterOfferStatuses.Pending)
            .ToListAsync(ct);
}