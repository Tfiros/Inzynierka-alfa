using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Trades.DTOs;
using ItemTradeApp.Features.Trades.DTOs.Request;
using ItemTradeApp.Features.Trades.DTOs.Response;
using ItemTradeApp.Features.Trades.Repositories;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Trades;
public interface ITradesService
{
    Task<Result<string>> AssignMiddlemanAsync(
        AssignMiddlemanRequest? request,
        string? auth0UserId,
        CancellationToken ct);

    Task<Result<int>> CreateAsync(
        CreateTradeRequest? request,
        string? auth0UserId,
        CancellationToken ct);

    Task<Result<string>> UpdateTradeByMiddlemanAsync(
        int tradeId,
        UpdateTradeRequest? request,
        string? auth0UserId,
        CancellationToken ct);

    Task<Result<TradeDetailsResponse>> GetTradeDetailsAsync(
        string? auth0UserId,
        int tradeId,
        CancellationToken ct);
    Task<Result<MiddlemanTradesStatsResponse>> GetMiddlemanStatsAsync(string? auth0UserId, CancellationToken ct);

    Task<Result<PagedResponse<TradeListItemDTO>>> GetAvailableNewAsync(int page, int pageSize, TradesQuery? q, CancellationToken ct);
    Task<Result<PagedResponse<TradeListItemDTO>>> GetMyInRealizationAsync(string? auth0UserId, int page, int pageSize, TradesQuery? query, CancellationToken ct);
    Task<Result<PagedResponse<TradeListItemDTO>>> GetMyCompletedAsync(string? auth0UserId, int page, int pageSize, TradesQuery? query, CancellationToken ct);

}
public sealed class TradesService(
    ITradeRepository tradeRepo,
    IOfferRepository offerRepo,
    ICounterOfferRepository counterOfferRepo,
    IUserRepository userRepo,
    IUnitOfWork unitOfWork
) : ITradesService
{
    public async Task<Result<MiddlemanTradesStatsResponse>> GetMiddlemanStatsAsync(
        string? auth0UserId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<MiddlemanTradesStatsResponse>.Unauthorized("Missing auth0 user id (sub claim).");

        var trimmed = TrimAuth0UserId(auth0UserId);

        var middleman = await userRepo.GetByAuth0UserIdAsync(trimmed!, ct);
        if (middleman is null)
            return Result<MiddlemanTradesStatsResponse>.Unauthorized("User not found for given auth0 user id.");

        var (all, completed, myActive, available) = await tradeRepo.GetMiddlemanStatsAsync(middleman.ID, ct);

        var dto = new MiddlemanTradesStatsResponse(
            All: all,
            Completed: completed,
            MyActive: myActive,
            Available: available
        );

        return Result<MiddlemanTradesStatsResponse>.Success(dto, "Successfully retrieved.");
    }

     public async Task<Result<int>> CreateAsync(CreateTradeRequest? request, string? auth0UserId, CancellationToken ct)
{
    if (request is null)
        return Result<int>.BadRequest("Body is required.");

    if (string.IsNullOrWhiteSpace(auth0UserId))
        return Result<int>.Unauthorized("Missing auth0 user id (sub claim).");

    var trimmedAuth0UserId = TrimAuth0UserId(auth0UserId);

    var caller = await userRepo.GetByAuth0UserIdAsync(trimmedAuth0UserId, ct);
    if (caller is null)
        return Result<int>.Unauthorized("User not found for given auth0 user id.");

    var offer = await offerRepo.GetByIdAsync(request.OfferId, ct);
    if (offer is null)
        return Result<int>.NotFound("Offer not found.");

    if (await tradeRepo.ExistsActiveForOfferAsync(request.OfferId, ct))
        return Result<int>.Conflict("Trade already exists for this offer.");

    if (offer.OfferStatus_ID != (int)OfferStatuses.Active)
        return Result<int>.BadRequest("Offer is not active.");

    var postingUserId = offer.User_ID;

    int customerId;
    int tokenCost;
    CounterOffer? acceptedCounterOffer = null;
    
    if (request.CounterOfferId is not null)
    {
        if (caller.ID != postingUserId)
            return Result<int>.Forbidden("Only offer owner can accept a counteroffer.");

        var counterOffer = await counterOfferRepo.GetByIdAsync(request.CounterOfferId.Value, ct);
        if (counterOffer is null)
            return Result<int>.NotFound("CounterOffer not found.");

        if (counterOffer.Offer_Id != request.OfferId)
            return Result<int>.BadRequest("CounterOffer does not belong to given Offer.");

        if (counterOffer.CounterOfferStatus_Id != (int)CounterOfferStatuses.Pending)
            return Result<int>.BadRequest("CounterOffer is not pending.");

        customerId = counterOffer.User_ID;
        if (customerId == postingUserId)
            return Result<int>.BadRequest("Offer owner cannot accept their own counteroffer.");

        tokenCost = counterOffer.TokensOffered;
        acceptedCounterOffer = counterOffer;
    }
    else
    {
        customerId = request.CustomerId;

        var customer = await userRepo.GetByIdAsync(customerId, ct);
        if (customer is null)
            return Result<int>.NotFound("Customer not found.");

        if (caller.ID != customerId)
            return Result<int>.Forbidden("You cannot create trade for another customer.");

        if (customerId == postingUserId)
            return Result<int>.BadRequest("Customer cannot buy their own offer.");

        tokenCost = offer.TokenCost;
    }

    await using var tx = await unitOfWork.BeginTransactionAsync(ct);
    try
    {
        offer.OfferStatus_ID = (int)OfferStatuses.InRealization;

        if (acceptedCounterOffer is not null)
        {
            acceptedCounterOffer.CounterOfferStatus_Id = (int)CounterOfferStatuses.Accepted;

            await counterOfferRepo.DenyOtherPendingForOfferAsync(
                request.OfferId,
                acceptedCounterOffer.ID,
                ct);
        }

        var trade = new Trade
        {
            Offer_ID = request.OfferId,
            Customer_ID = customerId,
            User_ID = postingUserId,
            TokenCost = tokenCost,

            CreationDate = DateTime.UtcNow,
            CompletitionDate = null,
            TradeStatus_ID = (int)TradeStatuses.New,

            HasBuyersItems = false,
            HasSellersItems = false,
        };

        await tradeRepo.AddAsync(trade, ct);

        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<int>.Success(trade.ID);
    }
    catch
    {
        await tx.RollbackAsync(ct);
        throw;
    }
}

    public async Task<Result<string>> AssignMiddlemanAsync(
        AssignMiddlemanRequest? request,
        string? auth0UserId,
        CancellationToken ct)
    {
        if (request is null)
            return Result<string>.BadRequest("Body is required.");

        if (request.TradeId <= 0)
            return Result<string>.BadRequest("TradeId must be > 0.");
        
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<string>.Unauthorized("Missing auth0 user id (sub claim).");
        string trimmedAuth0UserId = TrimAuth0UserId(auth0UserId);

        var middleman = await userRepo.GetByAuth0UserIdAsync(trimmedAuth0UserId, ct);
        if (middleman is null)
            return Result<string>.Unauthorized("User not found for given auth0 user id.");

        var trade = await tradeRepo.GetByIdAsync(request.TradeId, ct);
        if (trade is null)
            return Result<string>.NotFound("Trade not found.");
        if (trade.TradeStatus_ID != (int)TradeStatuses.New)
            return Result<string>.BadRequest("Trade is not in NEW status.");

        if (trade.MiddlemanUser_ID is not null)
            return Result<string>.Conflict("Trade already has a middleman assigned.");

        trade.MiddlemanUser_ID = middleman.ID;
        trade.TradeStatus_ID = (int)TradeStatuses.InRealization;

        await tradeRepo.SaveChangesAsync(ct);

        return Result<string>.Success("Middleman assigned.");
    }
      public async Task<Result<string>> UpdateTradeByMiddlemanAsync(
        int tradeId,
        UpdateTradeRequest? request,
        string? auth0UserId,
        CancellationToken ct)
    {
        if (tradeId <= 0)
            return Result<string>.BadRequest("TradeId must be > 0.");

        if (request is null)
            return Result<string>.BadRequest("Body is required.");

        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<string>.Unauthorized("Missing auth0 user id (sub claim).");
        string trimmedAuth0UserId = TrimAuth0UserId(auth0UserId);
        var middleman = await userRepo.GetByAuth0UserIdAsync(trimmedAuth0UserId, ct);
        if (middleman is null)
            return Result<string>.Unauthorized("User not found for given auth0 user id.");

        var trade = await tradeRepo.GetByIdWithUrlsAsync(tradeId, ct);
        if (trade is null)
            return Result<string>.NotFound("Trade not found.");

        if (trade.MiddlemanUser_ID is null)
            return Result<string>.BadRequest("Trade has no middleman assigned.");

        if (trade.MiddlemanUser_ID != middleman.ID)
            return Result<string>.Forbidden("You are not assigned to this trade.");

        if (trade.TradeStatus_ID != (int)TradeStatuses.InRealization)
            return Result<string>.BadRequest("Trade is not in InRealization status.");

        if (request.HasBuyerItems is not null)
            trade.HasBuyersItems = request.HasBuyerItems.Value;

        if (request.HasSellerItems is not null)
            trade.HasSellersItems = request.HasSellerItems.Value;
        
        if (trade.Urls.Count == 0)
        {
            trade.Urls.Add(new TradeUrl { TradeId = trade.ID, PhotoUrl = "" });
            trade.Urls.Add(new TradeUrl { TradeId = trade.ID, PhotoUrl = "" });
        }

        await tradeRepo.SaveChangesAsync(ct);

        return Result<string>.Success("Trade updated.");
    }


    public async Task<Result<PagedResponse<TradeListItemDTO>>> GetAvailableNewAsync(
        int page,
        int pageSize,
        TradesQuery? query,
        CancellationToken ct)
    {
        var (p, ps) = Normalize(page, pageSize);
        
        var invalid = ValidateTradesQuery(query, TradeStatuses.New);
        if (invalid is not null) return invalid;
        var (items, total) = await tradeRepo.GetTradesByStatusAsync(
            p, ps, middlemanUserId: null, status: TradeStatuses.New, query, ct);
        var resp = ToPaged(p, ps, total, items);

        return Result<PagedResponse<TradeListItemDTO>>.Success(resp, "Successfully retrieved.");
    }


    public async Task<Result<PagedResponse<TradeListItemDTO>>> GetMyInRealizationAsync(
        string? auth0UserId,
        int page,
        int pageSize,
        TradesQuery? query,
        CancellationToken ct)
    {
        var (p, ps) = Normalize(page, pageSize);

        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<PagedResponse<TradeListItemDTO>>.Unauthorized("Missing auth0 user id (sub claim).");
        var invalid = ValidateTradesQuery(query, TradeStatuses.InRealization);
        if (invalid is not null) return invalid;
        var trimmed = TrimAuth0UserId(auth0UserId);

        var middleman = await userRepo.GetByAuth0UserIdAsync(trimmed!, ct);
        if (middleman is null)
            return Result<PagedResponse<TradeListItemDTO>>.Unauthorized("User not found for given auth0 user id.");
        
        var (items, total) = await tradeRepo.GetTradesByStatusAsync(
            p, ps, middleman.ID, TradeStatuses.InRealization, query, ct);

        var resp = ToPaged(p, ps, total, items);
        return Result<PagedResponse<TradeListItemDTO>>.Success(resp, "Successfully retrieved.");
    }

    public async Task<Result<PagedResponse<TradeListItemDTO>>> GetMyCompletedAsync(
        string? auth0UserId,
        int page,
        int pageSize,
        TradesQuery? query,
        CancellationToken ct)
    {
        var (p, ps) = Normalize(page, pageSize);

        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<PagedResponse<TradeListItemDTO>>.Unauthorized("Missing auth0 user id (sub claim).");
        var invalid = ValidateTradesQuery(query, TradeStatuses.SuccesfulRealization);
        if (invalid is not null) return invalid;
        var trimmed = TrimAuth0UserId(auth0UserId);

        var middleman = await userRepo.GetByAuth0UserIdAsync(trimmed!, ct);
        if (middleman is null)
            return Result<PagedResponse<TradeListItemDTO>>.Unauthorized("User not found for given auth0 user id.");
        
        var (items, total) = await tradeRepo.GetTradesByStatusAsync(
            p, ps, middleman.ID, TradeStatuses.SuccesfulRealization, query, ct);

        var resp = ToPaged(p, ps, total, items);
        return Result<PagedResponse<TradeListItemDTO>>.Success(resp, "Successfully retrieved.");
    }
    public async Task<Result<TradeDetailsResponse>> GetTradeDetailsAsync(
        string? auth0UserId,
        int tradeId,
        CancellationToken ct)
    {
        if (tradeId <= 0)
            return Result<TradeDetailsResponse>.BadRequest("tradeId must be > 0.");

        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<TradeDetailsResponse>.Unauthorized("Missing auth0 user id (sub claim).");

        var trimmed = TrimAuth0UserId(auth0UserId);

        var middleman = await userRepo.GetByAuth0UserIdAsync(trimmed!, ct);
        if (middleman is null)
            return Result<TradeDetailsResponse>.Unauthorized("User not found for given auth0 user id.");

        var trade = await tradeRepo.GetTradeDetailsAsync(tradeId, ct);
        if (trade is null)
            return Result<TradeDetailsResponse>.NotFound("Trade not found.");

        if (trade.MiddlemanUser_ID != middleman.ID)
            return Result<TradeDetailsResponse>.Forbidden("You are not assigned to this trade.");

        var buyer = trade.Customer;
        var seller = trade.PostingUser;

        var buyerPhotos = trade.Urls
            .Where(u => u.IsBuyers)
            .Select(u => u.PhotoUrl)
            .ToList();

        var sellerPhotos = trade.Urls
            .Where(u => !u.IsBuyers)
            .Select(u => u.PhotoUrl)
            .ToList();

        var dto = new TradeDetailsResponse(
            hasBuyersItems: trade.HasBuyersItems,
            hasSellersItems: trade.HasSellersItems,
            buyingUserPhotos: new InTradeUserPhotos(
                buyer.ID,
                buyer.ProfileInfo?.Nickname ?? "",
                buyer.Email,
                buyerPhotos
            ),
            sellingUserPhotos: new InTradeUserPhotos(
                seller.ID,
                seller.ProfileInfo?.Nickname ?? "",
                seller.Email,
                sellerPhotos
            )
        );

        return Result<TradeDetailsResponse>.Success(dto, "Successfully retrieved.");
    }


    #region HELPERS
    private static string? TrimAuth0UserId(string? auth0UserId)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return null;

        return auth0UserId.StartsWith("auth0|", StringComparison.Ordinal)
            ? auth0UserId["auth0|".Length..]
            : auth0UserId;
    }
    private static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 10;
        if (pageSize > 100) pageSize = 100;
        return (page, pageSize);
    }
    private static PagedResponse<T> ToPaged<T>(int page, int pageSize, int totalCount, List<T> elements)
        => new()
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Elements = elements
        };

    #endregion

    #region VALIDATORS
   private static Result<PagedResponse<TradeListItemDTO>>? ValidateTradesQuery(
    TradesQuery? q,
    TradeStatuses scope)
{
    if (q is null)
        return null;

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

    if (q.MinTokenCost is not null && q.MinTokenCost < 0)
        return Result<PagedResponse<TradeListItemDTO>>.BadRequest("minTokenCost must be >= 0.");

    if (q.MaxTokenCost is not null && q.MaxTokenCost < 0)
        return Result<PagedResponse<TradeListItemDTO>>.BadRequest("maxTokenCost must be >= 0.");

    if (q.MinTokenCost is not null && q.MaxTokenCost is not null &&
        q.MinTokenCost.Value > q.MaxTokenCost.Value)
    {
        return Result<PagedResponse<TradeListItemDTO>>.BadRequest(
            "minTokenCost cannot be greater than maxTokenCost.");
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


    

    #endregion
}