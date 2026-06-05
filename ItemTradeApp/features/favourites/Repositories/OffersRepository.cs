using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Favourites.Repositories;

public interface IOffersRepository
{
    Task<bool> OfferExistsAsync(int offerId, CancellationToken ct = default);
    Task<bool> OfferIsActiveAsync(int offerId, CancellationToken ct = default);
}

public class OffersRepository(AppDbContext dbContext) : IOffersRepository
{
    public async Task<bool> OfferExistsAsync(int offerId, CancellationToken ct = default)
        => await dbContext.Offers.AsNoTracking().AnyAsync(o => o.ID == offerId, ct);
    
    public async Task<bool> OfferIsActiveAsync(int offerId, CancellationToken ct = default)
        => await dbContext.Offers.AsNoTracking().AnyAsync(o => o.ID == offerId && o.OfferStatus_ID == (int)OfferStatuses.Active, ct);
}