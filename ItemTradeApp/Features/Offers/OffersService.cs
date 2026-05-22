using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Offers.DTOs.RequestDTOs;
using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Offers.Internal;
using ItemTradeApp.Features.Offers.Repositories;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Shared.Emails.Services;
using ItemTradeApp.Features.Shared.Notifications;
using ItemTradeApp.Features.Shared.TokenEscrow;
using ItemTradeApp.Features.Shared.TradeCreation;
using ItemTradeApp.Features.Shared.TradeCreation.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using ItemTradeApp.Resources.NotificationsTemplates;

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

    Task<Result<AcceptOfferResponse>> AcceptOfferAsync(string auth0UserId, int offerId, CancellationToken ct = default);
}

public class OffersService(
    IOffersRepository offersRepository,
    IUsersRepository userRepository,
    IItemsRepository itemRepository,
    IGamesRepository gamesRepository,
    IGenresRepository genresRepository,
    IRaritiesRepository raritiesRepository,
    ITradeRepository tradeRepository,
    ICounterOfferRepository counterOfferRepository,
    ITradeCreation tradeCreation,
    ITokenEscrow tokenEscrow,
    IUnitOfWork unitOfWork,
    INotificationSender notificationSender,
    IEmailGenerationService emailGenerationService
) : IOffersService
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
        var response = await offersRepository.GetOfferWithDetailsByIdAsync(id, ct);
        if (response is null) return Result<OfferDetailsDTO>.NotFound("offer_not_found");

        return Result<OfferDetailsDTO>.Success(response);
    }

    public async Task<Result<OfferDetailsDTO>> CreateOfferAsync(string auth0UserId,
        OfferDraftRequest offerDraftRequest, CancellationToken ct = default)
    {

        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<OfferDetailsDTO>.Unauthorized("missing_sub_claim");

    
        var (okDraft, errDraft, draft) = await BuildDraftAsync(offerDraftRequest.Title, offerDraftRequest.Description,offerDraftRequest.OfferedItems, offerDraftRequest.WantedItems, offerDraftRequest.DurationDays, offerDraftRequest.IsHighlighted, offerDraftRequest.TokensOffered, offerDraftRequest.TokensWanted, ct);
        if (!okDraft)
            return Result<OfferDetailsDTO>.BadRequest(errDraft);
        if (draft is null) return Result<OfferDetailsDTO>.BadRequest("draft_creation_failed");
        
        var (okUser, errUser, userState) = await GetActiveUserOrErrorAsync(auth0UserId, ct);
        if (!okUser) return Result<OfferDetailsDTO>.Unauthorized(errUser!);
        if (userState is null) return Result<OfferDetailsDTO>.BadRequest("user_fetch_error");

        var requiredBalance = draft.TokenCost + draft.TokensOffered;
        if (userState.Tokens < requiredBalance) return Result<OfferDetailsDTO>.BadRequest("not_enough_tokens");

        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var charged = await userRepository.TrySubtractTokenCostAsync(userState.Id, draft.TokenCost, ct);
            if (!charged)
            {
                await tx.RollbackAsync(ct);
                return Result<OfferDetailsDTO>.Conflict("concurrency_conflict");
            }

            if (draft.TokensOffered > 0)
            {
                var locked = await tokenEscrow.TryLockOwnTokensAsync(userState.Id, draft.TokensOffered, ct);
                if (!locked)
                {
                    await tx.RollbackAsync(ct);
                    return Result<OfferDetailsDTO>.Conflict("concurrency_conflict");
                }
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
                TokensOffered = draft.TokensOffered,
                TokensWanted = draft.TokensWanted,
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
            try
            {
                await notificationSender.SendAsync(
                    offer.User_ID,
                    NotificationsMessages.OfferSuccessfullyAdded(offer.Title),
                    ct);

                var offerForEmail = await offersRepository.GetOfferWithItemsAsync(offer.ID, ct);

                if (offerForEmail is not null)
                {
                    await emailGenerationService.SendOfferCreatedAsync(
                        userState.Id,
                        offerForEmail,
                        ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            var response = await offersRepository.GetOfferWithDetailsByIdAsync(offer.ID, ct);
            if (response is null) return Result<OfferDetailsDTO>.InternalServerError("create_offer_failed");
            return Result<OfferDetailsDTO>.Created(response);


        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);

            var message = ex.InnerException?.Message ?? ex.Message;

            return Result<OfferDetailsDTO>.InternalServerError(message);
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
        var (okDraft, errDraft, draft) = await BuildDraftForUpdateAsync(request.Title,request.Description,request.OfferedItems, request.WantedItems, request.DurationDays, request.IsHighlighted, offer.ExpDate, request.TokensOffered, request.TokensWanted, ct);
        if (!okDraft)
            return Result<OfferDetailsDTO>.BadRequest(errDraft);
        if (draft is null) return Result<OfferDetailsDTO>.BadRequest("draft_creation_failed");

        
        
        
        
        var updateFeeTokens = Math.Max(OffersConsts.MinBaseTokenCost, draft.TokenCost - offer.TokenCost);
        var requiredBalance = updateFeeTokens + Math.Max(0, draft.TokensOffered - offer.TokensOffered);
        if (userState.Tokens < requiredBalance) return Result<OfferDetailsDTO>.BadRequest("not_enough_tokens");
        
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var charged = await userRepository.TrySubtractTokenCostAsync(userState.Id, updateFeeTokens, ct);
            if (!charged)
            {
                await tx.RollbackAsync(ct);
                return Result<OfferDetailsDTO>.Conflict("concurrency_conflict");
            }

            var oldOffered = offer.TokensOffered;
            var newOffered = draft.TokensOffered;
            if (newOffered > oldOffered)
            {
                var ok = await tokenEscrow.TryLockOwnTokensAsync(userState.Id, newOffered - oldOffered, ct);
                if (!ok)
                {
                    await tx.RollbackAsync(ct);
                    return Result<OfferDetailsDTO>.BadRequest("not_enough_tokens");
                }
            }else if (newOffered < oldOffered)
            {
                var ok = await tokenEscrow.TryReleaseOwnEscrowAsync(userState.Id, oldOffered - newOffered, ct);
                if (!ok)
                {
                    await tx.RollbackAsync(ct);
                    return Result<OfferDetailsDTO>.BadRequest("escrow_token_failed");
                }
            }

            ApplyListingItemsUpdate(offer, draft.Offered, draft.Wanted);
            offer.Title = draft.Title;
            offer.Description = draft.Description;
            offer.ExpDate = draft.ExpDate;
            offer.TokenCost = draft.TokenCost;
            offer.IsHighlighted = draft.IsHighlighted;
            offer.TokensOffered = draft.TokensOffered;
            offer.TokensWanted = draft.TokensWanted;
            await unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var response = await offersRepository.GetOfferWithDetailsByIdAsync(offer.ID, ct);
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

        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var offer = await offersRepository.GetTrackedOfferAsync(offerId, userState.Id, ct);
            if (offer is null)
            {
                return Result<string>.NotFound("offer_not_found");
            }

            if (offer.OfferStatus_ID != (int)OfferStatuses.Active)
            {
                return Result<string>.BadRequest("offer_not_active");

            }

            var updated = await offersRepository.CancelOfferAsync(userState.Id, offerId, ct);
            if (!updated)
            {
                await tx.RollbackAsync(ct);
                return Result<string>.BadRequest("cancel_offer_failed");
                
            }

            if (offer.TokensOffered > 0)
            {
                var released = await tokenEscrow.TryReleaseOwnEscrowAsync(userState.Id, offer.TokensOffered, ct);
                if (!released)
                {
                    await tx.RollbackAsync(ct);
                    return Result<string>.Conflict("escrow_token_failed");
                }
            }

            await RefundAndDenyPendingCounterOffersAsync(offer.ID, ct);

            await unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<string>.Success("offer_cancelled");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            return Result<string>.InternalServerError("cancel_offer_failed");
        }
    }

    public async Task<Result<OfferQuoteResponse>> GetQuoteAsync(OfferDraftRequest req, CancellationToken ct = default)
    {
        var (okDraft, errDraft, draft) =
            await BuildDraftAsync(req.Title,req.Description,req.OfferedItems, req.WantedItems, req.DurationDays, req.IsHighlighted,req.TokensOffered, req.TokensWanted, ct);
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
        
        var (okDraft, errDraft, draft) = await BuildDraftForUpdateAsync(request.Title,request.Description,request.OfferedItems, request.WantedItems, request.DurationDays, request.IsHighlighted,offer.ExpDate, request.TokensOffered, request.TokensWanted, ct);
        if (!okDraft)
            return Result<OfferUpdateQuoteResponse>.BadRequest(errDraft);
        if (draft is null) return Result<OfferUpdateQuoteResponse>.BadRequest("draft_creation_failed");
        
        var updateFeeTokens = Math.Max(OffersConsts.MinBaseTokenCost, draft.TokenCost - offer.TokenCost);

        return Result<OfferUpdateQuoteResponse>.Success(new OfferUpdateQuoteResponse(draft.TokenCost, updateFeeTokens));

    }

    public async Task<Result<AcceptOfferResponse>> AcceptOfferAsync(string auth0UserId, int offerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<AcceptOfferResponse>.Unauthorized("missing_sub_claim");
        if (offerId <= 0) return Result<AcceptOfferResponse>.BadRequest("invalid_offer_id");
        
        var (okUser, errUser, userState) = await GetActiveUserOrErrorAsync(auth0UserId, ct);
        if (!okUser) return Result<AcceptOfferResponse>.Unauthorized(errUser!);
        if (userState is null) return Result<AcceptOfferResponse>.Unauthorized("user_fetch_error");
        var buyer = await userRepository
            .GetNotificationDataByAuth0IdAsync(auth0UserId, ct);
        if (buyer is null)
            return Result<AcceptOfferResponse>.Unauthorized("buyer_not_found");

        await using var tx = await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var offer = await offersRepository.GetOfferByIdAsync(offerId, ct);
            if (offer is null)
            {
                return Result<AcceptOfferResponse>.NotFound("offer_not_found");
            }

            if (offer.User_ID == userState.Id)
            {
                return Result<AcceptOfferResponse>.BadRequest("cannot_accept_own_offer");
            }

            if (offer.OfferStatus_ID != (int)OfferStatuses.Active)
            {
                return Result<AcceptOfferResponse>.BadRequest("offer_not_active");
                
            }
            
            if (offer.ExpDate < DateOnly.FromDateTime(DateTime.UtcNow))
            {
                return Result<AcceptOfferResponse>.BadRequest("offer_expired");
                
            }
            
            if (await tradeRepository.TradeExistsForOfferAsync(offerId, ct))
            {
                return Result<AcceptOfferResponse>.Conflict("trade_already_exists");
            }


            var setInRealization = await offersRepository.SetOfferInRealizationAsync(offer.ID, ct);
            if (!setInRealization)
            {
                return Result<AcceptOfferResponse>.Conflict("trade_already_exists");
            }

            await RefundAndDenyPendingCounterOffersAsync(offer.ID, ct);

            if (offer.TokensOffered > 0)
            {
                var transferred =
                    await tokenEscrow.TryTransferEscrowAsync(offer.User_ID, userState.Id, offer.TokensOffered, ct);
                if (!transferred)
                {
                    await tx.RollbackAsync(ct);
                    return Result<AcceptOfferResponse>.Conflict("escrow_failed");
                }
            }
            
            if (offer.TokensWanted > 0)
            {
                var transferred =
                    await tokenEscrow.TryEscrowToOtherAsync(userState.Id, offer.User_ID, offer.TokensWanted, ct);
                if (!transferred)
                {
                    await tx.RollbackAsync(ct);
                    return Result<AcceptOfferResponse>.Conflict("escrow_failed");
                }
            }

            var trade = await tradeCreation.ExecuteAsync(
                new CreateTradeContext(offer.ID, userState.Id, offer.User_ID), ct);

            var seller = await userRepository
                .GetNotificationDataByIdAsync(offer.User_ID, ct);

            if (seller is null)
            {
                await tx.RollbackAsync(ct);
                return Result<AcceptOfferResponse>.NotFound("seller_not_found");
            }
            
            await unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            
            try
            {
                await notificationSender.SendAsync(
                    offer.User_ID,
                    NotificationsMessages.TradeCreatedFromOffer(
                        offer.Title),
                    ct);

                await notificationSender.SendAsync(
                    userState.Id,
                    NotificationsMessages.TradeCreatedFromOffer(
                        offer.Title),
                    ct);
                
                
                var buyerNick = buyer.Nickname ?? buyer.Email;
                var sellerNick = seller.Nickname ?? seller.Email;

                await emailGenerationService.SendTradeCreatedAsync(
                    offer.User_ID,
                    buyerNick,
                    sellerNick,
                    trade,
                    offer,
                    ct);

                await emailGenerationService.SendTradeCreatedAsync(
                    userState.Id,
                    buyerNick,
                    sellerNick,
                    trade,
                    offer,
                    ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return Result<AcceptOfferResponse>.Success(new AcceptOfferResponse(trade.ID, offer.ID));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            return Result<AcceptOfferResponse>.InternalServerError("accept_offer_failed");
        }

    }

    #region OfferServiceHelpers

    private async Task RefundAndDenyPendingCounterOffersAsync(int offerId, CancellationToken ct)
    {
        var pending = await counterOfferRepository.GetAllPendingForOfferAsync(offerId, ct);
        foreach (var co in pending)
        {
            if (co.TokensOffered > 0)
            {
                await tokenEscrow.TryReleaseOwnEscrowAsync(co.User_ID, co.TokensOffered, ct);
            }

            co.CounterOfferStatus_Id = (int)CounterOfferStatuses.Denied;
        }
    }

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
        IReadOnlyCollection<OfferItemDTO> wantedItems, int durationDays, bool isHighlighted, int tokensOffered, int tokensWanted, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length < 3)
        {
            return (false, "title_required", null);
        }
        if (string.IsNullOrWhiteSpace(description) || description.Length < 3)
        {
            return (false, "description_required", null);
        }
        var offered = offeredItems.ToDictionary(x => x.ItemId, x => x.Quantity); 
        var wanted = wantedItems.ToDictionary(x => x.ItemId, x => x.Quantity);

        if (offered.Count == 0 && wanted.Count == 0)
        {
            return (false, "at_least_one_side_must_have_items", null);
        }
        if (offered.Count == 0 && tokensOffered <= 0)
        {
            return (false, "must_offer_tokens_when_no_items_offered", null);
        }
        if (wanted.Count == 0 && tokensWanted <= 0)
        {
            return (false, "must_want_tokens_when_no_items_wanted", null);
        }

        if (tokensWanted < 0)
            return (false, "tokens_wanted_negative", null);
        if (tokensOffered < 0)
            return (false, "tokens_offered_negative", null);
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
            tokenCost = CalculateTokenCost(offeredLines, wantedLines, extraDayCost, highlightFee, tokensOffered,tokensWanted);
        }
        catch (OverflowException)
        {
            return (false, "token_cost_overflow", null);
        }

        var draft = new OfferDraft(title,description, offeredLines,wantedLines, expCalcDate, tokenCost, isHighlighted, tokensOffered, tokensWanted);

        return (true, null, draft);

    }
    private async Task<(bool Ok, string? err, OfferDraft? offerDraft)> BuildDraftForUpdateAsync(string title, string description, IReadOnlyCollection<OfferItemDTO> offeredItems,
        IReadOnlyCollection<OfferItemDTO> wantedItems, int durationDays, bool isHighlighted, DateOnly currentExpDate, int tokensOffered, int tokensWanted, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length < 3)
        {
            return (false, "title_required", null);
        }
        if (string.IsNullOrWhiteSpace(description) || description.Length < 3)
        {
            return (false, "description_required", null);
        }
        var offered = offeredItems.ToDictionary(x => x.ItemId, x => x.Quantity); 
        var wanted = wantedItems.ToDictionary(x => x.ItemId, x => x.Quantity);

        if (offered.Count == 0 && wanted.Count == 0)
        {
            return (false, "at_least_one_side_must_have_items", null);
        }
        if (offered.Count == 0 && tokensOffered <= 0)
        {
            return (false, "must_offer_tokens_when_no_items_offered", null);
        }
        if (wanted.Count == 0 && tokensWanted <= 0)
        {
            return (false, "must_want_tokens_when_no_items_wanted", null);
        }
        
        var highlightFee = isHighlighted ? OffersConsts.HighlightCost : 0;
        
        if (tokensWanted < 0)
            return (false, "tokens_wanted_negative", null);
        if (tokensOffered < 0)
            return (false, "tokens_offered_negative", null);

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
            tokenCost = CalculateTokenCost(offeredLines, wantedLines, extraDayCost, highlightFee, tokensOffered, tokensWanted);
        }
        catch (OverflowException)
        {
            return (false, "token_cost_overflow", null);
        }

        var draft = new OfferDraft(title,description, offeredLines,wantedLines, expDate, tokenCost, isHighlighted, tokensOffered, tokensWanted);

        return (true, null, draft);

    }

    private static int CalculateTokenCost(Dictionary<int,DictItemQuantity> offered, Dictionary<int,DictItemQuantity> wanted, int extraDayCost, int highlightFee, int tokensOffered, int tokensWanted)
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

        var itemContribution = (int)Math.Ceiling(totalValue * OffersConsts.BaseCostRate);
        var tokenContribution = (int)Math.Ceiling((tokensOffered + tokensWanted) * OffersConsts.BaseCostRate);

        int baseTokenCost;
        if (itemContribution < OffersConsts.MinBaseTokenCost)
        {
            baseTokenCost = OffersConsts.MinBaseTokenCost + tokenContribution;
        }
        else
        {
            baseTokenCost = tokenContribution + itemContribution;
        }


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