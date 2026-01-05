using ItemTradeApp.Features.Offers.DTOs.RequestDTOs;
using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Offers;

public interface IOffersRepository
{
    Task<(List<OfferListingDTO> offers, int totalCount)> GetOffersPagedAsync(
        OfferListingsQuery query, CancellationToken ct = default);
    Task<Dictionary<int, Item>> GetItemsByIdsAsync(IReadOnlyCollection<int> itemsIds, CancellationToken ct = default);
    Task<bool> CancelOfferAsync(int userId, int offerId, CancellationToken ct = default);
    void Add(Offer offer);
    void RemoveListingItemsRange(IEnumerable<ListingItems> items);
    void AddListingItemsRange(IEnumerable<ListingItems> items);

    Task<Offer?> GetTrackedOfferAsync(int offerId, int userId, CancellationToken ct = default);


}

public class OfferRepository(AppDbContext dbContext) : IOffersRepository
{
    public void RemoveListingItemsRange(IEnumerable<ListingItems> items)
        => dbContext.RemoveRange(items);
    public void AddListingItemsRange(IEnumerable<ListingItems> items)
        => dbContext.AddRange(items);
    public void Add(Offer offer) 
        => dbContext.Offers.Add(offer);
    
    public async Task<Offer?> GetTrackedOfferAsync(int offerId, int userId, CancellationToken ct = default){
        return await dbContext.Offers.Include(o => o.ListingItems)
            .SingleOrDefaultAsync(o => o.ID == offerId && o.User_ID == userId, ct);
    }
    
    public async Task<bool> CancelOfferAsync(int userId, int offerId, CancellationToken ct = default)
    {
        var updated = await dbContext.Offers
            .Where(o => o.ID == offerId && o.User_ID == userId && o.OfferStatus_ID == (int)OfferStatuses.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OfferStatus_ID, _ => (int)OfferStatuses.Canceled), ct);
        return updated == 1;
    }
    
    public async Task<(List<OfferListingDTO> offers, int totalCount)> GetOffersPagedAsync(
        OfferListingsQuery query,
        CancellationToken ct = default)
    {

        var page = query.Page;
        var pageSize = query.PageSize;

        var localQuery = dbContext.Offers.AsNoTracking().AsQueryable();
        localQuery = localQuery.Where(o => o.OfferStatus_ID == (int)OfferStatuses.Active);
        localQuery = ApplyFiltering(localQuery, query);
        localQuery = ApplyOrdering(localQuery, ResolverOrderBy(query));

        var totalCount = await localQuery.CountAsync(ct);

        var offers = await localQuery.Skip((page - 1) * pageSize)
            .Take(pageSize).Select(o => new OfferListingDTO
            (new OfferCoreDTO(o.ID,o.ExpDate,o.CreationDate,o.TokenCost),
              new OfferUserDTO
                    (
                        o.User_ID,
                        o.User.ProfileInfo!.Nickname,
                        o.User.ProfileInfo!.ImageUrl
                    ),
              o.OfferStatus_ID, 
              o.ListingItems.Select(li => 
                    new OfferListingItemDTO
                        (
                            li.Item.ID,
                            li.Item.Name,
                            li.Item.Game.ID,
                            li.Item.Photo_URL,
                            li.Quantity,
                            li.Item.Game.Name,
                            li.Item.Game.Genre.ID,
                            li.Item.Game.Genre.Name 
                        )).ToList()
            )).ToListAsync(ct);
        return (offers, totalCount);

    }
    
    public async Task<Dictionary<int, Item>> GetItemsByIdsAsync(IReadOnlyCollection<int> itemsIds, CancellationToken ct = default)
    {
        if (itemsIds.Count == 0)
        {
            return new Dictionary<int, Item>();
        }

        return await dbContext.Items.AsNoTracking().Where(i => itemsIds.Contains(i.ID) && !i.IsDeteled)
            .ToDictionaryAsync(i => i.ID, ct);
    }

    #region offerRepoHelpers

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
            ? query.OrderBy
            : OffersOrderByEnum.CreationDateAsc;
    }

    #endregion
    

}