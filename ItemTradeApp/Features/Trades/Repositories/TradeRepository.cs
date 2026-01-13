using ItemTradeApp.Features.Trades.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Trades.Repositories;

public interface ITradeRepository
{
    Task AddAsync(Trade trade, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<Trade?> GetByIdAsync(int tradeId, CancellationToken ct);
    Task<Trade?> GetByIdWithUrlsAsync(int tradeId, CancellationToken ct);
    Task<Trade?> GetTradeDetailsAsync(int tradeId, CancellationToken ct);
    Task<bool> ExistsActiveForOfferAsync(int offerId, CancellationToken ct);
    Task<(int All, int Completed, int MyActive, int Available)> GetMiddlemanStatsAsync(
        int middlemanUserId,
        CancellationToken ct);
    Task<(List<TradeListItemDTO> Items, int TotalCount)> GetTradesByStatusAsync(
        int page,
        int pageSize,
        int? middlemanUserId,
        TradeStatuses status,
        TradesQuery? q,
        CancellationToken ct,
        bool? onlyWithItemsToReturn = false);
}

public sealed class TradeRepository(AppDbContext db) : ITradeRepository
{
    public async Task AddAsync(Trade trade, CancellationToken ct) =>
        await db.Trades.AddAsync(trade, ct).AsTask();

    public async Task SaveChangesAsync(CancellationToken ct) =>
        await db.SaveChangesAsync(ct);

    public async Task<Trade?> GetByIdAsync(int tradeId, CancellationToken ct) =>
       await db.Trades.FirstOrDefaultAsync(t => t.ID == tradeId, ct);

    public async Task<Trade?> GetByIdWithUrlsAsync(int tradeId, CancellationToken ct) =>
       await db.Trades.Include(t => t.Urls).FirstOrDefaultAsync(t => t.ID == tradeId, ct);

    public async Task<bool> ExistsActiveForOfferAsync(int offerId, CancellationToken ct) =>
       await db.Trades.AnyAsync(t =>
            t.Offer_ID == offerId &&
            (t.TradeStatus_ID == (int)TradeStatuses.New ||
             t.TradeStatus_ID == (int)TradeStatuses.InRealization), ct);
    public async Task<Trade?> GetTradeDetailsAsync(int tradeId, CancellationToken ct)
    {
        return await db.Trades
            .AsNoTracking()
            .Include(t => t.Customer)
            .ThenInclude(u => u.ProfileInfo)
            .Include(t => t.PostingUser)
            .ThenInclude(u => u.ProfileInfo)
            .Include(t => t.Urls)
            .FirstOrDefaultAsync(t => t.ID == tradeId, ct);
    }

    public async Task<(int All, int Completed, int MyActive, int Available)> GetMiddlemanStatsAsync(
        int middlemanUserId,
        CancellationToken ct)
    {
        var raw = await db.Trades
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                All = g.Count(),
                Completed = g.Count(t => t.TradeStatus_ID == (int)TradeStatuses.SuccesfulRealization),
                MyActive = g.Count(t =>
                    t.TradeStatus_ID == (int)TradeStatuses.InRealization &&
                    t.MiddlemanUser_ID == middlemanUserId),
                Available = g.Count(t =>
                    t.TradeStatus_ID == (int)TradeStatuses.New &&
                    t.MiddlemanUser_ID == null)
            })
            .SingleOrDefaultAsync(ct);

        return raw is null
            ? (0, 0, 0, 0)
            : (raw.All, raw.Completed, raw.MyActive, raw.Available);
    }


    public async Task<(List<TradeListItemDTO> Items, int TotalCount)> GetTradesByStatusAsync(
    int page,
    int pageSize,
    int? middlemanUserId,
    TradeStatuses status,
    TradesQuery? q,
    CancellationToken ct,
    bool? onlyWithItemsToReturn = false)
{
    q ??= new TradesQuery();

    IQueryable<Trade> query = db.Trades
        .AsNoTracking()
        .Where(t => t.TradeStatus_ID == (int)status);

    if (middlemanUserId is not null)
        query = query.Where(t => t.MiddlemanUser_ID == middlemanUserId.Value);

    if (onlyWithItemsToReturn.Value)
        query = query.Where(t => t.HasBuyersItems || t.HasSellersItems);
    
    query = ApplyFilters(query, q);
    query = ApplySearch(query, q);

    var total = await query.CountAsync(ct);

    query = ApplySorting(query, q.SortBy);

    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(t => new
        {
            t.ID,
            t.Offer_ID,
            t.TokenCost,
            t.TradeStatus_ID,
            t.CreationDate,
            t.MiddlemanUser_ID,

            CustomerId = t.Customer.ID,
            CustomerNick = t.Customer.ProfileInfo.Nickname,
            CustomerEmail = t.Customer.Email,

            PostingId = t.PostingUser.ID,
            PostingNick = t.PostingUser.ProfileInfo.Nickname,
            PostingEmail = t.PostingUser.Email,

            PostingUserItems = t.Offer.ListingItems
                .Select(x => new ItemInfoDTO(x.Item.Name, x.Quantity))
                .ToList(),

            AcceptedCounter = t.Offer.CounterOffers
                .Where(co => co.CounterOfferStatus_Id == (int)CounterOfferStatuses.Accepted)
                .Select(co => new
                {
                    BuyerItems = co.ListingCounterOfferItems
                        .Select(li => new ItemInfoDTO(li.Item.Name, li.Quantity))
                        .ToList()
                })
                .SingleOrDefault()
        })
        .Select(x => new TradeListItemDTO(
            x.ID,
            x.Offer_ID,
            x.TokenCost,
            x.TradeStatus_ID,
            x.CreationDate,
            new InTradeUserDTO(
                x.CustomerId,
                x.CustomerNick,
                x.CustomerEmail,
                x.AcceptedCounter != null ? x.AcceptedCounter.BuyerItems : null
            ),
            new InTradeUserDTO(
                x.PostingId,
                x.PostingNick,
                x.PostingEmail,
                x.PostingUserItems
            ),
            x.MiddlemanUser_ID
        ))
        .ToListAsync(ct);

    return (items, total);
}

    #region HELPERS
    private static IQueryable<Trade> ApplyFilters(
        IQueryable<Trade> query,
        TradesQuery q)
    {

        if (q.MinTokenCost is not null)
            query = query.Where(t => t.TokenCost >= q.MinTokenCost.Value);

        if (q.MaxTokenCost is not null)
            query = query.Where(t => t.TokenCost <= q.MaxTokenCost.Value);

        if (q.CreatedFrom is not null)
            query = query.Where(t => t.CreationDate >= q.CreatedFrom.Value);

        if (q.CreatedTo is not null)
            query = query.Where(t => t.CreationDate <= q.CreatedTo.Value);

        if (q.ReadyForCompletion is not null)
        {
            query = q.ReadyForCompletion.Value
                ? query.Where(t => t.HasBuyersItems && t.HasSellersItems)
                : query.Where(t => !(t.HasBuyersItems && t.HasSellersItems));
        }

        if (q.IsCounterOfferTrade is not null)
        {
            query = q.IsCounterOfferTrade.Value
                ? query.Where(t => t.Offer.CounterOffers
                    .Any(co => co.CounterOfferStatus_Id == (int)CounterOfferStatuses.Accepted))
                : query.Where(t => !t.Offer.CounterOffers
                    .Any(co => co.CounterOfferStatus_Id == (int)CounterOfferStatuses.Accepted));
        }

        return query;
    }
    private static IQueryable<Trade> ApplySearch(
        IQueryable<Trade> query,
        TradesQuery q)
    {
        if (string.IsNullOrWhiteSpace(q.SearchText) || q.SearchBy is null)
            return query;

        var s = q.SearchText.Trim();

        return q.SearchBy.Value switch
        {
            TradeSearchBy.TradeId when int.TryParse(s, out var tradeId)
                => query.Where(t => t.ID == tradeId),

            TradeSearchBy.OfferId when int.TryParse(s, out var offerId)
                => query.Where(t => t.Offer_ID == offerId),

            TradeSearchBy.CustomerNickname
                => query.Where(t =>
                    EF.Functions.ILike(t.Customer.ProfileInfo.Nickname!, $"%{s}%")),

            TradeSearchBy.CustomerEmail
                => query.Where(t =>
                    EF.Functions.ILike(t.Customer.Email, $"%{s}%")),

            TradeSearchBy.PostingUserNickname
                => query.Where(t =>
                    EF.Functions.ILike(t.PostingUser.ProfileInfo.Nickname!, $"%{s}%")),

            TradeSearchBy.PostingUserEmail
                => query.Where(t =>
                    EF.Functions.ILike(t.PostingUser.Email, $"%{s}%")),

            _ => query
        };
    }
    private static IQueryable<Trade> ApplySorting(
        IQueryable<Trade> query,
        TradeSortBy sortBy)
    {
        return sortBy switch
        {
            TradeSortBy.CreationDateAsc
                => query.OrderBy(t => t.CreationDate)
                    .ThenBy(t => t.ID),

            TradeSortBy.CreationDateDesc
                => query.OrderByDescending(t => t.CreationDate)
                    .ThenByDescending(t => t.ID),

            TradeSortBy.TokenCostAsc
                => query.OrderBy(t => t.TokenCost)
                    .ThenByDescending(t => t.CreationDate),

            TradeSortBy.TokenCostDesc
                => query.OrderByDescending(t => t.TokenCost)
                    .ThenByDescending(t => t.CreationDate),

            TradeSortBy.TradeIdAsc
                => query.OrderBy(t => t.ID),

            TradeSortBy.TradeIdDesc
                => query.OrderByDescending(t => t.ID),

            _ => query.OrderByDescending(t => t.CreationDate)
        };
    }

    

    #endregion

     
}
