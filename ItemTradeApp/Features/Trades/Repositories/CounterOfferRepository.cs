using ItemTradeApp.Features.Trades.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Trades.Repositories;
public interface ICounterOfferRepository
{
    Task<CounterOfferSide?> CounterOfferHasItemsAsync(int counterOfferId, int offerId, CancellationToken ct);
}
public sealed class CounterOfferRepository(AppDbContext db) : ICounterOfferRepository
{
    public async Task<CounterOfferSide?> CounterOfferHasItemsAsync(int counterOfferId, int offerId, CancellationToken ct)
        => await db.CounterOffers
            .AsNoTracking()
            .Where(co => co.ID == counterOfferId && co.Offer_Id == offerId)
            .Select(co => new CounterOfferSide(co.ListingCounterOfferItems.Any(), co.TokensOffered))
            .FirstOrDefaultAsync(ct);
}