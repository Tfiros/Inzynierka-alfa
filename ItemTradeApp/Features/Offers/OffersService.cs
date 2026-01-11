using ItemTradeApp.Features.Offers.DTOs;
using ItemTradeApp.Features.Offers.DTOs.RequestDTOs;
using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Offers.Internal;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Offers;


public interface IOffersService
{
    Task<Result<PagedResponse<OfferListingDTO>>>
        GetOffersAsync(OfferListingsQuery query, CancellationToken ct = default);

    Task<Result<OfferDetailsDTO>> GetOfferByIdAsync(int id,
        CancellationToken ct = default);

    Task<Result<OfferDetailsDTO>> 
        CreateOfferAsync(string auth0UserId, OfferDraftRequest offerDraftRequest,
        CancellationToken ct = default);

    Task<Result<string>> CancelOfferAsync(string auth0UserId, int offerId, CancellationToken ct = default);

    Task<Result<OfferDetailsDTO>> UpdateOfferAsync(string auth0UserId, int offerId, OfferDraftRequest request,
        CancellationToken ct = default);
}

public class OffersService(
    IOffersRepository offersRepository,
    IUsersRepository userRepository,
    IUnitOfWork unitOfWork) : IOffersService
{
    public async Task<Result<PagedResponse<OfferListingDTO>>> GetOffersAsync(OfferListingsQuery query,
        CancellationToken ct = default)
    {
        if (query.Page <= 0) return Result<PagedResponse<OfferListingDTO>>.BadRequest("invalid_page_number");
        if (query.PageSize <= 0) return Result<PagedResponse<OfferListingDTO>>.BadRequest("invalid_page_size");
        query.PageSize = query.PageSize > 100 ? 100 : query.PageSize;

        if (query.GameId is not null && query.GameId <= 0)
        {
            return Result<PagedResponse<OfferListingDTO>>.BadRequest("incorrect_game_id");
        }
        if (query.GenreId is not null && query.GenreId <= 0)
        {
            return Result<PagedResponse<OfferListingDTO>>.BadRequest("incorrect_genre_id");
        }
        
        var (items, totalCount) = await offersRepository.GetOffersPagedAsync(query, ct);

        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var response = new PagedResponse<OfferListingDTO>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Elements = items.ToList()
        };

        return Result<PagedResponse<OfferListingDTO>>.Success(response);
    }
    public async Task<Result<OfferDetailsDTO>> GetOfferByIdAsync(int id,
        CancellationToken ct = default)
    {
        if (id <= 0) return Result<OfferDetailsDTO>.BadRequest("invalid_offer_id");
        var response = await offersRepository.GetOfferByIdAsync(id, ct);
        if (response is null) return Result<OfferDetailsDTO>.NotFound("offer_not_found");

        return Result<OfferDetailsDTO>.Success(response);
    }

    public async Task<Result<OfferDetailsDTO>> CreateOfferAsync(string auth0UserId,
        OfferDraftRequest offerDraftRequest, CancellationToken ct = default)
    {

        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<OfferDetailsDTO>.Unauthorized("missing_sub_claim");

    
        var (okDraft, errDraft, draft) = await BuildDraftAsync(offerDraftRequest.OfferedItems, offerDraftRequest.WantedItems, offerDraftRequest.ExpDate, ct);
        if (!okDraft)
            return Result<OfferDetailsDTO>.BadRequest(errDraft);
        if (draft is null) return Result<OfferDetailsDTO>.BadRequest("draft_creation_failed");
        
        var (okUser, errUser, userState) = await GetActiveUserOrErrorAsync(auth0UserId, ct);
        if (!okUser) return Result<OfferDetailsDTO>.Unauthorized(errUser!);
        if (userState is null) return Result<OfferDetailsDTO>.BadRequest("user_fetch_error");

        
        if (userState.Tokens < draft.TokenCost) return Result<OfferDetailsDTO>.BadRequest("not_enough_tokens");

        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var charged = await userRepository.TrySubtractTokenCostAsync(userState.Id, draft.TokenCost, ct);
            if (!charged)
            {
                await tx.RollbackAsync(ct);
                return Result<OfferDetailsDTO>.Conflict("concurrency_conflict");
            }

            var offer = new Offer
            {
                CreationDate = DateTime.UtcNow,
                ExpDate = draft.ExpDate,
                TokenCost = draft.TokenCost,
                User_ID = userState.Id,
                OfferStatus_ID = (int)OfferStatuses.Active,
                ListingItems = draft.Offered.Select(kv => new ListingItems
                {
                    Item_ID = kv.Key,
                    Quantity = kv.Value.Quantity,
                    IsWanted = false
                }).Concat(draft.Wanted.Select(kv => new ListingItems
                {
                    Item_ID = kv.Key,
                    Quantity = kv.Value.Quantity,
                    IsWanted = true
                })).ToList()
            };
            offersRepository.Add(offer);

            await unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var response = await offersRepository.GetOfferByIdAsync(offer.ID, ct);
            if (response is null) return Result<OfferDetailsDTO>.InternalServerError("create_offer_failed");
            return Result<OfferDetailsDTO>.Created(response);


        }
        catch
        {
            await tx.RollbackAsync(ct);
            return Result<OfferDetailsDTO>.InternalServerError("create_offer_failed");
        }
    }

    public async Task<Result<OfferDetailsDTO>> UpdateOfferAsync(string auth0UserId, int offerId,
        OfferDraftRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<OfferDetailsDTO>.Unauthorized("missing_sub_claim");
        if (offerId <= 0) return Result<OfferDetailsDTO>.BadRequest("invalid_offer_id");
        
        var (okDraft, errDraft, draft) = await BuildDraftAsync(request.OfferedItems, request.WantedItems, request.ExpDate, ct);
        if (!okDraft)
            return Result<OfferDetailsDTO>.BadRequest(errDraft);
        if (draft is null) return Result<OfferDetailsDTO>.BadRequest("draft_creation_failed");

        var (okUser, errUser, userState) = await GetActiveUserOrErrorAsync(auth0UserId, ct);
        if (!okUser) return Result<OfferDetailsDTO>.Unauthorized(errUser!);
        
        var offer = await offersRepository.GetTrackedOfferAsync(offerId, userState!.Id,ct);
        if (offer is null) return Result<OfferDetailsDTO>.NotFound("offer_not_found");
        if (offer.OfferStatus_ID != (int)OfferStatuses.Active)
            return Result<OfferDetailsDTO>.BadRequest("offer_not_active");
        
        var updateFeeTokens = Math.Max(OffersConsts.MinBaseTokenCost, draft.TokenCost - offer.TokenCost);
        if (userState.Tokens < updateFeeTokens) return Result<OfferDetailsDTO>.BadRequest("not_enough_tokens");
        
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var charged = await userRepository.TrySubtractTokenCostAsync(userState.Id, updateFeeTokens, ct);
            if (!charged)
            {
                await tx.RollbackAsync(ct);
                return Result<OfferDetailsDTO>.Conflict("concurrency_conflict");
            }
            
            ApplyListingItemsUpdate(offer, draft.Offered, draft.Wanted);
    
            offer.ExpDate = draft.ExpDate;
            offer.TokenCost = draft.TokenCost;
            
            await unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var response = await offersRepository.GetOfferByIdAsync(offer.ID, ct);
            if (response is null) return Result<OfferDetailsDTO>.InternalServerError("update_offer_failed");
            return Result<OfferDetailsDTO>.Success(response);

        }
        catch
        {
            await tx.RollbackAsync(ct);
            return Result<OfferDetailsDTO>.InternalServerError("update_offer_failed");
        }


    }

    public async Task<Result<string>> CancelOfferAsync(string auth0UserId, int offerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            return Result<string>.Unauthorized("missing_sub_claim");
        }

        if (offerId <= 0)
        {
            return Result<string>.BadRequest("invalid_offer_id");
        }

        var userState = await userRepository.GetStateByAuth0IdAsync(auth0UserId, ct);
        if (userState is null) return Result<string>.Unauthorized("user_not_found");
        if (userState.IsDeleted) return Result<string>.Unauthorized("user_deleted");

        var updated = await offersRepository.CancelOfferAsync(userState.Id, offerId, ct);
        if (!updated)
        {
            return Result<string>.BadRequest("cancel_offer_failed");
        }

        return Result<string>.Success("offer_cancelled");
    }

    #region OfferServiceHelpers
    
    private void ApplyListingItemsUpdate(Offer offer, Dictionary<int, DictItemQuantity> offered, Dictionary<int, DictItemQuantity> wanted)
    {
        var target = new Dictionary<(int ItemId, bool IsWanted), int>(offered.Count + wanted.Count);
        foreach (var kv in offered)
        {
            target[(kv.Key, false)] = kv.Value.Quantity;
        }
        foreach (var kv in wanted)
        {
            target[(kv.Key, true)] = kv.Value.Quantity;
        }

        var current = offer.ListingItems.ToDictionary(li => (li.Item_ID, li.IsWanted), li => li);

        var toRemove = offer.ListingItems.Where(li => !target.ContainsKey((li.Item_ID, li.IsWanted))).ToList();
        var toAdd = target.Where(kv => !current.ContainsKey(kv.Key)).Select(kv=>new ListingItems
        {
            Offer_ID = offer.ID,
            Item_ID = kv.Key.ItemId,
            IsWanted = kv.Key.IsWanted,
            Quantity = kv.Value
        }).ToList();
            
        if(toRemove.Count>0) offersRepository.RemoveListingItemsRange(toRemove);
        foreach (var (key,qty) in target)
        {
            if (current.TryGetValue(key, out var li))
            {
                li.Quantity = qty;
            }
        }
        if(toAdd.Count>0) offersRepository.AddListingItemsRange(toAdd);

    }
    private async Task<(bool Ok, string? err, OfferDraft? offerDraft)> BuildDraftAsync(IReadOnlyCollection<OfferItemDTO> offeredItems,
        IReadOnlyCollection<OfferItemDTO> wantedItems, DateOnly expDate, CancellationToken ct)
    {
        var offered = offeredItems.ToDictionary(x => x.ItemId, x => x.Quantity); 
        var wanted = wantedItems.ToDictionary(x => x.ItemId, x => x.Quantity);

        if (offered.Count == 0)
            return (false, "offered_items_required", null);
        if (wanted.Count == 0)
            return (false, "wanted_items_required", null);
        if (expDate == default)
            return (false, "exp_date_required", null);


        var (expOk, err, expCalcDate, extraDayCost) = ResolveExpiry(expDate);
        if (!expOk) return (false, err, null);

        var (okItems, errItems, items) = await LoadItemsOrErrorAsync(offered, wanted, ct);
        if (!okItems) return (false, errItems, null);

        var offeredLines = offered.ToDictionary(kv => kv.Key, kv => new DictItemQuantity(items[kv.Key], kv.Value));
        var wantedLines = wanted.ToDictionary(kv => kv.Key, kv => new DictItemQuantity(items[kv.Key], kv.Value));

        int tokenCost;
        try
        {
            tokenCost = CalculateTokenCost(offeredLines, wantedLines, extraDayCost);
        }
        catch (OverflowException)
        {
            return (false, "token_cost_overflow", null);
        }

        var draft = new OfferDraft(offeredLines, wantedLines, expCalcDate, tokenCost);

        return (true, null, draft);

    }

    private static int CalculateTokenCost(Dictionary<int,DictItemQuantity> offered, Dictionary<int,DictItemQuantity> wanted, int extraDayCost)
    {
        long totalValue = 0;

        checked
        {
            foreach (var items in offered.Values)
            {
                totalValue += (long)items.Item.EstimatedTokenValue * items.Quantity;
            }

            foreach (var items in wanted.Values)
            {
                totalValue += (long)items.Item.EstimatedTokenValue * items.Quantity;
            }
        }

        var baseTokenCost = (int)Math.Ceiling(totalValue * OffersConsts.BaseCostRate);
        if (baseTokenCost < OffersConsts.MinBaseTokenCost) baseTokenCost = OffersConsts.MinBaseTokenCost;

        int tokenCost;
        checked
        {
            tokenCost = baseTokenCost + extraDayCost;
        }

        return tokenCost;

    }

    private static (bool Ok, string? Error, DateOnly ExpDate, int ExtraDayCost) ResolveExpiry(DateOnly requestExpDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var minExpDate = today.AddDays(OffersConsts.MinExpiryDays);
        if (requestExpDate < minExpDate)
        {
            return (false, "exp_date_min_7_days", default, 0);
        }

        var extraDays = requestExpDate.DayNumber - minExpDate.DayNumber;
        var extraDaysCost = extraDays > 0 ? extraDays * OffersConsts.ExtraDayCost : 0;

        return (true, null, requestExpDate, extraDaysCost);



    }

    private async Task<(bool Ok, string? Error, Dictionary<int, Item> Items)> LoadItemsOrErrorAsync(
        Dictionary<int, int> offered, Dictionary<int, int> wanted, CancellationToken ct)
    {
        var allIds = offered.Keys.Concat(wanted.Keys).Distinct().ToArray();
        var items = await offersRepository.GetItemsByIdsAsync(allIds, ct);
        
        var missing = allIds.Where(id => !items.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
            return (false, $"items_not_found: {string.Join(",", missing)}", new());
        return (true, null, items);
    }

    private async Task<(bool ok, string? error, UserState? userState)> GetActiveUserOrErrorAsync(string auth0UserId, CancellationToken ct)
    {
        var userState = await userRepository.GetStateByAuth0IdAsync(auth0UserId, ct);
        if (userState is null) return (false, "user_not_found", null);
        if (userState.IsDeleted) return (false, "user_deleted", null);
        return (true, null, userState);
    }
    

    #endregion
    

}