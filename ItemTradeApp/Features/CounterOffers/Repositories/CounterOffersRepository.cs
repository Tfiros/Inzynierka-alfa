using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.CounterOffers.Repositories;

public interface ICounterOffersRepository
{
    Task<CounterOffer?> GetCounterOfferWithOfferAndItemsAsync(int counterOfferId, CancellationToken ct);

    Task<(List<CounterOfferListItemDto> items, int totalCount)> GetSentCounterOffersAsync(
        int userId,
        CounterOfferListingsQuery query,
        CancellationToken ct);

    Task<(List<CounterOfferListItemDto> items, int totalCount)> GetReceivedCounterOffersAsync(
        int userId,
        CounterOfferListingsQuery query,
        CancellationToken ct);
    
    void AddCounterOffer(CounterOffer counterOffer);
    Task<List<CounterOffer>> GetOtherPendingCounterOffersForOfferAsync(
        int offerId,
        int acceptedCounterOfferId,
        CancellationToken ct);

}

public sealed class CounterOffersRepository(AppDbContext db) : ICounterOffersRepository
{
    public async Task<CounterOffer?> GetCounterOfferWithOfferAndItemsAsync(int counterOfferId, CancellationToken ct)
    {
        return await db.CounterOffers
            .Include(co => co.Offer)
            .ThenInclude(o => o.ListingItems)
            .Include(co => co.ListingCounterOfferItems)
            .FirstOrDefaultAsync(co => co.ID == counterOfferId, ct);
    }
    public async Task<(List<CounterOfferListItemDto> items, int totalCount)> GetSentCounterOffersAsync(
        int userId,
        CounterOfferListingsQuery query,
        CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        IQueryable<CounterOffer> localQuery = db.CounterOffers
            .AsNoTracking()
            .Where(co => co.User_ID == userId);

        localQuery = ApplyOrdering(localQuery, ResolverOrderBy(query));

        var totalCount = await localQuery.CountAsync(ct);

        var counterOffers = await localQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(co => co.Offer)
            .Include(co => co.OfferStatus)
            .Include(co => co.ListingCounterOfferItems)
            .ThenInclude(i => i.Item)
            .ThenInclude(it => it.Game)
            .ToListAsync(ct);

        if (counterOffers.Count == 0)
            return (new List<CounterOfferListItemDto>(), totalCount);

        var ownerIds = counterOffers.Select(x => x.Offer.User_ID).Distinct().ToArray();

        var ownerNickByUserId = await db.ProfileInfos
            .AsNoTracking()
            .Where(p => ownerIds.Contains(p.User_ID))
            .Select(p => new { p.User_ID, p.Nickname })
            .ToDictionaryAsync(x => x.User_ID, x => x.Nickname, ct);

        var items = counterOffers.Select(counterOffer =>
        {
            ownerNickByUserId.TryGetValue(counterOffer.Offer.User_ID, out var ownerNick);

            return new CounterOfferListItemDto(
                CounterOfferId: counterOffer.ID,
                OfferId: counterOffer.Offer_Id,
                OfferTitle: counterOffer.Offer.Title,
                OfferOwnerUserId: counterOffer.Offer.User_ID,
                CounterOfferUserId: counterOffer.User_ID,
                OtherPartyNickname: ownerNick,
                CreationDate: counterOffer.CreationDate,
                TokensOffered: counterOffer.TokensOffered,
                StatusId: counterOffer.CounterOfferStatus_Id,
                StatusName: counterOffer.OfferStatus.StatusName,
                Items: counterOffer.ListingCounterOfferItems
                    .Select(i => new CounterOfferItemsDto(
                        i.Item_ID,
                        i.Item.Name,
                        i.Item.Photo_URL,
                        i.Item.Game_ID,
                        i.Item.Game.Name,
                        i.Quantity
                    ))
                    .ToList()
            );
        }).ToList();

        return (items, totalCount);
    }

    public async Task<(List<CounterOfferListItemDto> items, int totalCount)> GetReceivedCounterOffersAsync(
        int userId,
        CounterOfferListingsQuery query,
        CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        IQueryable<CounterOffer> localQuery = db.CounterOffers
            .AsNoTracking()
            .Where(co => co.Offer.User_ID == userId);

        localQuery = ApplyOrdering(localQuery, ResolverOrderBy(query));

        var totalCount = await localQuery.CountAsync(ct);

        var counterOffers = await localQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(co => co.Offer)
            .Include(co => co.OfferStatus)
            .Include(co => co.ListingCounterOfferItems)
            .ThenInclude(i => i.Item)
            .ThenInclude(it => it.Game)
            .ToListAsync(ct);

        if (counterOffers.Count == 0)
            return (new List<CounterOfferListItemDto>(), totalCount);

        var senderIds = counterOffers.Select(x => x.User_ID).Distinct().ToArray();

        var senderNickByUserId = await db.ProfileInfos
            .AsNoTracking()
            .Where(p => senderIds.Contains(p.User_ID))
            .Select(p => new { p.User_ID, p.Nickname })
            .ToDictionaryAsync(x => x.User_ID, x => x.Nickname, ct);

        var items = counterOffers.Select(counterOffer =>
        {
            senderNickByUserId.TryGetValue(counterOffer.User_ID, out var senderNickname);

            return new CounterOfferListItemDto(
                CounterOfferId: counterOffer.ID,
                OfferId: counterOffer.Offer_Id,
                OfferTitle: counterOffer.Offer.Title,
                OfferOwnerUserId: counterOffer.Offer.User_ID,
                CounterOfferUserId: counterOffer.User_ID,
                OtherPartyNickname: senderNickname,
                CreationDate: counterOffer.CreationDate,
                TokensOffered: counterOffer.TokensOffered,
                StatusId: counterOffer.CounterOfferStatus_Id,
                StatusName: counterOffer.OfferStatus.StatusName,
                Items: counterOffer.ListingCounterOfferItems
                    .Select(i => new CounterOfferItemsDto(
                        i.Item_ID,
                        i.Item.Name,
                        i.Item.Photo_URL,
                        i.Item.Game_ID,
                        i.Item.Game.Name,
                        i.Quantity
                    ))
                    .ToList()
            );
        }).ToList();

        return (items, totalCount);
    }
    

    public void AddCounterOffer(CounterOffer counterOffer)
    {
        db.CounterOffers.Add(counterOffer);
    }
    
    
    public async Task<List<CounterOffer>> GetOtherPendingCounterOffersForOfferAsync(
        int offerId,
        int acceptedCounterOfferId,
        CancellationToken ct)
    {
        return await db.CounterOffers
            .Where(co => co.Offer_Id == offerId
                         && co.ID != acceptedCounterOfferId
                         && co.CounterOfferStatus_Id == (int)CounterOfferStatuses.Pending)
            .ToListAsync(ct);
    }

    
    private static IQueryable<CounterOffer> ApplyOrdering(
        IQueryable<CounterOffer> counterOffers,
        CounterOffersOrderByEnum orderByEnum)
    {
        return orderByEnum switch
        {
            CounterOffersOrderByEnum.CreationDateAsc =>
                counterOffers.OrderBy(co => co.CreationDate),

            CounterOffersOrderByEnum.CreationDateDesc =>
                counterOffers.OrderByDescending(co => co.CreationDate),

            CounterOffersOrderByEnum.TokensAsc =>
                counterOffers.OrderBy(co => co.TokensOffered),

            CounterOffersOrderByEnum.TokensDesc =>
                counterOffers.OrderByDescending(co => co.TokensOffered),

            _ => counterOffers.OrderByDescending(co => co.CreationDate)
        };
    }
    
    private static CounterOffersOrderByEnum ResolverOrderBy(CounterOfferListingsQuery query)
    {
        return Enum.IsDefined(typeof(CounterOffersOrderByEnum), query.OrderBy)
            ? query.OrderBy
            : CounterOffersOrderByEnum.CreationDateDesc;
    }
}