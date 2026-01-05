using ItemTradeApp.Features.Offers.DTOs;
using ItemTradeApp.Features.Offers.DTOs.RequestDTOs;
using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Offers.Internal;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Offers;


public interface IOfferService
{
    Task<Result<PagedResponse<OfferListingDTO>>>
        GetOffersAsync(OfferListingsQuery query, CancellationToken ct = default);

    Task<Result<OfferResponse>> 
        CreateOfferAsync(string auth0UserId, CreateOfferRequest createOfferRequest,
        CancellationToken ct = default);

    Task<Result<bool>> CancelOfferAsync(string auth0UserId, int offerId, CancellationToken ct = default);

    Task<Result<OfferResponse>> UpdateOfferAsync(string auth0UserId, int offerId, UpdateOfferRequest request,
        CancellationToken ct = default);
}

public class OfferService(
    IOffersRepository offersRepository,
    IOfferUserRepository userRepository,
    IUnitOfWork unitOfWork) : IOfferService
{
    public async Task<Result<PagedResponse<OfferListingDTO>>> GetOffersAsync(OfferListingsQuery query,
        CancellationToken ct = default)
    {

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
            Items = items.ToList()
        };

        return Result<PagedResponse<OfferListingDTO>>.Success(response);
    }

    public async Task<Result<OfferResponse>> CreateOfferAsync(string auth0UserId,
        CreateOfferRequest createOfferRequest, CancellationToken ct = default)
    {

        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<OfferResponse>.Unauthorized("missing_sub_claim");

    
        var (okDraft, errDraft, draft) = await BuildDraftAsync(createOfferRequest.OfferedItems, createOfferRequest.WantedItems, createOfferRequest.ExpDate, ct);
        if (!okDraft)
            return Result<OfferResponse>.BadRequest(errDraft);
        if (draft is null) return Result<OfferResponse>.BadRequest("draft_creation_failed");
        
        var (okUser, errUser, userState) = await GetActiveUserOrErrorAsync(auth0UserId, ct);
        if (!okUser) return Result<OfferResponse>.Unauthorized(errUser!);
        if (userState is null) return Result<OfferResponse>.BadRequest("user_fetch_error");

        
        if (userState.Tokens < draft.TokenCost) return Result<OfferResponse>.BadRequest("not_enough_tokens");

        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var charged = await userRepository.TrySubtractTokenCostAsync(userState.Id, draft.TokenCost, ct);
            if (!charged)
            {
                await tx.RollbackAsync(ct);
                return Result<OfferResponse>.Conflict("concurrency_conflict");
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
            var response = MapResponse(offer, draft.Offered, draft.Wanted);
            return Result<OfferResponse>.Created(response);


        }
        catch
        {
            await tx.RollbackAsync(ct);
            return Result<OfferResponse>.InternalServerError("create_offer_failed");
        }
    }

    public async Task<Result<OfferResponse>> UpdateOfferAsync(string auth0UserId, int offerId,
        UpdateOfferRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<OfferResponse>.Unauthorized("missing_sub_claim");
        if (offerId <= 0) return Result<OfferResponse>.BadRequest("invalid_offer_id");
        
        var (okDraft, errDraft, draft) = await BuildDraftAsync(request.OfferedItems, request.WantedItems, request.ExpDate, ct);
        if (!okDraft)
            return Result<OfferResponse>.BadRequest(errDraft);
        if (draft is null) return Result<OfferResponse>.BadRequest("draft_creation_failed");

        var (okUser, errUser, userState) = await GetActiveUserOrErrorAsync(auth0UserId, ct);
        if (!okUser) return Result<OfferResponse>.Unauthorized(errUser!);
        
        var offer = await offersRepository.GetTrackedOfferAsync(offerId, userState!.Id,ct);
        if (offer is null) return Result<OfferResponse>.NotFound("offer_not_found");
        if (offer.OfferStatus_ID != (int)OfferStatuses.Active)
            return Result<OfferResponse>.BadRequest("offer_not_active");
        
        var updateFeeTokens = Math.Max(OfferConsts.MinBaseTokenCost, draft.TokenCost - offer.TokenCost);
        if (userState.Tokens < updateFeeTokens) return Result<OfferResponse>.BadRequest("not_enough_tokens");
        
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var charged = await userRepository.TrySubtractTokenCostAsync(userState.Id, updateFeeTokens, ct);
            if (!charged)
            {
                await tx.RollbackAsync(ct);
                return Result<OfferResponse>.Conflict("concurrency_conflict");
            }
            
            ApplyListingItemsUpdate(offer, draft.Offered, draft.Wanted);
    
            offer.ExpDate = draft.ExpDate;
            offer.TokenCost = draft.TokenCost;
            
            await unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var response = MapResponse(offer, draft.Offered, draft.Wanted);
            return Result<OfferResponse>.Success(response);

        }
        catch
        {
            await tx.RollbackAsync(ct);
            return Result<OfferResponse>.InternalServerError("update_offer_failed");
        }


    }

    public async Task<Result<bool>> CancelOfferAsync(string auth0UserId, int offerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            return Result<bool>.Unauthorized("missing_sub_claim");
        }

        if (offerId <= 0)
        {
            return Result<bool>.BadRequest("invalid_offer_id");
        }

        var userState = await userRepository.GetStateByAuth0IdAsync(auth0UserId, ct);
        if (userState is null) return Result<bool>.Unauthorized("user_not_found");
        if (userState.IsDeleted) return Result<bool>.Unauthorized("user_deleted");

        var updated = await offersRepository.CancelOfferAsync(userState.Id, offerId, ct);
        if (!updated)
        {
            return Result<bool>.BadRequest("cancel_offer_failed");
        }

        return Result<bool>.NoContent();
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

    private static OfferResponse MapResponse(Offer offer, Dictionary<int, DictItemQuantity> offered, Dictionary<int, DictItemQuantity> wanted)
    {
        var offerCore = new OfferCoreDTO(offer.ID, offer.ExpDate, offer.CreationDate, offer.TokenCost);
        return new OfferResponse(
            offerCore,
            offered.Select(x => new OfferItemDTO(x.Key, x.Value.Quantity)).ToList(),
            wanted.Select(x => new OfferItemDTO(x.Key, x.Value.Quantity)).ToList()
            )
        ;
    }

    private async Task<(bool Ok, string? err, OfferDraft? offerDraft)> BuildDraftAsync(IReadOnlyCollection<OfferItemDTO> offeredItems,
        IReadOnlyCollection<OfferItemDTO> wantedItems, DateOnly expDate, CancellationToken ct)
    {
        var offered = NormalizeItems(offeredItems);
        var wanted = NormalizeItems(wantedItems);

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

    private static Dictionary<int, int> NormalizeItems(IReadOnlyCollection<OfferItemDTO> items)
    {
        var result = new Dictionary<int, int>();
        foreach (var item in items)
        {
            
            //It will be fixed with fluentValidation
            if(item.ItemId <= 0) continue;
            if (item.Quantity <= 0) continue;

            if (result.TryGetValue(item.ItemId, out var q))
            {
                result[item.ItemId] = q + item.Quantity;
            }
            else
            {
                result[item.ItemId] =item.Quantity;
            }


        }

        return result;
    }

    private static int CalculateTokenCost(Dictionary<int,DictItemQuantity> offered, Dictionary<int,DictItemQuantity> wanted, int extraDayCost)
    {
        long totalValue = 0;

        checked
        {
            foreach (var items in offered.Values)
            {
                totalValue += (long)items.Item.EstimatedValue * items.Quantity;
            }

            foreach (var items in wanted.Values)
            {
                totalValue += (long)items.Item.EstimatedValue * items.Quantity;
            }
        }

        var baseTokenCost = (int)Math.Ceiling(totalValue * OfferConsts.BaseCostRate);
        if (baseTokenCost < OfferConsts.MinBaseTokenCost) baseTokenCost = OfferConsts.MinBaseTokenCost;

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

        var minExpDate = today.AddDays(OfferConsts.MinExpiryDays);
        if (requestExpDate < minExpDate)
        {
            return (false, "exp_date_min_7_days", default, 0);
        }

        var extraDays = requestExpDate.DayNumber - minExpDate.DayNumber;
        var extraDaysCost = extraDays > 0 ? extraDays * OfferConsts.ExtraDayCost : 0;

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