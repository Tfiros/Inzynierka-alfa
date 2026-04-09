using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ItemTradeApp.Features.CounterOffers;

public interface ICounterOffersRepository
{
    Task<User?> GetUserInfo(string auth0UserId, CancellationToken ct);
    Task<User?> GetUserEntityByIdAsync(int userId, CancellationToken ct);
    Task<Offer?> GetOfferAsync(int offerId, CancellationToken ct);
    Task<CounterOffer?> GetCounterOfferWithOfferAndItemsAsync(int counterOfferId, CancellationToken ct);
    Task<bool> TradeExistsForOfferAsync(int offerId, CancellationToken ct);
    Task<IReadOnlyList<CounterOfferListItemDto>> GetSentCounterOffersAsync(int userId, CancellationToken ct);
    Task<IReadOnlyList<CounterOfferListItemDto>> GetReceivedCounterOffersAsync(int userId, CancellationToken ct);
    Task<bool> AllItemsExistAsync(int[] itemIds, CancellationToken ct);
    Task<int?> GetOfferOwnerIdAsync(int offerId, CancellationToken ct);
    void AddCounterOffer(CounterOffer counterOffer);
    Task SaveChangesAsync(CancellationToken ct);
    Task<List<CounterOffer>> GetOtherPendingCounterOffersForOfferAsync(
        int offerId,
        int acceptedCounterOfferId,
        CancellationToken ct);
    void RemoveListingItems(IEnumerable<ListingItems> items);
}

public class CounterOffersRepository(AppDbContext db) : ICounterOffersRepository
{
    public async Task<User?> GetUserInfo(string auth0UserId, CancellationToken ct)
    {
        return await db.Users
            .FirstOrDefaultAsync(u => u.Auth0UserID == auth0UserId, ct);
    }

    public async Task<User?> GetUserEntityByIdAsync(int userId, CancellationToken ct)
    {
        return await db.Users
            .FirstOrDefaultAsync(u => u.ID == userId, ct);
    }

    public async Task<Offer?> GetOfferAsync(int offerId, CancellationToken ct)
    {
        return await db.Offers
            .FirstOrDefaultAsync(o => o.ID == offerId, ct);
    }

    public async Task<CounterOffer?> GetCounterOfferWithOfferAndItemsAsync(int counterOfferId, CancellationToken ct)
    {
        return await db.CounterOffers
            .Include(co => co.Offer)
            .ThenInclude(o => o.ListingItems)
            .Include(co => co.ListingCounterOfferItems)
            .FirstOrDefaultAsync(co => co.ID == counterOfferId, ct);
    }

    public async Task<bool> TradeExistsForOfferAsync(int offerId, CancellationToken ct)
    {
        return await db.Trades
            .AsNoTracking()
            .AnyAsync(t => t.Offer_ID == offerId && t.TradeStatus_ID != (int)TradeStatuses.Failed, ct);
    }
    

    public async Task<IReadOnlyList<CounterOfferListItemDto>> GetSentCounterOffersAsync(int userId, CancellationToken ct)
    {
        var counterOffers = await db.CounterOffers
            .AsNoTracking()
            .Where(co => co.User_ID == userId)
            .Include(co => co.Offer)
            .Include(co => co.OfferStatus)
            .Include(co => co.ListingCounterOfferItems)
            .ThenInclude(i => i.Item)
            .ThenInclude(it => it.Game)
            .ToListAsync(ct);

        if (counterOffers.Count == 0)
            return Array.Empty<CounterOfferListItemDto>();

        var ownerIds = counterOffers.Select(x => x.Offer.User_ID).Distinct().ToArray();

        var ownerNickByUserId = await db.ProfileInfos
            .AsNoTracking()
            .Where(p => ownerIds.Contains(p.User_ID))
            .Select(p => new { p.User_ID, p.Nickname })
            .ToDictionaryAsync(x => x.User_ID, x => x.Nickname ?? "", ct);

        return counterOffers.Select(counterOffer =>
        {
            ownerNickByUserId.TryGetValue(counterOffer.Offer.User_ID, out var ownerNick);

            return new CounterOfferListItemDto(
                CounterOfferId: counterOffer.ID,
                OfferId: counterOffer.Offer_Id,
                OfferTitle: counterOffer.Offer?.Title ?? "",
                OfferOwnerUserId: counterOffer.Offer?.User_ID ?? 0,
                CounterOfferUserId: counterOffer.User_ID,
                OtherPartyNickname: ownerNick ?? "",
                CreationDate: counterOffer.CreationDate,
                TokensOffered: counterOffer.TokensOffered,
                StatusId: counterOffer.CounterOfferStatus_Id,
                StatusName: counterOffer.OfferStatus?.StatusName ?? "",
                Items: counterOffer.ListingCounterOfferItems
                    .Select(i => new CounterOfferItemsDto(
                        i.Item_ID,
                        i.Item?.Name ?? "",
                        i.Item?.Photo_URL ?? "",
                        i.Item?.Game_ID ?? 0,
                        i.Item?.Game?.Name ?? "",
                        i.Quantity
                    ))
                    .ToList()
            );
        }).ToList();
    }

    public async Task<IReadOnlyList<CounterOfferListItemDto>> GetReceivedCounterOffersAsync(int userId, CancellationToken ct)
    {
        var counterOffers = await db.CounterOffers
            .AsNoTracking()
            .Where(co => co.Offer.User_ID == userId)
            .Include(co => co.Offer)
            .Include(co => co.OfferStatus)
            .Include(co => co.ListingCounterOfferItems)
            .ThenInclude(i => i.Item)
            .ThenInclude(it => it.Game)
            .ToListAsync(ct);

        if (counterOffers.Count == 0)
            return Array.Empty<CounterOfferListItemDto>();

        var senderIds = counterOffers.Select(x => x.User_ID).Distinct().ToArray();

        var senderNickByUserId = await db.ProfileInfos
            .AsNoTracking()
            .Where(p => senderIds.Contains(p.User_ID))
            .Select(p => new { p.User_ID, p.Nickname })
            .ToDictionaryAsync(x => x.User_ID, x => x.Nickname ?? "", ct);

        return counterOffers.Select(counterOffer =>
        {
            senderNickByUserId.TryGetValue(counterOffer.User_ID, out var senderNickname);

            return new CounterOfferListItemDto(
                CounterOfferId: counterOffer.ID,
                OfferId: counterOffer.Offer_Id,
                OfferTitle: counterOffer.Offer?.Title ?? "",
                OfferOwnerUserId: counterOffer.Offer?.User_ID ?? 0,
                CounterOfferUserId: counterOffer.User_ID,
                OtherPartyNickname: senderNickname ?? "",
                CreationDate: counterOffer.CreationDate,
                TokensOffered: counterOffer.TokensOffered,
                StatusId: counterOffer.CounterOfferStatus_Id,
                StatusName: counterOffer.OfferStatus?.StatusName ?? "",
                Items: counterOffer.ListingCounterOfferItems
                    .Select(i => new CounterOfferItemsDto(
                        i.Item_ID,
                        i.Item?.Name ?? "",
                        i.Item?.Photo_URL ?? "",
                        i.Item?.Game_ID ?? 0,
                        i.Item?.Game?.Name ?? "",
                        i.Quantity
                    ))
                    .ToList()
            );
        }).ToList();
    }

    public async Task<bool> AllItemsExistAsync(int[] itemIds, CancellationToken ct)
    {
        var existingCount = await db.Items.CountAsync(i => itemIds.Contains(i.ID), ct);
        return existingCount == itemIds.Length;
    }

    public async Task<int?> GetOfferOwnerIdAsync(int offerId, CancellationToken ct)
    {
        return await db.Offers
            .AsNoTracking()
            .Where(o => o.ID == offerId)
            .Select(o => (int?)o.User_ID)
            .FirstOrDefaultAsync(ct);
    }

    public void AddCounterOffer(CounterOffer counterOffer)
    {
        db.CounterOffers.Add(counterOffer);
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return db.SaveChangesAsync(ct);
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
    public void RemoveListingItems(IEnumerable<ListingItems> items)
    {
        db.ListingItems.RemoveRange(items);
    }
}