using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Favourites.Repositories;

public interface IFavouritesRepository
{

    Task<List<int>> GetFavouriteIdsAsync(int userId, CancellationToken ct = default);
    Task<bool> FavouriteExistsAsync(int userId, int offerId, CancellationToken ct = default);

    void Add(UserFavouriteOffer userFavouriteOffer);
    Task<int> RemoveAsync(int userId, int offerId, CancellationToken ct = default);
    
    Task<(List<OfferListingDTO> favourites, int totalCount)> GetFavouriteOffersPagedAsync(int userId, int page, int pageSize, CancellationToken ct = default);
}

public class FavouritesRepository(AppDbContext dbContext) : IFavouritesRepository
{
    public async Task<List<int>> GetFavouriteIdsAsync(int userId, CancellationToken ct = default)
    {
        var favouriteIds = await dbContext.UserFavouriteOffers.AsNoTracking()
            .Where(f => f.User_ID == userId)
            .Select(f => f.Offer_ID)
            .ToListAsync(ct);
        return favouriteIds;
    }

    public Task<bool> FavouriteExistsAsync(int userId, int offerId, CancellationToken ct = default)
        => dbContext.UserFavouriteOffers.AsNoTracking()
            .AnyAsync(f => f.User_ID == userId && f.Offer_ID == offerId, ct);

    public void Add(UserFavouriteOffer userFavouriteOffer)
        => dbContext.UserFavouriteOffers.Add(userFavouriteOffer);

    public Task<int> RemoveAsync(int userId, int offerId, CancellationToken ct = default)
        => dbContext.UserFavouriteOffers.Where(f => f.User_ID == userId && f.Offer_ID == offerId)
            .ExecuteDeleteAsync(ct);

    public async Task<(List<OfferListingDTO> favourites, int totalCount)> GetFavouriteOffersPagedAsync(int userId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var baseQuery = dbContext.UserFavouriteOffers.AsNoTracking().Where(f => f.User_ID == userId);

        var totalCount = await baseQuery.CountAsync(ct);

        var favourites = await baseQuery
            .OrderByDescending(f => f.AddedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => f.Offer)
            .SelectOfferListingDto()
            .ToListAsync(ct);

        return (favourites, totalCount);
    }
}