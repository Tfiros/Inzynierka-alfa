using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Shared.TradeCreation;
using ItemTradeApp.Features.Shared.TradeCreation.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.CounterOffers;

public interface ICounterOffersService
{
    Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetSentCounterOffers(string auth0UserId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetReceivedCounterOffers(string auth0UserId, CancellationToken ct = default);

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
}

public class CounterOffersService(
    ICounterOffersRepository repository,
    IUnitOfWork unitOfWork,
    ITradeCreation tradeCreation) : ICounterOffersService
{


    private async Task<User?> GetUserAsync(string auth0UserId, CancellationToken ct)
    {
        return await repository.GetUserInfo(auth0UserId, ct);
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

        if (user.IsDeleted)
            return (null, Result<T>.Unauthorized("Użytkownik nie istnieje"));

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

        if (request.Items.Any(x => x.ItemId <= 0))
            return Result<CounterOfferDto>.BadRequest("Niepoprawne ID przedmiotu");

        if (request.Items.Any(x => x.Quantity <= 0))
            return Result<CounterOfferDto>.BadRequest("Niepoprawna ilość");

        if (request.TokensOffered < 0)
            return Result<CounterOfferDto>.BadRequest("Niepoprawna ilość tokenów");
        
        if ((request.Items is null || request.Items.Count == 0) && request.TokensOffered <= 0)
            return Result<CounterOfferDto>.BadRequest("Kontroferta musi zawierać co najmniej jeden przedmiot lub tokeny.");

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

    public async Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetSentCounterOffers(
        string auth0UserId,
        CancellationToken ct = default)
    {
        var (user, userError) = await GetActiveUser<IReadOnlyList<CounterOfferListItemDto>>(auth0UserId, ct);
        if (userError is not null)
            return userError;

        var counterOffers = await repository.GetSentCounterOffersAsync(user!.ID, ct);
        return Result<IReadOnlyList<CounterOfferListItemDto>>.Success(counterOffers);
    }

    public async Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetReceivedCounterOffers(
        string auth0UserId,
        CancellationToken ct = default)
    {
        var (user, userError) = await GetActiveUser<IReadOnlyList<CounterOfferListItemDto>>(auth0UserId, ct);
        if (userError is not null)
            return userError;

        var counterOffers = await repository.GetReceivedCounterOffersAsync(user!.ID, ct);
        return Result<IReadOnlyList<CounterOfferListItemDto>>.Success(counterOffers);
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

        var offer = await repository.GetOfferAsync(offerId, ct);

        var offerValidation = ValidateOfferForCounterOffer<CounterOfferDto>(offer, user!.ID);
        if (offerValidation is not null)
            return offerValidation;

        var itemIds = request.Items.Select(x => x.ItemId).ToArray();
        if (itemIds.Length != itemIds.Distinct().Count())
            return Result<CounterOfferDto>.BadRequest("Przedmioty w kontrofercie muszą być unikalne");

        var allItemsExist = await repository.AllItemsExistAsync(itemIds, ct);
        if (!allItemsExist)
            return Result<CounterOfferDto>.BadRequest("Jeden z przedmiotów nie istnieje.");

        var totalToCharge = request.TokensOffered + Consts.CounterOfferCreationFee;

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            if (totalToCharge > 0)
            {
                if (user.Tokens < totalToCharge)
                    return Result<CounterOfferDto>.BadRequest("Za mało tokenów");

                user.Tokens -= totalToCharge;
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

            var offerOwnerId = await repository.GetOfferOwnerIdAsync(counterOffer.Offer_Id, ct);
            if (offerOwnerId != user!.ID)
                return Result<CounterOfferDto>.Unauthorized();

            if (counterOffer.CounterOfferStatus_Id != (int)CounterOfferStatuses.Pending)
                return Result<CounterOfferDto>.BadRequest("Kontroferta nie jest pending");

            counterOffer.CounterOfferStatus_Id = statusId;

            if (counterOffer.TokensOffered > 0)
            {
                var sender = await repository.GetUserEntityByIdAsync(counterOffer.User_ID, ct);
                if (sender is not null && !sender.IsDeleted)
                {
                    sender.Tokens += counterOffer.TokensOffered;
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

            var tradeExists = await repository.TradeExistsForOfferAsync(offer.ID, ct);
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
                    var sender = await repository.GetUserEntityByIdAsync(otherCounterOffer.User_ID, ct);
                    if (sender is not null && !sender.IsDeleted)
                    {
                        sender.Tokens += otherCounterOffer.TokensOffered;
                    }
                }
            }

            var oldWantedItems = offer.ListingItems
                .Where(x => x.IsWanted)
                .ToList();

            if (oldWantedItems.Any())
            {
                repository.RemoveListingItems(oldWantedItems);
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

            var context = new CreateTradeContext(
                OfferId: offer.ID,
                BuyerId: counterOffer.User_ID,
                SellerId: offer.User_ID,
                TokenCost: counterOffer.TokensOffered
            );

            var createdTrade = await tradeCreation.ExecuteAsync(context, ct);

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

        var finalCost = request.TokensOffered + Consts.CounterOfferCreationFee;

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

        if (request.Items.Any(x => x.ItemId <= 0))
            return Result<CounterOfferCostDto>.BadRequest("Niepoprawne ID przedmiotu");

        if (request.Items.Any(x => x.Quantity <= 0))
            return Result<CounterOfferCostDto>.BadRequest("Niepoprawna ilość");

        if (request.TokensOffered < 0)
            return Result<CounterOfferCostDto>.BadRequest("Niepoprawna ilość tokenów");

        if ((request.Items is null || request.Items.Count == 0) && request.TokensOffered <= 0)
            return Result<CounterOfferCostDto>.BadRequest("Kontroferta musi zawierać co najmniej jeden przedmiot lub tokeny.");

        var (user, userError) = await GetActiveUser<CounterOfferCostDto>(auth0UserId, ct);
        if (userError is not null)
            return userError;

        var offer = await repository.GetOfferAsync(offerId, ct);

        var offerValidation = ValidateOfferForCounterOffer<CounterOfferCostDto>(offer, user!.ID);
        if (offerValidation is not null)
            return offerValidation;

        var itemIds = request.Items.Select(x => x.ItemId).ToArray();
        if (itemIds.Length != itemIds.Distinct().Count())
            return Result<CounterOfferCostDto>.BadRequest("Przedmioty w kontrofercie muszą być unikalne");

        var allItemsExist = await repository.AllItemsExistAsync(itemIds, ct);
        if (!allItemsExist)
            return Result<CounterOfferCostDto>.BadRequest("Jeden z przedmiotów nie istnieje.");

        return null;
    }
}