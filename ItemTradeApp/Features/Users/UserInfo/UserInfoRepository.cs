using ItemTradeApp.features.Users.UserInfo.DTOs.Request;
using ItemTradeApp.features.Users.UserInfo.DTOs.Response;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Users.UserInfo;

public interface IUserInfoRepository
{
    Task<int> GetChatUnreadTotalAsync(int userId, CancellationToken ct);
    Task<User?> GetUserWithProfileInfoByUserIdAsync(int id, CancellationToken ct);
    Task<User?> GetUserWithProfileByAuth0IdAsync(string authZeroUserId, CancellationToken ct);
    Task UpdateUserWithProfileInfoAsync(ProfileInfo profile, CancellationToken ct);
    Task<bool> ExistsByAuth0IdAsync(string auth0UserId, CancellationToken ct);
    Task<bool> ExistsByIdAsync(int auth0UserId, CancellationToken ct);

    Task<(int activeOffersCount, int successTradeCount, int completedTradeCount, float rating )?>
        GetUserStatsByUserIdAsync(int id, CancellationToken ct);

    Task<int> GetNumberOfUnreadNotifications(int id, CancellationToken ct);
    
    Task<(List<CounterOfferListItemDto> items, int totalCount)> GetSentCounterOffersAsync(
        int userId,
        CounterOfferListingsQuery query,
        CancellationToken ct);

    Task<(List<CounterOfferListItemDto> items, int totalCount)> GetReceivedCounterOffersAsync(
        int userId,
        CounterOfferListingsQuery query,
        CancellationToken ct);
}
public class UserInfoRepository(AppDbContext dbContext) : IUserInfoRepository
{
    public async Task<bool> ExistsByAuth0IdAsync(string auth0UserId, CancellationToken ct) =>
       await dbContext.Users.AnyAsync(u => u.Auth0UserID == auth0UserId, ct);

    public async Task<int> GetNumberOfUnreadNotifications(int userId, CancellationToken ct) =>
        await dbContext.Notifications
            .Where(n => n.UserId == userId)
            .Where(n => n.ReadAt == null)
            .Where(n => !n.User.IsDeleted)
            .CountAsync(ct);
    public async Task<User?> GetUserWithProfileInfoByUserIdAsync(int id, CancellationToken ct)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .Include(u => u.ProfileInfo)
            .Include(u => u.Chats)
            .ThenInclude(c => c.ChatConversation)
            .SingleOrDefaultAsync(u => u.ID == id, ct);
        return user;
    }
    public async Task<int> GetChatUnreadTotalAsync(int userId, CancellationToken ct)
    {
        var q =
            from cm in dbContext.ConversationMembers.AsNoTracking()
            where cm.UserId == userId
            where dbContext.ChatMessages.AsNoTracking().Any(m =>
                m.ChatConversationId == cm.ChatConversationId &&
                m.DeletedAt == null &&
                m.SenderId != userId &&
                (cm.LastReadMessageId == null || m.Id > cm.LastReadMessageId)
            )
            select 1;

        return await q.CountAsync(ct);
    }
    public async Task<(int activeOffersCount, int successTradeCount, int completedTradeCount, float rating )?> GetUserStatsByUserIdAsync(int id, CancellationToken ct)
    {
        var res = await dbContext.Users.AsNoTracking().Where(u => u.ID == id && !u.IsDeleted)
            .Select(u => new
            {
                ActiveOffers = u.Offers.Count(o => o.OfferStatus_ID == (int)OfferStatuses.Active),
                SuccessTrade = u.OwningTrades.Count(t => t.TradeStatus_ID == (int)TradeStatuses.SuccesfulRealization),
                CompletedTrade = u.OwningTrades.Count(t =>
                    t.TradeStatus_ID == (int)TradeStatuses.SuccesfulRealization ||
                    t.TradeStatus_ID == (int)TradeStatuses.Failed),
                Rating = u.Rates.Select(r => (decimal?)r.Mark).Average() ?? 0m

            }).SingleOrDefaultAsync(ct);
        if (res is null) return null;
        return (res.ActiveOffers,res.SuccessTrade,res.CompletedTrade,(float)res.Rating);
    }

    public async Task<User?> GetUserWithProfileByAuth0IdAsync(string authZeroUserId, CancellationToken ct)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .Include(u => u.ProfileInfo)
            .SingleOrDefaultAsync(u => u.Auth0UserID == authZeroUserId, ct);
    }

    public async Task UpdateUserWithProfileInfoAsync(ProfileInfo profile, CancellationToken ct)
    {
        dbContext.ProfileInfos.Update(profile);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsByIdAsync(int id, CancellationToken ct) =>
        await dbContext.Users.AnyAsync(u => u.ID == id && !u.IsDeleted, ct);
    public async Task<(List<CounterOfferListItemDto> items, int totalCount)> GetSentCounterOffersAsync(
    int userId,
    CounterOfferListingsQuery query,
    CancellationToken ct)
{
    var page = query.Page < 1 ? 1 : query.Page;
    var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

    IQueryable<CounterOffer> localQuery = dbContext.CounterOffers
        .AsNoTracking()
        .Where(co => co.User_ID == userId);

    localQuery = ApplyCounterOfferOrdering(localQuery, ResolveCounterOfferOrderBy(query));

    var totalCount = await localQuery.CountAsync(ct);

    var counterOffers = await localQuery
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Include(co => co.Offer)
        .Include(co => co.OfferStatus)
        .Include(co => co.ListingCounterOfferItems.Where(lci => !lci.Item.IsDeleted))
        .ThenInclude(i => i.Item)
        .ThenInclude(it => it.Game)
        .ToListAsync(ct);

    if (counterOffers.Count == 0)
        return (new List<CounterOfferListItemDto>(), totalCount);

    var ownerIds = counterOffers.Select(x => x.Offer.User_ID).Distinct().ToArray();

    var ownerNickByUserId = await dbContext.ProfileInfos
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

    IQueryable<CounterOffer> localQuery = dbContext.CounterOffers
        .AsNoTracking()
        .Where(co => co.Offer.User_ID == userId);

    localQuery = ApplyCounterOfferOrdering(localQuery, ResolveCounterOfferOrderBy(query));

    var totalCount = await localQuery.CountAsync(ct);

    var counterOffers = await localQuery
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Include(co => co.Offer)
        .Include(co => co.OfferStatus)
        .Include(co => co.ListingCounterOfferItems.Where(lci => !lci.Item.IsDeleted))
        .ThenInclude(i => i.Item)
        .ThenInclude(it => it.Game)
        .ToListAsync(ct);

    if (counterOffers.Count == 0)
        return (new List<CounterOfferListItemDto>(), totalCount);

    var senderIds = counterOffers.Select(x => x.User_ID).Distinct().ToArray();

    var senderNickByUserId = await dbContext.ProfileInfos
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

private static IQueryable<CounterOffer> ApplyCounterOfferOrdering(
    IQueryable<CounterOffer> counterOffers,
    CounterOffersOrderByEnum orderByEnum)
{
    return orderByEnum switch
    {
        CounterOffersOrderByEnum.CreationDateAsc =>
            counterOffers.OrderBy(co => co.CreationDate).ThenBy(co => co.ID),

        CounterOffersOrderByEnum.CreationDateDesc =>
            counterOffers.OrderByDescending(co => co.CreationDate).ThenByDescending(co => co.ID),

        CounterOffersOrderByEnum.TokensAsc =>
            counterOffers.OrderBy(co => co.TokensOffered).ThenBy(co => co.ID),

        CounterOffersOrderByEnum.TokensDesc =>
            counterOffers.OrderByDescending(co => co.TokensOffered).ThenByDescending(co => co.ID),

        _ => counterOffers.OrderByDescending(co => co.CreationDate).ThenByDescending(co => co.ID)
    };
}

private static CounterOffersOrderByEnum ResolveCounterOfferOrderBy(CounterOfferListingsQuery query)
{
    return Enum.IsDefined(typeof(CounterOffersOrderByEnum), query.OrderBy)
        ? query.OrderBy
        : CounterOffersOrderByEnum.CreationDateDesc;
}
}