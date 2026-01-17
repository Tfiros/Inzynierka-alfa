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
    Task<Result<int>> CreateAsync(CreateTradeRequest? request, string? auth0UserId, CancellationToken ct);

    Task<Result<string>> AssignMiddlemanAsync(AssignMiddlemanRequest? request, string? auth0UserId, CancellationToken ct);

    Task<Result<string>> UpdateTradeByMiddlemanAsync(int tradeId, UpdateTradeRequest? request, string? auth0UserId, CancellationToken ct);

    Task<Result<string>> SetTradeAsFailedAsync(int tradeId, string? auth0UserId, CancellationToken ct);

    Task<Result<string>> SetTradeAsRealisedAsync(int tradeId, string? auth0UserId, CancellationToken ct);

    Task<Result<UserTradeStatsResponse>> GetStatsAsync(string? auth0UserId, bool isMiddleman, CancellationToken ct);

    Task<Result<PagedResponse<TradeListItemDTO>>> GetAvailableNewAsync(int page, int pageSize, TradesQuery? query, string? auth0UserId, CancellationToken ct);

    Task<Result<PagedResponse<TradeListItemDTO>>> GetMyInRealizationAsync(int page, int pageSize, TradesQuery? query, string? auth0UserId, CancellationToken ct);

    Task<Result<PagedResponse<TradeListItemDTO>>> GetMyCompletedAsync(int page, int pageSize, TradesQuery? query, string? auth0UserId, CancellationToken ct);

    Task<Result<PagedResponse<TradeListItemDTO>>> GetMyFailedWithItemsToReturnAsync(int page, int pageSize, TradesQuery? query, string? auth0UserId, CancellationToken ct);

    Task<Result<TradeDetailsResponse>> GetTradeDetailsAsync(int tradeId, string? auth0UserId, CancellationToken ct);
}

public sealed class TradesService(
    ITradeRepository tradeRepo,
    IOfferRepository offerRepo,
    ICounterOfferRepository counterOfferRepo,
    IUserRepository userRepo,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ITradesRequestValidator validator,
    ITradeListQueryService listQuery
) : ITradesService
{
    public async Task<Result<UserTradeStatsResponse>> GetStatsAsync(string? auth0UserId, bool isMiddleman, CancellationToken ct)
    {
        if (isMiddleman)
        {
            var ures = await TryGetMiddleman(auth0UserId, ct);
            if (ures.Error is not null)
                return Result<UserTradeStatsResponse>.Unauthorized(ures.Error);

            var middleman = ures.User!;
            var (all, completed, myActive, available) =
                await tradeRepo.GetMiddlemanStatsAsync(middleman.ID, ct);

            var dto = new UserTradeStatsResponse(
                All: all,
                Completed: completed,
                MyActive: myActive,
                Created: available
            );

            return Result<UserTradeStatsResponse>.Success(dto, "Successfully retrieved.");
        }
        else
        {
            var ures = await TryGetUser(auth0UserId, ct);
            if (ures.Error is not null)
                return Result<UserTradeStatsResponse>.Unauthorized(ures.Error);

            var user = ures.User!;
            var (all, completed, myActive, created) =
                await tradeRepo.GetUserStatsAsync(user.ID, ct);

            var dto = new UserTradeStatsResponse(
                All: all,
                Completed: completed,
                MyActive: myActive,
                Created: created
            );

            return Result<UserTradeStatsResponse>.Success(dto, "Successfully retrieved.");
        }
    }


    public async Task<Result<UserTradeStatsResponse>> GetMiddlemanStatsAsync(string? auth0UserId, CancellationToken ct)
    {
        var ures = await TryGetMiddleman(auth0UserId, ct);
        if (ures.Error is not null)
            return Result<UserTradeStatsResponse>.Unauthorized(ures.Error);

        var middleman = ures.User!;

        var (all, completed, myActive, available) =
            await tradeRepo.GetMiddlemanStatsAsync(middleman.ID, ct);

        return Result<UserTradeStatsResponse>.Success(new UserTradeStatsResponse(
            All: all,
            Completed: completed,
            MyActive: myActive,
            Created: available
        ));
    }

    public async Task<Result<PagedResponse<TradeListItemDTO>>> GetAvailableNewAsync(
        int page,
        int pageSize,
        TradesQuery? query,
        string? auth0UserId,
        CancellationToken ct)
    {
        var (p, ps) = validator.Normalize(page, pageSize);

        var ures = await TryGetUser(auth0UserId, ct);
        if (ures.Error is not null)
            return Result<PagedResponse<TradeListItemDTO>>.Unauthorized(ures.Error);

        var user = ures.User!;

        var invalid = validator.ValidateTradesQuery(query, TradeStatuses.New);
        if (invalid is not null) return invalid;
        const bool isMiddlemanView = true;

        var (items, total) = await listQuery.GetTradesAsync(
            status: TradeStatuses.New,
            page: p,
            pageSize: ps,
            callerUserId: user.ID,
            q: query ?? new TradesQuery(),
            isMiddlemanView: isMiddlemanView,
            onlyWithItemsToReturn: false,
            ct: ct);

        return Result<PagedResponse<TradeListItemDTO>>.Success(ToPaged(p, ps, total, items));
    }

    public async Task<Result<PagedResponse<TradeListItemDTO>>> GetMyInRealizationAsync(
        int page,
        int pageSize,
        TradesQuery? query,
        string? auth0UserId,
        CancellationToken ct)
    {
        var (p, ps) = validator.Normalize(page, pageSize);

        var ures = await TryGetMiddleman(auth0UserId, ct);
        if (ures.Error is not null)
            return Result<PagedResponse<TradeListItemDTO>>.Unauthorized(ures.Error);

        var middleman = ures.User!;

        var invalid = validator.ValidateTradesQuery(query, TradeStatuses.InRealization);
        if (invalid is not null) return invalid;

        var (items, total) = await listQuery.GetTradesAsync(
            status: TradeStatuses.InRealization,
            page: p,
            pageSize: ps,
            callerUserId: middleman.ID,
            q: query ?? new TradesQuery(),
            isMiddlemanView: true,
            onlyWithItemsToReturn: false,
            ct: ct);

        return Result<PagedResponse<TradeListItemDTO>>.Success(ToPaged(p, ps, total, items));
    }

    public async Task<Result<PagedResponse<TradeListItemDTO>>> GetMyCompletedAsync(
        int page,
        int pageSize,
        TradesQuery? query,
        string? auth0UserId,
        CancellationToken ct)
    {
        var (p, ps) = validator.Normalize(page, pageSize);

        var ures = await TryGetMiddleman(auth0UserId, ct);
        if (ures.Error is not null)
            return Result<PagedResponse<TradeListItemDTO>>.Unauthorized(ures.Error);

        var middleman = ures.User!;

        var invalid = validator.ValidateTradesQuery(query, TradeStatuses.SuccesfulRealization);
        if (invalid is not null) return invalid;

        var (items, total) = await listQuery.GetTradesAsync(
            status: TradeStatuses.SuccesfulRealization,
            page: p,
            pageSize: ps,
            callerUserId: middleman.ID,
            q: query ?? new TradesQuery(),
            isMiddlemanView: true,
            onlyWithItemsToReturn: false,
            ct: ct);

        return Result<PagedResponse<TradeListItemDTO>>.Success(ToPaged(p, ps, total, items));
    }

    public async Task<Result<PagedResponse<TradeListItemDTO>>> GetMyFailedWithItemsToReturnAsync(
        int page,
        int pageSize,
        TradesQuery? query,
        string? auth0UserId,
        CancellationToken ct)
    {
        var (p, ps) = validator.Normalize(page, pageSize);

        var ures = await TryGetMiddleman(auth0UserId, ct);
        if (ures.Error is not null)
            return Result<PagedResponse<TradeListItemDTO>>.Unauthorized(ures.Error);

        var middleman = ures.User!;

        var invalid = validator.ValidateTradesQuery(query, TradeStatuses.Failed);
        if (invalid is not null) return invalid;

        var (items, total) = await listQuery.GetTradesAsync(
            status: TradeStatuses.Failed,
            page: p,
            pageSize: ps,
            callerUserId: middleman.ID,
            q: query ?? new TradesQuery(),
            isMiddlemanView: true,
            onlyWithItemsToReturn: true,
            ct: ct);

        return Result<PagedResponse<TradeListItemDTO>>.Success(ToPaged(p, ps, total, items));
    }

    public async Task<Result<TradeDetailsResponse>> GetTradeDetailsAsync(
        int tradeId,
        string? auth0UserId,
        CancellationToken ct)
    {
        if (tradeId <= 0)
            return Result<TradeDetailsResponse>.BadRequest("tradeId must be > 0.");

        var ures = await TryGetMiddleman(auth0UserId, ct);
        if (ures.Error is not null)
            return Result<TradeDetailsResponse>.Unauthorized(ures.Error);

        var middleman = ures.User!;

        var trade = await tradeRepo.GetTradeDetailsAsync(tradeId, ct);
        if (trade is null)
            return Result<TradeDetailsResponse>.NotFound("Trade not found.");

        if (trade.MiddlemanUser_ID != middleman.ID)
            return Result<TradeDetailsResponse>.Forbidden("You are not assigned to this trade.");

        var buyer = trade.Customer;
        var seller = trade.PostingUser;

        var buyerPhotos = trade.Urls.Where(u => u.IsBuyers).Select(u => u.PhotoUrl).ToList();
        var sellerPhotos = trade.Urls.Where(u => !u.IsBuyers).Select(u => u.PhotoUrl).ToList();

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
    

    public async Task<Result<int>> CreateAsync(CreateTradeRequest? request, string? auth0UserId, CancellationToken ct)
    {
        if (request is null)
            return Result<int>.BadRequest("Body is required.");

        var ures = await TryGetUser(auth0UserId, ct);
        if (ures.Error is not null)
            return Result<int>.Unauthorized(ures.Error);

        var caller = ures.User!;

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

    public async Task<Result<string>> AssignMiddlemanAsync(AssignMiddlemanRequest? request, string? auth0UserId, CancellationToken ct)
    {
        if (request is null)
            return Result<string>.BadRequest("Body is required.");

        if (request.TradeId <= 0)
            return Result<string>.BadRequest("TradeId must be > 0.");

        var ures = await TryGetMiddleman(auth0UserId, ct);
        if (ures.Error is not null)
            return Result<string>.Unauthorized(ures.Error);

        var middleman = ures.User!;

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

    public async Task<Result<string>> UpdateTradeByMiddlemanAsync(int tradeId, UpdateTradeRequest? request, string? auth0UserId, CancellationToken ct)
    {
        if (tradeId <= 0)
            return Result<string>.BadRequest("TradeId must be > 0.");

        if (request is null)
            return Result<string>.BadRequest("Body is required.");

        var ures = await TryGetMiddleman(auth0UserId, ct);
        if (ures.Error is not null)
            return Result<string>.Unauthorized(ures.Error);

        var middleman = ures.User!;

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

    public async Task<Result<string>> SetTradeAsFailedAsync(int tradeId, string? auth0UserId, CancellationToken ct)
    {
        if (tradeId <= 0)
            return Result<string>.BadRequest("TradeId must be > 0.");

        var ures = await TryGetMiddleman(auth0UserId, ct);
        if (ures.Error is not null)
            return Result<string>.Unauthorized(ures.Error);

        var middleman = ures.User!;

        var trade = await tradeRepo.GetTradeWithOfferByIdAsync(tradeId, ct);
        if (trade is null)
            return Result<string>.NotFound("Trade not found.");

        if (trade.MiddlemanUser_ID is null)
            return Result<string>.BadRequest("Trade has no middleman assigned.");

        if (trade.MiddlemanUser_ID != middleman.ID)
            return Result<string>.Forbidden("You are not assigned to this trade.");

        trade.TradeStatus_ID = (int)TradeStatuses.Failed;
        trade.Offer.OfferStatus_ID = (int)OfferStatuses.Active;
        trade.Offer.ExpDate = DateOnly.FromDateTime(DateTime.Now.AddDays(7));

        await tradeRepo.SaveChangesAsync(ct);
        return Result<string>.Success("Successfully set as failed.");
    }

    public async Task<Result<string>> SetTradeAsRealisedAsync(int tradeId, string? auth0UserId, CancellationToken ct)
    {
        if (tradeId <= 0)
            return Result<string>.BadRequest("TradeId must be > 0.");

        var ures = await TryGetMiddleman(auth0UserId, ct);
        if (ures.Error is not null)
            return Result<string>.Unauthorized(ures.Error);

        var middleman = ures.User!;

        var trade = await tradeRepo.GetByIdAsync(tradeId, ct);
        if (trade is null)
            return Result<string>.NotFound("Trade not found.");

        if (trade.MiddlemanUser_ID is null)
            return Result<string>.BadRequest("Trade has no middleman assigned.");

        if (trade.MiddlemanUser_ID != middleman.ID)
            return Result<string>.Forbidden("You are not assigned to this trade.");

        if (!trade.HasBuyersItems || !trade.HasSellersItems)
            return Result<string>.Forbidden("Cannot set trade as realised as users items are still in your possession.");

        trade.TradeStatus_ID = (int)TradeStatuses.SuccesfulRealization;

        await tradeRepo.SaveChangesAsync(ct);
        return Result<string>.Success("Successfully set as realised.");
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

    private async Task<(User? User, string? Error)> TryGetUser(string? auth0UserId, CancellationToken ct)
    {
        try
        {
            var user = await userContext.GetRequiredUserAsync(auth0UserId, ct);
            return user is null ? (null, "User not found") : (user, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private async Task<(User? User, string? Error)> TryGetMiddleman(string? auth0UserId, CancellationToken ct)
    {
        try
        {
            var user = await userContext.GetRequiredMiddlemanAsync(auth0UserId, ct);
            return user is null ? (null, "User not found") : (user, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }
}
