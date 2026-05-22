using ItemTradeApp.Features.Trades.DTOs;
using ItemTradeApp.Features.Trades.Repositories;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Trades;

public interface ITradeListQueryService
{
    Task<(List<TradeListItemDTO> Items, int Total)> GetTradesAsync(
        TradeStatuses status,
        int page,
        int pageSize,
        int callerUserId,
        TradesQuery q,
        bool isMiddlemanView,
        bool onlyWithItemsToReturn,
        CancellationToken ct);

    Task<TradeListItemDTO?> GetTradeByIdAsync(int tradeId, int callerUserId, bool isMiddlemanView, CancellationToken ct);
}

public sealed class TradeListQueryService(ITradeRepository tradeRepo) : ITradeListQueryService
{

    public async Task<TradeListItemDTO?> GetTradeByIdAsync(int tradeId, int callerUserId, bool isMiddlemanView,
        CancellationToken ct)
    {
        var query = tradeRepo.QueryNoTracking()
            .Where(t => t.ID == tradeId)
            .Where(t => t.Customer_ID == callerUserId || t.User_ID == callerUserId ||
                        t.MiddlemanUser_ID == callerUserId);
        return await ProjectToListItemDto(query, isMiddlemanView).SingleOrDefaultAsync(ct);
    }

    public async Task<(List<TradeListItemDTO> Items, int Total)> GetTradesAsync(
        TradeStatuses status,
        int page,
        int pageSize,
        int callerUserId,
        TradesQuery q,
        bool isMiddlemanView,
        bool onlyWithItemsToReturn,
        CancellationToken ct)
    {
        q ??= new TradesQuery();

        var query = tradeRepo.QueryNoTracking()
            .Where(t => t.TradeStatus_ID == (int)status);
        if (isMiddlemanView)
        {
            query = status == TradeStatuses.New
                ? query.Where(t => t.MiddlemanUser_ID == null)
                : query.Where(t => t.MiddlemanUser_ID == callerUserId);
        }
        else
        {
            query = query.Where(t => t.PostingUser.ID == callerUserId);
        }

        if (onlyWithItemsToReturn)
            query = query.Where(t => t.HasBuyersItems || t.HasSellersItems);

        query = ApplyFilters(query, q);
        query = ApplySearch(query, q);

        var total = await query.CountAsync(ct);

        query = ApplySorting(query, q.SortBy);

        var items = await ProjectToListItemDto(query, isMiddlemanView)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    private static IQueryable<TradeListItemDTO> ProjectToListItemDto(
        IQueryable<Trade> query,
        bool isMiddlemanView)
        => query
            .Select(t => new TradeListItemDTO(
                t.ID,
                t.Offer_ID,
                t.TradeStatus_ID,
                t.CreationDate,
                new InTradeUserDTO(
                    t.Customer.ID,
                    t.Customer.ProfileInfo.Nickname,
                    isMiddlemanView ? t.Customer.Email : null,
                    t.Offer.ListingItems
                        .Where(x => x.IsWanted)
                        .Select(x => new ItemInfoDTO(x.Item.Name, x.Quantity))
                        .ToList()
                ),
                new InTradeUserDTO(
                    t.PostingUser.ID,
                    t.PostingUser.ProfileInfo.Nickname,
                    isMiddlemanView ? t.PostingUser.Email : null,
                    t.Offer.ListingItems
                        .Where(x => !x.IsWanted)
                        .Select(x => new ItemInfoDTO(x.Item.Name, x.Quantity))
                        .ToList()
                ),
                t.MiddlemanUser_ID,
                t.Offer.TokensOffered,
                t.Offer.TokensWanted
            ));
    
    private static IQueryable<Trade> ApplyFilters(IQueryable<Trade> query, TradesQuery q)
    {

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

    private static IQueryable<Trade> ApplySearch(IQueryable<Trade> query, TradesQuery q)
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
                => query.Where(t => EF.Functions.ILike(t.Customer.ProfileInfo.Nickname!, $"%{s}%")),

            TradeSearchBy.CustomerEmail
                => query.Where(t => EF.Functions.ILike(t.Customer.Email, $"%{s}%")),

            TradeSearchBy.PostingUserNickname
                => query.Where(t => EF.Functions.ILike(t.PostingUser.ProfileInfo.Nickname!, $"%{s}%")),

            TradeSearchBy.PostingUserEmail
                => query.Where(t => EF.Functions.ILike(t.PostingUser.Email, $"%{s}%")),

            _ => query
        };
    }

    private static IQueryable<Trade> ApplySorting(IQueryable<Trade> query, TradeSortBy sortBy)
        => sortBy switch
        {
            TradeSortBy.CreationDateAsc
                => query.OrderBy(t => t.CreationDate).ThenBy(t => t.ID),

            TradeSortBy.CreationDateDesc
                => query.OrderByDescending(t => t.CreationDate).ThenByDescending(t => t.ID),

            TradeSortBy.TradeIdAsc
                => query.OrderBy(t => t.ID),

            TradeSortBy.TradeIdDesc
                => query.OrderByDescending(t => t.ID),

            _ => query.OrderByDescending(t => t.CreationDate)
        };
}
