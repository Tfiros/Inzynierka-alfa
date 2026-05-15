using ItemTradeApp.Features.Shared.TokenEscrow;
﻿using ItemTradeApp.Features.Shared.Chat;
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
    Task<Result<string>> AssignMiddlemanAsync(AssignMiddlemanRequest? request, string? auth0UserId, CancellationToken ct);

    Task<Result<string>> UpdateTradeByMiddlemanAsync(int tradeId, UpdateTradeRequest? request, string? auth0UserId, CancellationToken ct);

    Task<Result<string>> SetTradeAsFailedAsync(int tradeId, string? auth0UserId, CancellationToken ct);

    Task<Result<string>> SetTradeAsRealisedAsync(int tradeId, string? auth0UserId, CompleteAndMarkTradeRequest request, CancellationToken ct);

    Task<Result<UserTradeStatsResponse>> GetStatsAsync(string? auth0UserId, bool isMiddleman, CancellationToken ct);

    Task<Result<PagedResponse<TradeListItemDTO>>> GetAvailableNewAsync(int page, int pageSize, TradesQuery? query, string? auth0UserId, CancellationToken ct);

    Task<Result<PagedResponse<TradeListItemDTO>>> GetMyInRealizationAsync(int page, int pageSize, TradesQuery? query, string? auth0UserId, CancellationToken ct);

    Task<Result<PagedResponse<TradeListItemDTO>>> GetMyCompletedAsync(int page, int pageSize, TradesQuery? query, string? auth0UserId, CancellationToken ct);

    Task<Result<PagedResponse<TradeListItemDTO>>> GetMyFailedWithItemsToReturnAsync(int page, int pageSize, TradesQuery? query, string? auth0UserId, CancellationToken ct);

    Task<Result<TradeDetailsResponse>> GetTradeDetailsAsync(int tradeId, string? auth0UserId, CancellationToken ct);

    Task<Result<TradeListItemDTO>> GetByIdAsync(int tradeId, string? auth0UserId, bool isMiddlemanView,
        CancellationToken ct);
}

public sealed class TradesService(
    ITradeRepository tradeRepo,
    IUserContext userContext,
    ITradesRequestValidator validator,
    ITradeListQueryService listQuery,
    IUnitOfWork unitOfWork,
    IUserRepository userRepo,
    ITokenEscrow tokenEscrow,
    IChatOperations chatOperations
) : ITradesService
{
    public async Task<Result<UserTradeStatsResponse>> GetStatsAsync(string? auth0UserId, bool isMiddleman, CancellationToken ct)
    {
        if (isMiddleman)
        {
            var user = await TryGetMiddleman(auth0UserId, ct);
            if (user.Error is not null)
                return Result<UserTradeStatsResponse>.Unauthorized(user.Error);

            var middleman = user.User!;
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
            var getUser = await TryGetUser(auth0UserId, ct);
            if (getUser.Error is not null)
                return Result<UserTradeStatsResponse>.Unauthorized(getUser.Error);

            var user = getUser.User!;
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
        var tryGetMiddleman = await TryGetMiddleman(auth0UserId, ct);
        if (tryGetMiddleman.Error is not null)
            return Result<UserTradeStatsResponse>.Unauthorized(tryGetMiddleman.Error);

        var middleman = tryGetMiddleman.User!;

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

        var tryGetUser = await TryGetUser(auth0UserId, ct);
        if (tryGetUser.Error is not null)
            return Result<PagedResponse<TradeListItemDTO>>.Unauthorized(tryGetUser.Error);

        var user = tryGetUser.User!;

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

        var tryGetMiddleman = await TryGetMiddleman(auth0UserId, ct);
        if (tryGetMiddleman.Error is not null)
            return Result<PagedResponse<TradeListItemDTO>>.Unauthorized(tryGetMiddleman.Error);

        var middleman = tryGetMiddleman.User!;

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

        var tryGetMiddleman = await TryGetMiddleman(auth0UserId, ct);
        if (tryGetMiddleman.Error is not null)
            return Result<PagedResponse<TradeListItemDTO>>.Unauthorized(tryGetMiddleman.Error);

        var middleman = tryGetMiddleman.User!;

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

        var tryGetMiddleman = await TryGetMiddleman(auth0UserId, ct);
        if (tryGetMiddleman.Error is not null)
            return Result<PagedResponse<TradeListItemDTO>>.Unauthorized(tryGetMiddleman.Error);

        var middleman = tryGetMiddleman.User!;

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

        var tryGetMiddleman = await TryGetMiddleman(auth0UserId, ct);
        if (tryGetMiddleman.Error is not null)
            return Result<TradeDetailsResponse>.Unauthorized(tryGetMiddleman.Error);

        var middleman = tryGetMiddleman.User!;

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

    public async Task<Result<string>> AssignMiddlemanAsync(AssignMiddlemanRequest? request, string? auth0UserId, CancellationToken ct)
    {
        if (request is null)
            return Result<string>.BadRequest("Body is required.");

        if (request.TradeId <= 0)
            return Result<string>.BadRequest("TradeId must be > 0.");

        var tryGetMiddleman = await TryGetMiddleman(auth0UserId, ct);
        if (tryGetMiddleman.Error is not null)
            return Result<string>.Unauthorized(tryGetMiddleman.Error);

        var middleman = tryGetMiddleman.User!;

        var trade = await tradeRepo.GetByIdAsync(request.TradeId, ct);
        if (trade is null)
            return Result<string>.NotFound("Trade not found.");

        if (trade.TradeStatus_ID != (int)TradeStatuses.New)
            return Result<string>.BadRequest("Trade is not in NEW status.");

        if (trade.MiddlemanUser_ID is not null)
            return Result<string>.Conflict("Trade already has a middleman assigned.");

        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            trade.MiddlemanUser_ID = middleman.ID;
            trade.TradeStatus_ID = (int)TradeStatuses.InRealization;

            await chatOperations.CreateChatsForTradeAsync(
                new CreateChatsForTradeContext(trade.ID, trade.Customer_ID, trade.User_ID, middleman.ID), ct);
            await unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            return Result<string>.InternalServerError("Assign middleman failed");
        }

        await chatOperations.PublishChatsCreatedAsync(trade.ID, ct);
        return Result<string>.Success("Middleman assigned");
    }

    public async Task<Result<string>> UpdateTradeByMiddlemanAsync(int tradeId, UpdateTradeRequest? request, string? auth0UserId, CancellationToken ct)
    {
        if (tradeId <= 0)
            return Result<string>.BadRequest("TradeId must be > 0.");

        if (request is null)
            return Result<string>.BadRequest("Body is required.");

        var tryGetMiddleman = await TryGetMiddleman(auth0UserId, ct);
        if (tryGetMiddleman.Error is not null)
            return Result<string>.Unauthorized(tryGetMiddleman.Error);

        var middleman = tryGetMiddleman.User!;

        var trade = await tradeRepo.GetByIdWithUrlsAsync(tradeId, ct);
        if (trade is null)
            return Result<string>.NotFound("Trade not found.");

        if (trade.MiddlemanUser_ID is null)
            return Result<string>.BadRequest("Trade has no middleman assigned.");

        if (trade.MiddlemanUser_ID != middleman.ID)
            return Result<string>.Forbidden("You are not assigned to this trade.");

        if (trade.TradeStatus_ID != (int)TradeStatuses.InRealization && trade.TradeStatus_ID != (int)TradeStatuses.Failed)
            return Result<string>.BadRequest("Trade is not in InRealization or Failed status.");

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

        var tryGetMiddleman = await TryGetMiddleman(auth0UserId, ct);
        if (tryGetMiddleman.Error is not null)
            return Result<string>.Unauthorized(tryGetMiddleman.Error);

        var middleman = tryGetMiddleman.User!;

        var trade = await tradeRepo.GetTradeWithOfferByIdAsync(tradeId, ct);
        if (trade is null)
            return Result<string>.NotFound("Trade not found.");

        if (trade.MiddlemanUser_ID is null)
            return Result<string>.BadRequest("Trade has no middleman assigned.");

        if (trade.MiddlemanUser_ID != middleman.ID)
            return Result<string>.Forbidden("You are not assigned to this trade.");
        
        if (trade.TradeStatus_ID != (int)TradeStatuses.InRealization)
            return Result<string>.BadRequest("Trade is not in InRealization status.");

        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            if (trade.Offer.TokensOffered > 0)
            {
                if (!await tokenEscrow.TryRefundEscrowToOtherAsync(
                        trade.Customer_ID,
                        trade.User_ID,
                        trade.Offer.TokensOffered,
                        ct))
                {
                    await tx.RollbackAsync(ct);
                    return Result<string>.BadRequest("Failed to refund seller's offered tokens.");
                }
            }

            if (trade.Offer.TokensWanted > 0)
            {
                if (!await tokenEscrow.TryRefundEscrowToOtherAsync(
                        trade.User_ID,
                        trade.Customer_ID,
                        trade.Offer.TokensWanted,
                        ct))
                {
                    await tx.RollbackAsync(ct);
                    return Result<string>.BadRequest("Failed to refund buyer's wanted tokens.");
                }
            }
            
            trade.TradeStatus_ID = (int)TradeStatuses.Failed;
            trade.Offer.OfferStatus_ID = (int)OfferStatuses.Active;
            trade.Offer.ExpDate = DateOnly.FromDateTime(DateTime.Now.AddDays(7));
            await chatOperations.CloseChatsForTradeAsync(trade.ID, ct);

            await unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            return Result<string>.BadRequest("There was an error when trying to refund tokens");
        }

        await chatOperations.PublishChatsClosedAsync(trade.ID, ct);
        return Result<string>.Success("Successfully set as failed.");

    }

    public async Task<Result<string>> SetTradeAsRealisedAsync(int tradeId,
        string? auth0UserId,
        CompleteAndMarkTradeRequest request,
        CancellationToken ct)
    {
        if (tradeId <= 0)
            return Result<string>.BadRequest("TradeId must be > 0.");

        var tryGetMiddleman = await TryGetMiddleman(auth0UserId, ct);
        if (tryGetMiddleman.Error is not null)
            return Result<string>.Unauthorized(tryGetMiddleman.Error);

        var middleman = tryGetMiddleman.User!;

        var trade = await tradeRepo.GetTradeWithOfferByIdAsync(tradeId, ct);
        if (trade is null)
            return Result<string>.NotFound("Trade not found.");

        if (trade.MiddlemanUser_ID is null)
            return Result<string>.BadRequest("Trade has no middleman assigned.");

        if (trade.MiddlemanUser_ID != middleman.ID)
            return Result<string>.Forbidden("You are not assigned to this trade.");

        if (!trade.HasBuyersItems || !trade.HasSellersItems)
            return Result<string>.Forbidden("Cannot set trade as realised as users items are still in your possession.");
        
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            trade.TradeStatus_ID = (int)TradeStatuses.SuccesfulRealization;
            trade.Offer.OfferStatus_ID = (int)OfferStatuses.Completed;

            var buyersRate = new Rate
            {
                TradeId = trade.ID,
                UserId = trade.Customer_ID,
                Mark = request.BuyersGrade,
                Description = request.BuyersDescription
            };

            var sellersRate = new Rate
            {
                TradeId = trade.ID,
                UserId = trade.User_ID,
                Mark = request.SellersGrade,
                Description = request.SellersDescription
            };
        
            trade.Rates.Add(buyersRate);
            trade.Rates.Add(sellersRate);

            
            if (trade.Offer.TokensOffered > 0)
            {
                if (!await tokenEscrow.TryReleaseOwnEscrowAsync(
                        trade.Customer_ID,
                        trade.Offer.TokensOffered,
                        ct))
                {
                    await tx.RollbackAsync(ct);
                    return Result<string>.BadRequest("Failed to release offered tokens.");
                }
            }

            if (trade.Offer.TokensWanted > 0)
            {
                if (!await tokenEscrow.TryReleaseOwnEscrowAsync(
                        trade.User_ID,
                        trade.Offer.TokensWanted,
                        ct))
                {
                    await tx.RollbackAsync(ct);
                    return Result<string>.BadRequest("Failed to release wanted tokens.");
                }
            }

            await chatOperations.CloseChatsForTradeAsync(trade.ID, ct);
            await unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            return Result<string>.BadRequest("There was an error when trying to transfer tokens");
        }

        await chatOperations.PublishChatsClosedAsync(trade.ID, ct);
        return Result<string>.Success("Successfully set as realised");
    }

    public async Task<Result<TradeListItemDTO>> GetByIdAsync(int tradeId, string? auth0UserId, bool isMiddlemanView,
        CancellationToken ct)
    {
        if (tradeId <= 0)
            return Result<TradeListItemDTO>.BadRequest("trade id must be greater than 0");

        var user = await TryGetUser(auth0UserId, ct);
        if (user.Error is not null)
        {
            return Result<TradeListItemDTO>.Unauthorized(user.Error);
            
        }

        var trade = await listQuery.GetTradeByIdAsync(tradeId, user.User!.ID, isMiddlemanView, ct);

        return trade is null
            ? Result<TradeListItemDTO>.NotFound("trade not found")
            : Result<TradeListItemDTO>.Success(trade, "success");

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
