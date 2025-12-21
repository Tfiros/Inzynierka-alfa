using ItemTradeApp.Features.TradeFeatures.Items.DTOs.ResponseDTOs;
using ItemTradeApp.Features.TradeFeatures.Offers.DTOs.ResponseDTOs;

namespace ItemTradeApp.Features.TradeFeatures.Offers;

public interface IItemService
{
    Task<Result<OfferListingsPagedReponse>>
        GetOffersAsync(OfferListingsQuery query, CancellationToken ct = default);
}

public class OfferService(IOffersRepository repo) : IItemService
{
    public async Task<Result<OfferListingsPagedReponse>> GetOffersAsync(OfferListingsQuery query,
        CancellationToken ct)
    {
        if (query is null)
        {
            return Result<OfferListingsPagedReponse>.BadRequest("body_required");
        }

        
        var page = query.Page< 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 10 : query.PageSize;

        var repoQuery = new OfferListingsQuery
        {
            Page = page,
            PageSize = pageSize,
            GameId = query.GameId,
            GenreId = query.GenreId,
            SearchText = query.SearchText,
            OrderBy = query.OrderBy
        };

        var (items, hasNext) = await repo.GetMarketplaceOffersAsync(repoQuery, ct);

        var res = new OfferListingsPagedReponse { Page = page, PageSize = pageSize,HasNextPage = hasNext, Items = items.ToList()};

        return Result<OfferListingsPagedReponse>.Success(res);
    }
}