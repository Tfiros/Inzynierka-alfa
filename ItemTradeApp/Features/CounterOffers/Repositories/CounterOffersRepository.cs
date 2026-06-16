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
    
    void AddCounterOffer(CounterOffer counterOffer);
    Task<List<CounterOffer>> GetOtherPendingCounterOffersForOfferAsync(
        int offerId,
        int acceptedCounterOfferId,
        CancellationToken ct);

    Task<List<CounterOfferListItemDto>> GetPendingCounterOffersForOfferAsync(
        int offerId,
        CancellationToken ct);
    
    Task<bool> HasPendingForOfferAsync(int offerId, CancellationToken ct);
}

public sealed class CounterOffersRepository(AppDbContext db) : ICounterOffersRepository
{
    public async Task<CounterOffer?> GetCounterOfferWithOfferAndItemsAsync(
        int counterOfferId,
        CancellationToken ct)
    {
        return await db.CounterOffers
            .AsSplitQuery()
            .Include(co => co.User)
            .ThenInclude(u => u.ProfileInfo)
            .Include(co => co.Offer)
            .ThenInclude(o => o.User)
            .ThenInclude(u => u.ProfileInfo)
            .Include(co => co.Offer)
            .ThenInclude(o => o.ListingItems)
            .ThenInclude(li => li.Item)
            .Include(co => co.ListingCounterOfferItems)
            .ThenInclude(li => li.Item)
            .FirstOrDefaultAsync(co => co.ID == counterOfferId, ct);
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
    public async Task<List<CounterOfferListItemDto>> GetPendingCounterOffersForOfferAsync(
    int offerId,
    CancellationToken ct)
{
    var counterOffers = await db.CounterOffers
        .AsNoTracking()
        .Where(co =>
            co.Offer_Id == offerId &&
            co.CounterOfferStatus_Id == (int)CounterOfferStatuses.Pending)
        .OrderByDescending(co => co.CreationDate)
        .Include(co => co.Offer)
        .Include(co => co.OfferStatus)
        .Include(co => co.ListingCounterOfferItems.Where(lci => !lci.Item.IsDeleted))
        .ThenInclude(i => i.Item)
        .ThenInclude(it => it.Game)
        .ToListAsync(ct);

    if (counterOffers.Count == 0)
        return new List<CounterOfferListItemDto>();

    var senderIds = counterOffers.Select(x => x.User_ID).Distinct().ToArray();

    var senderNickByUserId = await db.ProfileInfos
        .AsNoTracking()
        .Where(p => senderIds.Contains(p.User_ID))
        .Select(p => new { p.User_ID, p.Nickname })
        .ToDictionaryAsync(x => x.User_ID, x => x.Nickname, ct);

    return counterOffers.Select(counterOffer =>
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
}
    
    public async Task<bool> HasPendingForOfferAsync(int offerId, CancellationToken ct)
        => await db.CounterOffers.AnyAsync(
            co => co.Offer_Id == offerId && co.CounterOfferStatus_Id == (int)CounterOfferStatuses.Pending, ct);
}