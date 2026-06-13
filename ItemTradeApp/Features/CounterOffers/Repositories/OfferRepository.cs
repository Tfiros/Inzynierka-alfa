using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.CounterOffers.Repositories;

public interface IOfferRepository
{
    Task<Offer?> GetOfferAsync(int offerId, CancellationToken ct);
    Task<int?> GetOfferOwnerIdAsync(int offerId, CancellationToken ct);
    void RemoveListingItems(IEnumerable<ListingItems> items);
}

public sealed class OfferRepository(AppDbContext db):IOfferRepository
{
    public async Task<Offer?> GetOfferAsync(int offerId, CancellationToken ct)
    {
        return await db.Offers
            .Include(o => o.ListingItems)
            .FirstOrDefaultAsync(o => o.ID == offerId, ct);
    }
    public async Task<int?> GetOfferOwnerIdAsync(int offerId, CancellationToken ct)
    {
        return await db.Offers
            .AsNoTracking()
            .Where(o => o.ID == offerId)
            .Select(o => (int?)o.User_ID)
            .FirstOrDefaultAsync(ct);
    }
    public void RemoveListingItems(IEnumerable<ListingItems> items)
    {
        db.ListingItems.RemoveRange(items);
    }
}