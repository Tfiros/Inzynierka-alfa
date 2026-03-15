using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.CounterOffers;

public interface ICounterOffersService
{
    Task<Result<OfferInformationDTO>> GetOfferInfoAsync(string auth0UserId, int offerId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetSentCounterOffers(string auth0UserId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetRecivedCounterOffers(string auth0UserId, CancellationToken ct = default);

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
}

public class CounterOffersService(ICounterOffersRepository repository) : ICounterOffersService
{
    private const int CounterOfferCreationFee = 20;

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

    private async Task<Offer?> GetOfferAsync(int offerId, CancellationToken ct)
    {
        return await repository.GetOfferAsync(offerId, ct);
    }

    private static Result<T>? ValidateOfferForCounterOffer<T>(Offer? offer, int userId)
    {
        if (offer is null)
            return Result<T>.NotFound("Nie znaleziono oferty");

        if (offer.User_ID == userId)
            return Result<T>.BadRequest("Nie można złożyć kontroferty do swojej oferty");

        if (offer.OfferStatus_ID != 1)
            return Result<T>.BadRequest("Oferta nieaktywna");

        if (offer.ExpDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<T>.BadRequest("Oferta wygasła");

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

    private async Task<Result<AcceptCounterOfferResponse>?> ValidateAcceptCounterOffer(
        string auth0UserId,
        int counterOfferId,
        CancellationToken ct)
    {
        if (counterOfferId <= 0)
            return Result<AcceptCounterOfferResponse>.BadRequest("Niepoprawne ID KO");

        var (caller, userError) = await GetActiveUser<AcceptCounterOfferResponse>(auth0UserId, ct);
        if (userError is not null)
            return userError;

        var counterOffer = await repository.GetCounterOfferWithOfferAndItemsAsync(counterOfferId, ct);

        if (counterOffer is null)
            return Result<AcceptCounterOfferResponse>.NotFound("KO nie znalezione");

        var offer = counterOffer.Offer;
        if (offer is null)
            return Result<AcceptCounterOfferResponse>.NotFound("Oferta nie znaleziona");

        if (offer.User_ID != caller!.ID)
            return Result<AcceptCounterOfferResponse>.Forbidden("Nie jesteś właścicielem oferty");

        if (counterOffer.CounterOfferStatus_Id != 1)
            return Result<AcceptCounterOfferResponse>.BadRequest("Niepoprawny status kontroferty");

        if (offer.OfferStatus_ID != 1)
            return Result<AcceptCounterOfferResponse>.BadRequest("Oferta nie jest aktywna");

        if (offer.ExpDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<AcceptCounterOfferResponse>.BadRequest("Oferta jest przeterminowana");

        var tradeExists = await repository.TradeExistsForOfferAsync(offer.ID, ct);
        if (tradeExists)
            return Result<AcceptCounterOfferResponse>.Conflict("Trade już istnieje");

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

        return null;
    }

    public async Task<Result<OfferInformationDTO>> GetOfferInfoAsync(
        string auth0UserId,
        int offerId,
        CancellationToken ct = default)
    {
        if (offerId <= 0)
            return Result<OfferInformationDTO>.BadRequest("Niepoprawne ID oferty.");

        var (user, userError) = await GetActiveUser<OfferInformationDTO>(auth0UserId, ct);
        if (userError is not null)
            return userError;

        var offer = await GetOfferAsync(offerId, ct);

        var offerError = ValidateOfferForCounterOffer<OfferInformationDTO>(offer, user!.ID);
        if (offerError is not null)
            return offerError;

        var items = await repository.GetOfferListingItemsAsync(offerId, ct);

        var dto = new OfferInformationDTO(
            OfferId: offer!.ID,
            OwnerId: offer.User_ID,
            Title: offer.Title,
            Description: offer.Description,
            TokenCost: offer.TokenCost,
            ExpDate: offer.ExpDate,
            OfferStatusId: offer.OfferStatus_ID,
            CreationDate: offer.CreationDate,
            Items: items
        );

        return Result<OfferInformationDTO>.Success(dto);
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

    public async Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetRecivedCounterOffers(
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

        var itemIds = request.Items.Select(x => x.ItemId).Distinct().ToArray();
        var allItemsExist = await repository.AllItemsExistAsync(itemIds, ct);
        if (!allItemsExist)
            return Result<CounterOfferDto>.BadRequest("Jeden z przedmiotów nie istnieje.");

        var totalToCharge = request.TokensOffered + CounterOfferCreationFee;

        if (totalToCharge > 0)
        {
            if (user.Tokens < totalToCharge)
                return Result<CounterOfferDto>.BadRequest("Za mało tokenów");

            user.Tokens -= totalToCharge;
        }

        await using var transaction = await repository.BeginTransactionAsync(ct);

        try
        {
            var counterOffer = new CounterOffer
            {
                User_ID = user.ID,
                Offer_Id = offerId,
                CreationDate = DateTime.UtcNow,
                TokensOffered = request.TokensOffered,
                CounterOfferStatus_Id = 1,
                ListingCounterOfferItems = request.Items
                    .Select(x => new ListingCounterOfferItem
                    {
                        Item_ID = x.ItemId,
                        Quantity = x.Quantity
                    })
                    .ToList()
            };

            repository.AddCounterOffer(counterOffer);

            await repository.SaveChangesAsync(ct);
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

        if (statusId is not (2 or 3))
            return Result<CounterOfferDto>.BadRequest("Niepoprawny status");

        var (user, userError) = await GetActiveUser<CounterOfferDto>(auth0UserId, ct);
        if (userError is not null)
            return userError;

        var counterOffer = await repository.GetCounterOfferWithOfferAndItemsAsync(counterOfferId, ct);
        if (counterOffer is null)
            return Result<CounterOfferDto>.NotFound("Nie znaleziono kontroferty");

        var offerOwnerId = await repository.GetOfferOwnerIdAsync(counterOffer.Offer_Id, ct);
        if (offerOwnerId != user!.ID)
            return Result<CounterOfferDto>.Unauthorized();

        if (counterOffer.CounterOfferStatus_Id != 1)
            return Result<CounterOfferDto>.BadRequest("Kontroferta nie jest pending");

        await using var transaction = await repository.BeginTransactionAsync(ct);

        try
        {
            counterOffer.CounterOfferStatus_Id = statusId;

            if (statusId == 3 && counterOffer.TokensOffered > 0)
            {
                var sender = await repository.GetUserEntityByIdAsync(counterOffer.User_ID, ct);
                if (sender is not null && !sender.IsDeleted)
                {
                    sender.Tokens += counterOffer.TokensOffered;
                }
            }

            await repository.SaveChangesAsync(ct);
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
        var validationResult = await ValidateAcceptCounterOffer(auth0UserId, counterOfferId, ct);
        if (validationResult is not null)
            return validationResult;

        await using var transaction = await repository.BeginTransactionAsync(ct);

        try
        {
            var counterOffer = await repository.GetCounterOfferWithOfferAndItemsAsync(counterOfferId, ct);
            if (counterOffer is null || counterOffer.Offer is null)
                return Result<AcceptCounterOfferResponse>.NotFound("KO nie znalezione");

            var offer = counterOffer.Offer;

            counterOffer.CounterOfferStatus_Id = 2;
            offer.OfferStatus_ID = (int)OfferStatuses.InRealization;

            var trade = new Trade
            {
                Offer_ID = offer.ID,
                Customer_ID = counterOffer.User_ID,
                User_ID = offer.User_ID,
                TokenCost = counterOffer.TokensOffered,
                CreationDate = DateTime.UtcNow,
                CompletitionDate = null,
                TradeStatus_ID = (int)TradeStatuses.New,
                MiddlemanUser_ID = null,
                HasBuyersItems = false,
                HasSellersItems = false,
            };

            repository.AddTrade(trade);

            await repository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Result<AcceptCounterOfferResponse>.Success(
                new AcceptCounterOfferResponse(
                    TradeId: trade.ID,
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
}