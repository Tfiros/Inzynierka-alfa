using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.ResponseDTOs;
using ItemTradeApp.Features.CounterOffers.Repositories;
using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.TokenEscrow;
using ItemTradeApp.Features.Shared.TradeCreation;
using ItemTradeApp.Features.Shared.TradeCreation.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.CounterOffers;

public interface ICounterOffersService
{
    Task<Result<PagedResponse<CounterOfferListItemDto>>> GetSentCounterOffers(
        string auth0UserId,
        CounterOfferListingsQuery query,
        CancellationToken ct = default);

    Task<Result<PagedResponse<CounterOfferListItemDto>>> GetReceivedCounterOffers(
        string auth0UserId,
        CounterOfferListingsQuery query,
        CancellationToken ct = default);
    
    Task<Result<CounterOfferDto>> CreateCounterOfferAsync(
        string auth0UserId,
        int offerId,
        CounterOfferDraftRequest request,
        CancellationToken ct);

    Task<Result<CounterOfferDto>> UpdateCounterOfferStatusAsync(
        string auth0UserId,
        int counterOfferId,
        int statusId,
        CancellationToken ct = default);

    Task<Result<AcceptCounterOfferResponse>> AcceptCounterOfferAsync(
        string auth0UserId,
        int counterOfferId,
        CancellationToken ct = default);

    Task<Result<CounterOfferCostDto>> QuoteCounterOfferAsync(
        string auth0UserId,
        int offerId,
        CounterOfferDraftRequest request,
        CancellationToken ct = default);
    Task<Result<List<CounterOfferListItemDto>>> GetCounterOffersForOfferAsync(
        string auth0UserId,
        int offerId,
        CancellationToken ct = default);
}

public sealed class CounterOffersService(
    ICounterOffersRepository repository,
    IOfferRepository offerRepository,
    ITradeRepository tradeRepository,
    IItemsRepository itemsRepository,
    IUserRepository userRepository,
    ITokenEscrow tokenEscrow,
    IUnitOfWork unitOfWork,
    ITradeCreation tradeCreation) : ICounterOffersService
{
    
    private async Task<User?> GetUserAsync(string auth0UserId, CancellationToken ct)
    {
        return await userRepository.GetUserInfo(auth0UserId, ct);
    }

    private async Task<(User? User, Result<T>? Error)> GetActiveUser<T>(
        string auth0UserId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return (null, Result<T>.Unauthorized("missing_sub_claim"));

        var user = await GetUserAsync(auth0UserId, ct);

        if (user is null)
            return (null, Result<T>.Unauthorized("Nie znaleziono użytkownika"));
        

        return (user, null);
    }

    private static Result<T>? ValidateOfferForCounterOffer<T>(Offer? offer, int userId)
    {
        if (offer is null)
            return Result<T>.NotFound("Nie znaleziono oferty");

        if (offer.User_ID == userId)
            return Result<T>.BadRequest("Nie można złożyć kontroferty do swojej oferty");

        if (offer.OfferStatus_ID != (int)OfferStatuses.Active)
            return Result<T>.BadRequest("Oferta nieaktywna");

        if (offer.ExpDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<T>.BadRequest("Oferta wygasła");

        return null;
    }

    private static Result<CounterOfferDto>? ValidateCreateRequest(
        string auth0UserId,
        int offerId,
        CounterOfferDraftRequest request)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<CounterOfferDto>.Unauthorized("missing_sub_claim");

        if (offerId <= 0)
            return Result<CounterOfferDto>.BadRequest("Niepoprawne ID oferty");

        if (request.Items is null)
            return Result<CounterOfferDto>.BadRequest("Brak listy przedmiotów");
        
        if (request.Items.Count == 0 && request.TokensOffered <= 0)
            return Result<CounterOfferDto>.BadRequest(
                "Kontroferta musi zawierać co najmniej jeden przedmiot lub tokeny."
            );
        
        if (request.Items.Any(x => x.ItemId <= 0))
            return Result<CounterOfferDto>.BadRequest("Niepoprawne ID przedmiotu");

        if (request.Items.Any(x => x.Quantity <= 0))
            return Result<CounterOfferDto>.BadRequest("Niepoprawna ilość");

        if (request.TokensOffered < 0)
            return Result<CounterOfferDto>.BadRequest("Niepoprawna ilość tokenów");

        return null;
    }

    private static CounterOfferDto MapToCounterOfferDto(CounterOffer counterOffer)
    {
        return new CounterOfferDto
        {
            Id = counterOffer.ID,
            OfferId = counterOffer.Offer_Id,
            UserId = counterOffer.User_ID,
            CreationDate = counterOffer.CreationDate,
            CounterOfferStatusId = counterOffer.CounterOfferStatus_Id,
            TokensOffered = counterOffer.TokensOffered,
            Items = counterOffer.ListingCounterOfferItems
                .Select(i => new CounterOfferItemDto(i.Item_ID, i.Quantity))
                .ToList()
        };
    }

public async Task<Result<PagedResponse<CounterOfferListItemDto>>> GetSentCounterOffers(
    string auth0UserId,
    CounterOfferListingsQuery query,
    CancellationToken ct = default)
{
    if (query.Page <= 0)
        return Result<PagedResponse<CounterOfferListItemDto>>.BadRequest("invalid_page_number");

    if (query.PageSize <= 0)
        return Result<PagedResponse<CounterOfferListItemDto>>.BadRequest("invalid_page_size");

    query.PageSize = query.PageSize > 100 ? 100 : query.PageSize;

    var (user, userError) = await GetActiveUser<PagedResponse<CounterOfferListItemDto>>(auth0UserId, ct);
    if (userError is not null)
        return userError;

    var (items, totalCount) = await repository.GetSentCounterOffersAsync(user!.ID, query, ct);

    var totalPages = totalCount == 0
        ? 1
        : (int)Math.Ceiling(totalCount / (double)query.PageSize);

    var response = new PagedResponse<CounterOfferListItemDto>
    {
        Page = query.Page,
        PageSize = query.PageSize,
        TotalCount = totalCount,
        TotalPages = totalPages,
        Elements = items
    };

    return Result<PagedResponse<CounterOfferListItemDto>>.Success(response);
}

public async Task<Result<PagedResponse<CounterOfferListItemDto>>> GetReceivedCounterOffers(
    string auth0UserId,
    CounterOfferListingsQuery query,
    CancellationToken ct = default)
{
    if (query.Page <= 0)
        return Result<PagedResponse<CounterOfferListItemDto>>.BadRequest("invalid_page_number");

    if (query.PageSize <= 0)
        return Result<PagedResponse<CounterOfferListItemDto>>.BadRequest("invalid_page_size");

    query.PageSize = query.PageSize > 100 ? 100 : query.PageSize;

    var (user, userError) = await GetActiveUser<PagedResponse<CounterOfferListItemDto>>(auth0UserId, ct);
    if (userError is not null)
        return userError;

    var (items, totalCount) = await repository.GetReceivedCounterOffersAsync(user!.ID, query, ct);

    var totalPages = totalCount == 0
        ? 1
        : (int)Math.Ceiling(totalCount / (double)query.PageSize);

    var response = new PagedResponse<CounterOfferListItemDto>
    {
        Page = query.Page,
        PageSize = query.PageSize,
        TotalCount = totalCount,
        TotalPages = totalPages,
        Elements = items
    };

    return Result<PagedResponse<CounterOfferListItemDto>>.Success(response);
}

    public async Task<Result<CounterOfferDto>> CreateCounterOfferAsync(
        string auth0UserId,
        int offerId,
        CounterOfferDraftRequest request,
        CancellationToken ct = default)
    {
        var reqValidation = ValidateCreateRequest(auth0UserId, offerId, request);
        if (reqValidation is not null)
            return reqValidation;

        var (user, userError) = await GetActiveUser<CounterOfferDto>(auth0UserId, ct);
        if (userError is not null)
            return userError;

        var offer = await offerRepository.GetOfferAsync(offerId, ct);

        var offerValidation = ValidateOfferForCounterOffer<CounterOfferDto>(offer, user!.ID);
        if (offerValidation is not null)
            return offerValidation;

        var itemIds = request.Items.Select(x => x.ItemId).ToArray();
        if (itemIds.Length != itemIds.Distinct().Count())
            return Result<CounterOfferDto>.BadRequest("Przedmioty w kontrofercie muszą być unikalne");

        var allItemsExist = await itemsRepository.AllItemsExistAsync(itemIds, ct);
        if (!allItemsExist)
            return Result<CounterOfferDto>.BadRequest("Jeden z przedmiotów nie istnieje.");

        var totalToCharge = request.TokensOffered + Consts.CounterOfferCreationFee;

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            if (totalToCharge > 0)
            {
                if (user.Tokens < totalToCharge)
                {
                    await transaction.RollbackAsync(ct);
                    return Result<CounterOfferDto>.BadRequest("Za mało tokenów");
                }

                if (Consts.CounterOfferCreationFee > 0)
                {
                    var charged =
                        await userRepository.TrySubtractTokenCostAsync(user.ID, Consts.CounterOfferCreationFee, ct);
                    if (!charged)
                    {
                        await transaction.RollbackAsync(ct);
                        return Result<CounterOfferDto>.BadRequest("Za mało tokenów");

                    }
                }

                if (request.TokensOffered > 0)
                {
                    var locked = await tokenEscrow.TryLockOwnTokensAsync(user.ID, request.TokensOffered, ct);
                    if (!locked)
                    {
                        await transaction.RollbackAsync(ct);
                        return Result<CounterOfferDto>.BadRequest("token_escrow_failed");
                    }
                }
            }

            var counterOffer = new CounterOffer
            {
                User_ID = user.ID,
                Offer_Id = offerId,
                CreationDate = DateTime.UtcNow,
                TokensOffered = request.TokensOffered,
                CounterOfferStatus_Id = (int)CounterOfferStatuses.Pending,
                ListingCounterOfferItems = request.Items
                    .Select(x => new ListingCounterOfferItem
                    {
                        Item_ID = x.ItemId,
                        Quantity = x.Quantity
                    })
                    .ToList()
            };

            repository.AddCounterOffer(counterOffer);

            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Result<CounterOfferDto>.Created(MapToCounterOfferDto(counterOffer));
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            return Result<CounterOfferDto>.InternalServerError("Stworzenie kontroferty nie powiodło się");
        }
    }

    public async Task<Result<CounterOfferDto>> UpdateCounterOfferStatusAsync(
        string auth0UserId,
        int counterOfferId,
        int statusId,
        CancellationToken ct = default)
    {
        if (counterOfferId <= 0)
            return Result<CounterOfferDto>.BadRequest("Niepoprawne ID kontroferty");

        if (statusId != (int)CounterOfferStatuses.Denied)
            return Result<CounterOfferDto>.BadRequest("Niepoprawny status");

        var (user, userError) = await GetActiveUser<CounterOfferDto>(auth0UserId, ct);
        if (userError is not null)
            return userError;

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var counterOffer = await repository.GetCounterOfferWithOfferAndItemsAsync(counterOfferId, ct);
            if (counterOffer is null)
                return Result<CounterOfferDto>.NotFound("Nie znaleziono kontroferty");

            var offerOwnerId = await offerRepository.GetOfferOwnerIdAsync(counterOffer.Offer_Id, ct);
            if (offerOwnerId != user!.ID)
                return Result<CounterOfferDto>.Forbidden();

            if (counterOffer.CounterOfferStatus_Id != (int)CounterOfferStatuses.Pending)
                return Result<CounterOfferDto>.BadRequest("Kontroferta nie jest pending");

            counterOffer.CounterOfferStatus_Id = statusId;

            if (counterOffer.TokensOffered > 0)
            {
                var transferred = await tokenEscrow.TryReleaseOwnEscrowAsync(counterOffer.User_ID, counterOffer.TokensOffered, ct);
                if (!transferred)
                {
                    await transaction.RollbackAsync(ct);
                    return Result<CounterOfferDto>.BadRequest("token_escrow_release_failed");
                }
            }

            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Result<CounterOfferDto>.Success(MapToCounterOfferDto(counterOffer));
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            return Result<CounterOfferDto>.InternalServerError("Aktualizacja statusu kontroferty nie powiodła się");
        }
    }

    public async Task<Result<AcceptCounterOfferResponse>> AcceptCounterOfferAsync(
        string auth0UserId,
        int counterOfferId,
        CancellationToken ct = default)
    {
        if (counterOfferId <= 0)
            return Result<AcceptCounterOfferResponse>.BadRequest("Niepoprawne ID KO");

        var (caller, userError) = await GetActiveUser<AcceptCounterOfferResponse>(auth0UserId, ct);
        if (userError is not null)
            return userError;

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var counterOffer = await repository.GetCounterOfferWithOfferAndItemsAsync(counterOfferId, ct);

            if (counterOffer is null)
                return Result<AcceptCounterOfferResponse>.NotFound("KO nie znalezione");

            var offer = counterOffer.Offer;
            if (offer is null)
                return Result<AcceptCounterOfferResponse>.NotFound("Oferta nie znaleziona");

            if (offer.User_ID != caller!.ID)
                return Result<AcceptCounterOfferResponse>.Forbidden("Nie jesteś właścicielem oferty");

            if (counterOffer.CounterOfferStatus_Id != (int)CounterOfferStatuses.Pending)
                return Result<AcceptCounterOfferResponse>.BadRequest("Niepoprawny status kontroferty");

            if (offer.OfferStatus_ID != (int)OfferStatuses.Active)
                return Result<AcceptCounterOfferResponse>.BadRequest("Oferta nie jest aktywna");

            if (offer.ExpDate < DateOnly.FromDateTime(DateTime.UtcNow))
                return Result<AcceptCounterOfferResponse>.BadRequest("Oferta jest przeterminowana");

            var tradeExists = await tradeRepository.TradeExistsForOfferAsync(offer.ID, ct);
            if (tradeExists)
                return Result<AcceptCounterOfferResponse>.Conflict("Trade już istnieje");

            counterOffer.CounterOfferStatus_Id = (int)CounterOfferStatuses.Accepted;
            offer.OfferStatus_ID = (int)OfferStatuses.InRealization;

            var otherPendingCounterOffers = await repository.GetOtherPendingCounterOffersForOfferAsync(
                offer.ID,
                counterOffer.ID,
                ct);

            foreach (var otherCounterOffer in otherPendingCounterOffers)
            {
                otherCounterOffer.CounterOfferStatus_Id = (int)CounterOfferStatuses.Denied;

                if (otherCounterOffer.TokensOffered > 0)
                {
                    var transferred =
                        await tokenEscrow.TryReleaseOwnEscrowAsync(otherCounterOffer.User_ID, otherCounterOffer.TokensOffered, ct);
                    if (!transferred)
                    {
                        await transaction.RollbackAsync(ct);
                        return Result<AcceptCounterOfferResponse>.BadRequest("token_release_failed");
                    }
                }
            }

            var oldWantedItems = offer.ListingItems
                .Where(x => x.IsWanted)
                .ToList();

            if (oldWantedItems.Any())
            {
                offerRepository.RemoveListingItems(oldWantedItems);
            }

            foreach (var counterItem in counterOffer.ListingCounterOfferItems)
            {
                offer.ListingItems.Add(new ListingItems
                {
                    Offer_ID = offer.ID,
                    Item_ID = counterItem.Item_ID,
                    Quantity = counterItem.Quantity,
                    IsWanted = true
                });
            }
            offer.TokensWanted = counterOffer.TokensOffered;

            var context = new CreateTradeContext(
                OfferId: offer.ID,
                BuyerId: counterOffer.User_ID,
                SellerId: offer.User_ID,
                TokenCost: 0
            );

            var createdTrade = await tradeCreation.ExecuteAsync(context, ct);

            if (offer.TokensOffered > 0)
            {
                var transferred =
                    await tokenEscrow.TryTransferEscrowAsync(offer.User_ID, counterOffer.User_ID, offer.TokensOffered,
                        ct);
                if (!transferred)
                {
                    await transaction.RollbackAsync(ct);
                    return Result<AcceptCounterOfferResponse>.Conflict("escrow_transfer_failed");
                }
            }

            if (counterOffer.TokensOffered > 0)
            {
                var transferred =
                    await tokenEscrow.TryTransferEscrowAsync(counterOffer.User_ID, offer.User_ID, counterOffer.TokensOffered,
                        ct);
                if (!transferred)
                {
                    await transaction.RollbackAsync(ct);
                    return Result<AcceptCounterOfferResponse>.Conflict("escrow_transfer_failed");
                }
            }
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Result<AcceptCounterOfferResponse>.Success(
                new AcceptCounterOfferResponse(
                    TradeId: createdTrade.ID,
                    OfferId: offer.ID,
                    AcceptedCounterOfferId: counterOffer.ID
                )
            );
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            return Result<AcceptCounterOfferResponse>.InternalServerError("Akceptacja nie powiodła się");
        }
    }
    
    public async Task<Result<CounterOfferCostDto>> QuoteCounterOfferAsync(
        string auth0UserId,
        int offerId,
        CounterOfferDraftRequest request,
        CancellationToken ct = default)
    {
        var validation = await ValidateCounterOfferForQuote(auth0UserId, offerId, request, ct);
        if (validation is not null)
            return validation;

        var finalCost = Consts.CounterOfferCreationFee;

        return Result<CounterOfferCostDto>.Success(
            new CounterOfferCostDto(
                TotalCost: finalCost
            )
        );
    }
    
private async Task<Result<CounterOfferCostDto>?> ValidateCounterOfferForQuote(
    string auth0UserId,
    int offerId,
    CounterOfferDraftRequest request,
    CancellationToken ct)
{
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<CounterOfferCostDto>.Unauthorized("missing_sub_claim");

        if (offerId <= 0)
            return Result<CounterOfferCostDto>.BadRequest("Niepoprawne ID oferty");

        if (request.Items is null)
            return Result<CounterOfferCostDto>.BadRequest("Brak listy przedmiotów");
        
        if (request.Items.Count == 0 && request.TokensOffered <= 0)
            return Result<CounterOfferCostDto>.BadRequest(
                "Kontroferta musi zawierać co najmniej jeden przedmiot lub tokeny."
            );
        
        if (request.Items.Any(x => x.ItemId <= 0))
            return Result<CounterOfferCostDto>.BadRequest("Niepoprawne ID przedmiotu");

        if (request.Items.Any(x => x.Quantity <= 0))
            return Result<CounterOfferCostDto>.BadRequest("Niepoprawna ilość");

        if (request.TokensOffered < 0)
            return Result<CounterOfferCostDto>.BadRequest("Niepoprawna ilość tokenów");

        var (user, userError) = await GetActiveUser<CounterOfferCostDto>(auth0UserId, ct);
        if (userError is not null)
            return userError;

        var offer = await offerRepository.GetOfferAsync(offerId, ct);

        var offerValidation = ValidateOfferForCounterOffer<CounterOfferCostDto>(offer, user!.ID);
        if (offerValidation is not null)
            return offerValidation;

        var itemIds = request.Items.Select(x => x.ItemId).ToArray();
        if (itemIds.Length != itemIds.Distinct().Count())
            return Result<CounterOfferCostDto>.BadRequest("Przedmioty w kontrofercie muszą być unikalne");

        var allItemsExist = await itemsRepository.AllItemsExistAsync(itemIds, ct);
        if (!allItemsExist)
            return Result<CounterOfferCostDto>.BadRequest("Jeden z przedmiotów nie istnieje.");

        return null;
    }

public async Task<Result<List<CounterOfferListItemDto>>> GetCounterOffersForOfferAsync(
    string auth0UserId,
    int offerId,
    CancellationToken ct = default)
{
    if (offerId <= 0)
        return Result<List<CounterOfferListItemDto>>.BadRequest("invalid_offer_id");

    var (user, userError) = await GetActiveUser<List<CounterOfferListItemDto>>(auth0UserId, ct);
    if (userError is not null)
        return userError;

    var offerOwnerId = await offerRepository.GetOfferOwnerIdAsync(offerId, ct);

    if (offerOwnerId != user!.ID)
        return Result<List<CounterOfferListItemDto>>.Forbidden();

    var items = await repository.GetPendingCounterOffersForOfferAsync(offerId, ct);

    return Result<List<CounterOfferListItemDto>>.Success(items);
}
}