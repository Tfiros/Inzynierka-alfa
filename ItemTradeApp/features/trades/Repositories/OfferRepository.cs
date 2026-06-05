using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Trades.Repositories;
public interface IOfferRepository
{
    Task<Offer?> GetByIdAsync(int offerId, CancellationToken ct);
}
public sealed class OfferRepository(AppDbContext db) : IOfferRepository
{
    public async Task<Offer?> GetByIdAsync(int offerId, CancellationToken ct) =>
        await db.Offers.FirstOrDefaultAsync(o => o.ID == offerId, ct);
}