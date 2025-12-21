using ItemTradeApp.Features.TradeFeatures.Items.DTOs.ResponseDTOs;
using ItemTradeApp.Features.TradeFeatures.Offers.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.TradeFeatures.Offers;

public interface IOffersRepository
{
    Task<(IReadOnlyList<OfferListingDTO> offers,bool hasNext)> GetMarketplaceOffersAsync(
        OfferListingsQuery query, CancellationToken ct = default);
}

public class OfferRepository(AppDbContext dbContext) : IOffersRepository
{
    public async Task<(IReadOnlyList<OfferListingDTO> offers, bool hasNext)> GetMarketplaceOffersAsync(OfferListingsQuery query,
        CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 10 : query.PageSize;
        
        
        var localQuery = dbContext.Offers.AsNoTracking().AsQueryable();
        localQuery = localQuery.Where(o => o.OfferStatus_ID==(int)OfferStatuses.Active);
        localQuery = ApplyFiltering(localQuery, query);
        localQuery = ApplyOrdering(localQuery, ResolverOrderBy(query)); 
        
        var offersPlusOne = await localQuery.Where(o=>o.User.ProfileInfo != null).Skip((page - 1) * pageSize).Take(pageSize+1).Select(o => new OfferListingDTO(o.ID,
            o.ExpDate,o.CreationDate, o.TokenCost, o.OfferStatus_ID,
            new OfferUserDTO(o.User_ID, o.User.ProfileInfo!.Nickname, o.User.ProfileInfo!.ImageUrl),
            o.ListingItems.Select(li => new OfferListingItemDTO(li.Item.ID, li.Item.Name, li.Item.Game.ID,
                    li.Item.Photo_URL, li.Quantity, li.Item.Game.Name, li.Item.Game.Genre.ID, li.Item.Game.Genre.Name))
                .ToList())).ToListAsync(ct);
        var hasNext = offersPlusOne.Count > pageSize;
        var offers = offersPlusOne.Take(pageSize).ToList();
        return (offers,hasNext);

    }

    private static IQueryable<Offer> ApplyOrdering(IQueryable<Offer> offers, OffersOrderByEnum orderByEnum)
    {
        return orderByEnum switch
        {
            OffersOrderByEnum.CreationDateAsc => offers.OrderBy(o => o.CreationDate),
            OffersOrderByEnum.CreationDateDesc => offers.OrderByDescending(o => o.CreationDate),

            OffersOrderByEnum.PriceAsc => offers.OrderBy(o => o.TokenCost),
            OffersOrderByEnum.PriceDesc => offers.OrderByDescending(o => o.TokenCost),

            OffersOrderByEnum.ExpiryAsc => offers.OrderBy(o => o.ExpDate),
            OffersOrderByEnum.ExpiryDesc => offers.OrderByDescending(o => o.ExpDate),
            
            _ => offers.OrderBy(o=>o.CreationDate)

        };
    }

    private static IQueryable<Offer> ApplyFiltering(IQueryable<Offer> offers, OfferListingsQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var s = query.SearchText.Trim();

            offers = offers.Where(o => o.ListingItems.Any(li => EF.Functions.ILike(li.Item.Name, $"%{s}%")));
        }

        var gameId = query.GameId;
        var genreId = query.GenreId;

        if (gameId.HasValue || genreId.HasValue)
        {
            offers = offers.Where(o => o.ListingItems.Any(li => (!gameId.HasValue|| li.Item.Game_ID==gameId.Value) && (!genreId.HasValue || li.Item.Game.Genre_ID==genreId.Value)));
        }

        return offers;
    }

    private static OffersOrderByEnum ResolverOrderBy(OfferListingsQuery query)
    {
        return Enum.IsDefined(typeof(OffersOrderByEnum), query.OrderBy)
            ? (OffersOrderByEnum)query.OrderBy
            : OffersOrderByEnum.CreationDateAsc;
    }

}