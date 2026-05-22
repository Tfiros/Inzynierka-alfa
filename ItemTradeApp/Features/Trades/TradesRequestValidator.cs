using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Trades.DTOs;
using ItemTradeApp.Persistence;

namespace ItemTradeApp.Features.Trades;

public interface ITradesRequestValidator
{
    (int Page, int PageSize) Normalize(int page, int pageSize);

    Result<PagedResponse<TradeListItemDTO>>? ValidateTradesQuery(
        TradesQuery? q,
        TradeStatuses scope);
}

public sealed class TradesRequestValidator : ITradesRequestValidator
{
    public (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 10;
        if (pageSize > 100) pageSize = 100;
        return (page, pageSize);
    }

    public Result<PagedResponse<TradeListItemDTO>>? ValidateTradesQuery(
        TradesQuery? q,
        TradeStatuses scope)
    {
        if (q is null) return null;

        var hasSearchText = !string.IsNullOrWhiteSpace(q.SearchText);
        var hasSearchBy = q.SearchBy is not null;

        if (hasSearchText && !hasSearchBy)
            return Result<PagedResponse<TradeListItemDTO>>.BadRequest(
                "searchBy is required when searchText is provided.");

        if (!hasSearchText && hasSearchBy)
            return Result<PagedResponse<TradeListItemDTO>>.BadRequest(
                "searchText is required when searchBy is provided.");

        if (hasSearchText && hasSearchBy &&
            (q.SearchBy == TradeSearchBy.TradeId || q.SearchBy == TradeSearchBy.OfferId) &&
            !int.TryParse(q.SearchText!.Trim(), out _))
        {
            return Result<PagedResponse<TradeListItemDTO>>.BadRequest(
                "searchText must be a number when searchBy is TradeId or OfferId.");
        }

        if (q.CreatedFrom is not null && q.CreatedTo is not null &&
            q.CreatedFrom.Value > q.CreatedTo.Value)
        {
            return Result<PagedResponse<TradeListItemDTO>>.BadRequest(
                "createdFrom cannot be greater than createdTo.");
        }

        if (q.CreatedTo is not null && q.CreatedFrom is null)
        {
            return Result<PagedResponse<TradeListItemDTO>>.BadRequest(
                "createdFrom is required when createdTo is provided.");
        }

        switch (scope)
        {
            case TradeStatuses.New:
            {
                if (q.CreatedTo is not null)
                    return Result<PagedResponse<TradeListItemDTO>>.BadRequest(
                        "createdTo is not supported for available trades.");

                if (q.ReadyForCompletion is not null)
                    return Result<PagedResponse<TradeListItemDTO>>.BadRequest(
                        "readyForCompletion is not applicable for available trades.");

                break;
            }

            case TradeStatuses.SuccesfulRealization:
            {
                if (q.ReadyForCompletion is not null)
                    return Result<PagedResponse<TradeListItemDTO>>.BadRequest(
                        "readyForCompletion is not applicable for completed trades.");

                break;
            }
        }

        return null;
    }
}