using ItemTradeApp.Features.Trades.DTOs;
using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Trades.Repositories;
public interface IOfferRepository
{ 
    Task<OfferSide?> GetOfferSidesItems(int offerId, CancellationToken ct);
}
public sealed class OfferRepository(AppDbContext db) : IOfferRepository
{
    public async Task<OfferSide?> GetOfferSidesItems(int offerId, CancellationToken ct)
        => await db.Offers
            .AsNoTracking()
            .Where(o => o.ID == offerId)
            .Select(o => new OfferSide(
                o.ListingItems.Any(li => !li.IsWanted),
                o.ListingItems.Any(li => li.IsWanted),
                o.TokensOffered,
                o.TokensWanted))
            .FirstOrDefaultAsync(ct);
}