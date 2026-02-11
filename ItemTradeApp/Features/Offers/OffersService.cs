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

    Task<Result<OfferDetailsDTO>> UpdateOfferAsync(string auth0UserId, int offerId, OfferUpdateDraftRequest request,
        CancellationToken ct = default);

    Task<Result<OfferQuoteResponse>> GetQuoteAsync(OfferDraftRequest req, CancellationToken ct = default);
    Task<Result<List<ItemDTO>>> GetItemsByName(string searchText, CancellationToken ct = default);
    Task<Result<List<ItemDTO>>> GetItemsByNameAndGameId(string searchText, int gameId, CancellationToken ct = default);
    Task<Result<List<GameDTO>>> GetAllGames(CancellationToken ct = default);
    Task<Result<List<GenreDTO>>> GetAllGenres(CancellationToken ct = default);
    Task<Result<List<RarityDTO>>> GetRaritiesByGameId(int gameId, CancellationToken ct = default);
    Task<Result<OfferUpdateQuoteResponse>> GetUpdateQuoteAsync(string auth0UserId, int offerId,
        OfferUpdateDraftRequest request, CancellationToken ct = default);
}

public class OffersService(
    IOffersRepository offersRepository,
    IUsersRepository userRepository,
    IItemsRepository itemRepository,
    IGamesRepository gamesRepository,
    IGenresRepository genresRepository,
    IRaritiesRepository raritiesRepository,
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
        if (query.RarityId is not null && query.RarityId <= 0)
        {
            return Result<PagedResponse<OfferListingDTO>>.BadRequest("incorrect_rarity_id");
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

    
        var (okDraft, errDraft, draft) = await BuildDraftAsync(offerDraftRequest.Title, offerDraftRequest.Description,offerDraftRequest.OfferedItems, offerDraftRequest.WantedItems, offerDraftRequest.DurationDays, offerDraftRequest.IsHighlighted, ct);
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
                Title = draft.Title,
                Description = draft.Description,
                CreationDate = DateTime.UtcNow,
                ExpDate = draft.ExpDate,
                TokenCost = draft.TokenCost,
                User_ID = userState.Id,
                OfferStatus_ID = (int)OfferStatuses.Active,
                IsHighlighted = draft.IsHighlighted,
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
        OfferUpdateDraftRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<OfferDetailsDTO>.Unauthorized("missing_sub_claim");
        if (offerId <= 0) return Result<OfferDetailsDTO>.BadRequest("invalid_offer_id");
        var (okUser, errUser, userState) = await GetActiveUserOrErrorAsync(auth0UserId, ct);
        if (!okUser) return Result<OfferDetailsDTO>.Unauthorized(errUser!);
        var offer = await offersRepository.GetTrackedOfferAsync(offerId, userState!.Id,ct);
        if (offer is null) return Result<OfferDetailsDTO>.NotFound("offer_not_found");
        if (offer.OfferStatus_ID != (int)OfferStatuses.Active)
            return Result<OfferDetailsDTO>.BadRequest("offer_not_active");
        var (okDraft, errDraft, draft) = await BuildDraftForUpdateAsync(request.Title,request.Description,request.OfferedItems, request.WantedItems, request.DurationDays, request.IsHighlighted,offer.ExpDate, ct);
        if (!okDraft)
            return Result<OfferDetailsDTO>.BadRequest(errDraft);
        if (draft is null) return Result<OfferDetailsDTO>.BadRequest("draft_creation_failed");

        
        
        
        
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
            offer.Title = draft.Title;
            offer.Description = draft.Description;
            offer.ExpDate = draft.ExpDate;
            offer.TokenCost = draft.TokenCost;
            offer.IsHighlighted = draft.IsHighlighted;
            
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

    public async Task<Result<OfferQuoteResponse>> GetQuoteAsync(OfferDraftRequest req, CancellationToken ct = default)
    {
        var (okDraft, errDraft, draft) =
            await BuildDraftAsync(req.Title,req.Description,req.OfferedItems, req.WantedItems, req.DurationDays, req.IsHighlighted, ct);
        if (!okDraft) return Result<OfferQuoteResponse>.BadRequest(errDraft);
        if (draft is null) return Result<OfferQuoteResponse>.BadRequest("draft_creation_failed");

        return Result<OfferQuoteResponse>.Success(new OfferQuoteResponse(draft.TokenCost));
    }
    public async Task<Result<List<ItemDTO>>> GetItemsByName(string searchText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return Result<List<ItemDTO>>.Success(new List<ItemDTO>());
        }
        

        var items = await itemRepository.GetByName(searchText,ct);
        var response = items.Select(i => new ItemDTO(
            i.ID,
            i.Name,
            i.Photo_URL,
            i.EstimatedTokenValue,
            new GameDTO(i.Game_ID,i.Game.Name,i.Game.Photo_URL,i.Game.Genre_ID)
            )).ToList();
        return Result<List<ItemDTO>>.Success(response);
    }

    public async Task<Result<List<ItemDTO>>> GetItemsByNameAndGameId(string searchText, int gameId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return Result<List<ItemDTO>>.Success(new List<ItemDTO>());
        }

        if (gameId <= 0)
        {
            return Result<List<ItemDTO>>.BadRequest("Game ID is required");
        }

        var items = await itemRepository.GetByNameAndGameId(searchText, gameId, ct);
        var response = items.Select(i => new ItemDTO(
            i.ID,
            i.Name,
            i.Photo_URL,
            i.EstimatedTokenValue,
            new GameDTO(i.Game_ID,i.Game.Name,i.Game.Photo_URL,i.Game.Genre_ID)
            )).ToList();
        return Result<List<ItemDTO>>.Success(response);

    }

    public async Task<Result<List<GameDTO>>> GetAllGames(CancellationToken ct = default)
    {
        var games = await gamesRepository.GetAll(ct);
        var response = games.Select(g => new GameDTO(
            g.ID,
            g.Name,
            g.Photo_URL,
            g.Genre_ID
        )).ToList();
        return Result<List<GameDTO>>.Success(response);
    }

    public async Task<Result<List<GenreDTO>>> GetAllGenres(CancellationToken ct = default)
    {
        var genres = await genresRepository.GetAll(ct);
        var response = genres.Select(g => new GenreDTO(
            g.ID,
            g.Name
        )).ToList();
        return Result<List<GenreDTO>>.Success(response);
    }

    public async Task<Result<List<RarityDTO>>> GetRaritiesByGameId(int gameId, CancellationToken ct = default)
    {
        if (gameId <= 0)
        {
            return Result<List<RarityDTO>>.BadRequest("invalid_game_id");
        }
        var rarities = await raritiesRepository.GetByGameId(gameId, ct);
        var response = rarities.Select(r => new RarityDTO(
            r.ID,
            r.RarityName
        )).ToList();
        return Result<List<RarityDTO>>.Success(response);
    }

    public async Task<Result<OfferUpdateQuoteResponse>> GetUpdateQuoteAsync(string auth0UserId, int offerId,
        OfferUpdateDraftRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<OfferUpdateQuoteResponse>.Unauthorized("missing_sub_claim");
        if (offerId <= 0) return Result<OfferUpdateQuoteResponse>.BadRequest("invalid_offer_id");
        var (okUser, errUser, userState) = await GetActiveUserOrErrorAsync(auth0UserId, ct);
        if (!okUser) return Result<OfferUpdateQuoteResponse>.Unauthorized(errUser!);
        var offer = await offersRepository.GetTrackedOfferAsync(offerId, userState!.Id,ct);
        if (offer is null) return Result<OfferUpdateQuoteResponse>.NotFound("offer_not_found");
        if (offer.OfferStatus_ID != (int)OfferStatuses.Active)
            return Result<OfferUpdateQuoteResponse>.BadRequest("offer_not_active");
        
        var (okDraft, errDraft, draft) = await BuildDraftForUpdateAsync(request.Title,request.Description,request.OfferedItems, request.WantedItems, request.DurationDays, request.IsHighlighted,offer.ExpDate, ct);
        if (!okDraft)
            return Result<OfferUpdateQuoteResponse>.BadRequest(errDraft);
        if (draft is null) return Result<OfferUpdateQuoteResponse>.BadRequest("draft_creation_failed");
        
        var updateFeeTokens = Math.Max(OffersConsts.MinBaseTokenCost, draft.TokenCost - offer.TokenCost);

        return Result<OfferUpdateQuoteResponse>.Success(new OfferUpdateQuoteResponse(draft.TokenCost, updateFeeTokens));

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
    private async Task<(bool Ok, string? err, OfferDraft? offerDraft)> BuildDraftAsync(string title, string description, IReadOnlyCollection<OfferItemDTO> offeredItems,
        IReadOnlyCollection<OfferItemDTO> wantedItems, int durationDays, bool isHighlighted, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return (false, "title_required", null);
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            return (false, "description_required", null);
        }
        var offered = offeredItems.ToDictionary(x => x.ItemId, x => x.Quantity); 
        var wanted = wantedItems.ToDictionary(x => x.ItemId, x => x.Quantity);

        if (offered.Count == 0)
            return (false, "offered_items_required", null);
        if (wanted.Count == 0)
            return (false, "wanted_items_required", null);
        var highlightFee = isHighlighted ? OffersConsts.HighlightCost : 0;
        
        var (expOk, err, expCalcDate, extraDayCost) = ResolveExpiry(durationDays);
        if (!expOk) return (false, err, null);

        var (okItems, errItems, items) = await LoadItemsOrErrorAsync(offered, wanted, ct);
        if (!okItems) return (false, errItems, null);

        var offeredLines = offered.ToDictionary(kv => kv.Key, kv => new DictItemQuantity(items[kv.Key], kv.Value));
        var wantedLines = wanted.ToDictionary(kv => kv.Key, kv => new DictItemQuantity(items[kv.Key], kv.Value));

        int tokenCost;
        try
        {
            tokenCost = CalculateTokenCost(offeredLines, wantedLines, extraDayCost, highlightFee);
        }
        catch (OverflowException)
        {
            return (false, "token_cost_overflow", null);
        }

        var draft = new OfferDraft(title,description, offeredLines,wantedLines, expCalcDate, tokenCost, isHighlighted);

        return (true, null, draft);

    }
    private async Task<(bool Ok, string? err, OfferDraft? offerDraft)> BuildDraftForUpdateAsync(string title, string description, IReadOnlyCollection<OfferItemDTO> offeredItems,
        IReadOnlyCollection<OfferItemDTO> wantedItems, int durationDays, bool isHighlighted, DateOnly currentExpDate, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return (false, "title_required", null);
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            return (false, "description_required", null);
        }
        var offered = offeredItems.ToDictionary(x => x.ItemId, x => x.Quantity); 
        var wanted = wantedItems.ToDictionary(x => x.ItemId, x => x.Quantity);

        if (offered.Count == 0)
            return (false, "offered_items_required", null);
        if (wanted.Count == 0)
            return (false, "wanted_items_required", null);
        var highlightFee = isHighlighted ? OffersConsts.HighlightCost : 0;

        DateOnly expDate;
        int extraDayCost;
        
        if (durationDays == 0)
        {
            expDate = currentExpDate;
            extraDayCost = 0;
        }
        else
        {
            var (expOk, err, expCalcDate, dayCost) = ResolveExpiry(durationDays);
            if (!expOk) return (false, err, null);
            expDate = expCalcDate;
            extraDayCost = dayCost;
        }

        var (okItems, errItems, items) = await LoadItemsOrErrorAsync(offered, wanted, ct);
        if (!okItems) return (false, errItems, null);

        var offeredLines = offered.ToDictionary(kv => kv.Key, kv => new DictItemQuantity(items[kv.Key], kv.Value));
        var wantedLines = wanted.ToDictionary(kv => kv.Key, kv => new DictItemQuantity(items[kv.Key], kv.Value));

        int tokenCost;
        try
        {
            tokenCost = CalculateTokenCost(offeredLines, wantedLines, extraDayCost, highlightFee);
        }
        catch (OverflowException)
        {
            return (false, "token_cost_overflow", null);
        }

        var draft = new OfferDraft(title,description, offeredLines,wantedLines, expDate, tokenCost, isHighlighted);

        return (true, null, draft);

    }

    private static int CalculateTokenCost(Dictionary<int,DictItemQuantity> offered, Dictionary<int,DictItemQuantity> wanted, int extraDayCost, int highlightFee)
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


        checked
        {
            return baseTokenCost + extraDayCost + highlightFee;
        }
        
    }

    private static (bool Ok, string? Error, DateOnly ExpDate, int ExtraDayCost) ResolveExpiry(int durationDays)
    {
        if (durationDays != 7 && durationDays != 14 && durationDays != 31)
        {
            return (false, "invalid_duration_days", default, 0);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var expDate = today.AddDays(durationDays);
        var durationFee = durationDays switch
        {
            7 => 0,
            14 => OffersConsts.Duration14Cost,
            31 => OffersConsts.Duration31Cost,
            _ => 0
        };
        

        return (true, null, expDate, durationFee);



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