using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.CounterOffers;

public interface ICounterOffersRepository
{
    void Add(CounterOffer counterOffer);

    Task<CounterOffer?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<bool> CanCreateCounterOfferAsync(int userId, int offerId, CancellationToken ct = default);
}

public sealed class CounterOffersRepository(AppDbContext db) : ICounterOffersRepository
{
    private const int PendingStatusId = 1;

    public void Add(CounterOffer counterOffer) => db.CounterOffers.Add(counterOffer);

    public Task<CounterOffer?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.CounterOffers
            .AsNoTracking()
            .Include(x => x.ListingCounterOfferItems)
            .FirstOrDefaultAsync(x => x.ID == id, ct);

    public async Task<bool> CanCreateCounterOfferAsync(int userId, int offerId, CancellationToken ct = default)
    {
        var existsPending = await db.CounterOffers.AnyAsync(
            x => x.User_ID == userId
                 && x.Offer_Id == offerId
                 && x.CounterOfferStatus_Id == PendingStatusId,
            ct
        );

        return !existsPending;
    }
}