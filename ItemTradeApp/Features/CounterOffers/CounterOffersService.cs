using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.CounterOffers;

public interface ICounterOffersService
{
    Task<Result<OfferInformationDTO>> GetOfferInfoAsync(string auth0UserId, int offerId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetSentCounterOffers(int userId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetRecivedCounterOffers(int userId, CancellationToken ct = default);

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


public class CounterOffersService : ICounterOffersService
{
    private readonly AppDbContext db;
    private const int CounterOfferCreationFee = 20;

    public CounterOffersService(AppDbContext db)
    {
        this.db = db ?? throw new ArgumentNullException(nameof(db));
    }

    private async Task<(int Id, bool IsDeleted)?> GetUserAsync(string auth0UserId, CancellationToken ct)
    {
        return await db.Users
            .AsNoTracking()
            .Where(u => u.Auth0UserID == auth0UserId)
            .Select(u => new ValueTuple<int, bool>(u.ID, u.IsDeleted))
            .FirstOrDefaultAsync(ct);
    }
    
    private async Task<Result<AcceptCounterOfferResponse>> ValidateAcceptCounterOffer(
    string auth0UserId,
    int counterOfferId,
    CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(auth0UserId))
        return Result<AcceptCounterOfferResponse>.Unauthorized("missing_sub_claim");

    if (counterOfferId <= 0)
        return Result<AcceptCounterOfferResponse>.BadRequest("Niepoprawne ID KO");
    
    var caller = await db.Users
        .FirstOrDefaultAsync(u => u.Auth0UserID == auth0UserId, ct);

    if (caller is null)
        return Result<AcceptCounterOfferResponse>.Unauthorized("Nie znaleziono użytkownika");
    if (caller.IsDeleted)
        return Result<AcceptCounterOfferResponse>.Unauthorized("Użytkownik nie istnieje");

    var counterOffer = await db.CounterOffers
        .Include(co => co.Offer)
        .Include(co => co.ListingCounterOfferItems)
        .FirstOrDefaultAsync(co => co.ID == counterOfferId, ct);

    if (counterOffer is null)
        return Result<AcceptCounterOfferResponse>.NotFound("KO nie znalezione");

    var offer = counterOffer.Offer;
    if (offer is null)
        return Result<AcceptCounterOfferResponse>.NotFound("Oferta nie znaleziona");

    if (offer.User_ID != caller.ID)
        return Result<AcceptCounterOfferResponse>.Forbidden("Nie właściciel ofert");

    if (counterOffer.CounterOfferStatus_Id != 1) 
        return Result<AcceptCounterOfferResponse>.BadRequest("Niepoprawny status oferty");

    if (offer.OfferStatus_ID != 1) 
        return Result<AcceptCounterOfferResponse>.BadRequest("Oferta nie aktywna");

    if (offer.ExpDate < DateOnly.FromDateTime(DateTime.UtcNow))
        return Result<AcceptCounterOfferResponse>.BadRequest("Oferta przeterminowana");

    var tradeExists = await db.Trades
        .AsNoTracking()
        .AnyAsync(t => t.Offer_ID == offer.ID && t.TradeStatus_ID != (int)TradeStatuses.Failed, ct);

    if (tradeExists)
        return Result<AcceptCounterOfferResponse>.Conflict("Trade już istnieje");

    return null;
}

    private async Task<Offer?> GetOfferAsync(int offerId, CancellationToken ct)
    {
        return await db.Offers
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.ID == offerId, ct);
    }

    private static Result<T>? ValidateOfferForCounterOffer<T>(Offer offer, int userId)
    {
        if (offer.User_ID == userId)
            return Result<T>.BadRequest("Nie możesz złożyć kontroferty na własną ofertę.");

        if (offer.OfferStatus_ID != 1)
            return Result<T>.BadRequest("Oferta nie jest aktywna.");

        if (offer.ExpDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<T>.BadRequest("Oferta wygasła.");

        return null;
    }

    public async Task<Result<OfferInformationDTO>> GetOfferInfoAsync(
        string auth0UserId,
        int offerId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<OfferInformationDTO>.Unauthorized("Brak ID użytkownika.");

        if (offerId <= 0)
            return Result<OfferInformationDTO>.BadRequest("Niepoprawne ID oferty.");

        var user = await GetUserAsync(auth0UserId, ct);
        if (user is null) return Result<OfferInformationDTO>.Unauthorized("Nie znaleziono użytkownika.");
        if (user.Value.IsDeleted) return Result<OfferInformationDTO>.Unauthorized("Konto użytkownika jest usunięte.");

        var offer = await GetOfferAsync(offerId, ct);
        if (offer is null) return Result<OfferInformationDTO>.NotFound("Oferta nie istnieje.");

        var offerError = ValidateOfferForCounterOffer<OfferInformationDTO>(offer, user.Value.Id);
        if (offerError is not null) return offerError;

        var items = await db.ListingItems
            .AsNoTracking()
            .Where(li => li.Offer_ID == offerId)
            .Include(li => li.Item).ThenInclude(i => i.Game).ThenInclude(g => g.Genre)
            .Include(li => li.Item).ThenInclude(i => i.ItemRarity)
            .Select(li => new OfferListingItemDTO(
                new ItemDTO(
                    li.Item.ID,
                    li.Item.Name,
                    li.Item.Photo_URL,
                    li.Item.EstimatedTokenValue,
                    new GameDTO(
                        li.Item.Game_ID,
                        li.Item.Game.Name,
                        li.Item.Game.Photo_URL,
                        li.Item.Game.Genre_ID
                    )
                ),
                li.Quantity,
                li.Item.Game.Genre_ID,
                li.Item.Game.Genre.Name,
                li.Item.ItemRarityId,
                li.Item.ItemRarity.RarityName,
                li.IsWanted
            ))
            .ToListAsync(ct);

        var dto = new OfferInformationDTO(
            OfferId: offer.ID,
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
        int userId,
        CancellationToken ct = default)
    {
        var counterOffers = await db.CounterOffers
            .AsNoTracking()
            .Where(co => co.User_ID == userId)
            .Include(co => co.Offer)
            .Include(co => co.OfferStatus)
            .Include(co => co.ListingCounterOfferItems)
            .ThenInclude(i => i.Item)
            .ThenInclude(it => it.Game)
            .ToListAsync(ct);

        if (counterOffers.Count == 0)
            return Result<IReadOnlyList<CounterOfferListItemDto>>.Success(Array.Empty<CounterOfferListItemDto>());

        var ownerIds = counterOffers.Select(x => x.Offer.User_ID).Distinct().ToArray();

        var ownerNickByUserId = await db.ProfileInfos
            .AsNoTracking()
            .Where(p => ownerIds.Contains(p.User_ID))
            .Select(p => new { p.User_ID, p.Nickname })
            .ToDictionaryAsync(x => x.User_ID, x => x.Nickname ?? "", ct);

        var dtos = counterOffers.Select(counterOffer =>
        {
            ownerNickByUserId.TryGetValue(counterOffer.Offer.User_ID, out var ownerNick);

            return new CounterOfferListItemDto(
                CounterOfferId: counterOffer.ID,
                OfferId: counterOffer.Offer_Id,
                OfferTitle: counterOffer.Offer?.Title ?? "",
                OfferOwnerUserId: counterOffer.Offer?.User_ID ?? 0,

                CounterOfferUserId: counterOffer.User_ID,
                CounterOfferUserNickname: ownerNick ?? "",

                CreationDate: counterOffer.CreationDate,
                TokensOffered: counterOffer.TokensOffered,
                StatusId: counterOffer.CounterOfferStatus_Id,
                StatusName: counterOffer.OfferStatus?.StatusName ?? "",

                Items: counterOffer.ListingCounterOfferItems
                    .Select(i => new CounterOfferItemsDto(
                        i.Item_ID,
                        i.Item?.Name ?? "",
                        i.Item?.Photo_URL ?? "",
                        i.Item?.Game_ID ?? 0,
                        i.Item?.Game?.Name ?? "",
                        i.Quantity
                    ))
                    .ToList()
            );
        }).ToList();

        return Result<IReadOnlyList<CounterOfferListItemDto>>.Success(dtos);
    }


    public async Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetRecivedCounterOffers(
        int userId,
        CancellationToken ct = default)
    {
        var counterOffers = await db.CounterOffers
            .AsNoTracking()
            .Where(co => co.Offer.User_ID == userId)
            .Include(co => co.Offer)
            .Include(co => co.OfferStatus)
            .Include(co => co.ListingCounterOfferItems)
            .ThenInclude(i => i.Item)
            .ThenInclude(it => it.Game)
            .ToListAsync(ct);

        if (counterOffers.Count == 0)
            return Result<IReadOnlyList<CounterOfferListItemDto>>.Success(Array.Empty<CounterOfferListItemDto>());

        var senderIds = counterOffers.Select(x => x.User_ID).Distinct().ToArray();

        var senderNickByUserId = await db.ProfileInfos
            .AsNoTracking()
            .Where(p => senderIds.Contains(p.User_ID))
            .Select(p => new { p.User_ID, p.Nickname })
            .ToDictionaryAsync(x => x.User_ID, x => x.Nickname ?? "", ct);

        var dtos = counterOffers.Select(counterOffer =>
        {
            senderNickByUserId.TryGetValue(counterOffer.User_ID, out var senderNickname);

            return new CounterOfferListItemDto(
                CounterOfferId: counterOffer.ID,
                OfferId: counterOffer.Offer_Id,
                OfferTitle: counterOffer.Offer?.Title ?? "",
                OfferOwnerUserId: counterOffer.Offer?.User_ID ?? 0,

                CounterOfferUserId: counterOffer.User_ID,
                CounterOfferUserNickname: senderNickname ?? "",

                CreationDate: counterOffer.CreationDate,
                TokensOffered: counterOffer.TokensOffered,
                StatusId: counterOffer.CounterOfferStatus_Id,
                StatusName: counterOffer.OfferStatus?.StatusName ?? "",

                Items: counterOffer.ListingCounterOfferItems
                    .Select(i => new CounterOfferItemsDto(
                        i.Item_ID,
                        i.Item?.Name ?? "",
                        i.Item?.Photo_URL ?? "",
                        i.Item?.Game_ID ?? 0,
                        i.Item?.Game?.Name ?? "",
                        i.Quantity
                    ))
                    .ToList()
            );
        }).ToList();

        return Result<IReadOnlyList<CounterOfferListItemDto>>.Success(dtos);
    }


    public async Task<Result<CounterOfferDto>> CreateCounterOfferAsync(
        string auth0UserId,
        int offerId,
        CounterOfferDraftRequest request,
        CancellationToken ct = default)
    {
        var reqValidation = ValidateCreateRequest(auth0UserId, offerId, request);
        if (reqValidation is not null) return reqValidation;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Auth0UserID == auth0UserId, ct);

        if (user is null)
            return Result<CounterOfferDto>.Unauthorized("Nie znaleziono użytkownika");
        if (user.IsDeleted)
            return Result<CounterOfferDto>.Unauthorized("Użytkownik nie istnije");

        var offer = await db.Offers
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.ID == offerId, ct);

        var offerValidation = ValidateOffer(offer, user);
        if (offerValidation is not null) return offerValidation;

        var itemIds = request.Items.Select(x => x.ItemId).Distinct().ToArray();
        var existingCount = await db.Items.CountAsync(i => itemIds.Contains(i.ID), ct);
        if (existingCount != itemIds.Length)
            return Result<CounterOfferDto>.BadRequest("Jeden z przedmiotów nie istnieje.");

        var totalToCharge = request.TokensOffered + CounterOfferCreationFee;

        if (totalToCharge > 0)
        {
            if (user.Tokens < totalToCharge)
                return Result<CounterOfferDto>.BadRequest("Za mało tokenów");

            user.Tokens -= totalToCharge;
        }

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

            db.CounterOffers.Add(counterOffer);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var dto = new CounterOfferDto
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

            return Result<CounterOfferDto>.Created(dto);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            return Result<CounterOfferDto>.InternalServerError("Stworzenie kontroferty nie powiodło się");
        }
    }

    private static Result<CounterOfferDto>? ValidateCreateRequest(
        string auth0UserId,
        int offerId,
        CounterOfferDraftRequest request)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<CounterOfferDto>.Unauthorized("missing_sub_claim");
        if (offerId <= 0)
            return Result<CounterOfferDto>.BadRequest("Niepoprawne id Oferty");
        if (request.Items.Any(x => x.ItemId <= 0))
            return Result<CounterOfferDto>.BadRequest("Niepoprawne ID przedmiotu");
        if (request.Items.Any(x => x.Quantity <= 0))
            return Result<CounterOfferDto>.BadRequest("Niepoprawna jakość");
        if (request.TokensOffered < 0)
            return Result<CounterOfferDto>.BadRequest("Niepoprawna ilość tokenó∑");

        return null;
    }

    private static Result<CounterOfferDto>? ValidateOffer(
        Offer? offer,
        User user)
    {
        if (offer is null)
            return Result<CounterOfferDto>.NotFound("Nie znaleziono oferty");

        if (offer.User_ID == user.ID)
            return Result<CounterOfferDto>.BadRequest("Nie można stworzyć kontroferty do swojej oferty");

        if (offer.OfferStatus_ID != 1)
            return Result<CounterOfferDto>.BadRequest("Ofert nie aktywna");

        if (offer.ExpDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<CounterOfferDto>.BadRequest("Oferta wygasła");

        return null;
    }

    public async Task<Result<CounterOfferDto>> UpdateCounterOfferStatusAsync(
        string auth0UserId,
        int counterOfferId,
        int statusId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<CounterOfferDto>.Unauthorized("missing_sub_claim");

        if (counterOfferId <= 0)
            return Result<CounterOfferDto>.BadRequest("Niepoprawne ID kontroferty");

        if (statusId is not (2 or 3))
            return Result<CounterOfferDto>.BadRequest("Niepoprawny status");

        var user = await db.Users
            .AsNoTracking()
            .Select(u => new { u.ID, u.Auth0UserID, u.IsDeleted })
            .FirstOrDefaultAsync(u => u.Auth0UserID == auth0UserId, ct);

        if (user is null)
            return Result<CounterOfferDto>.Unauthorized("user_not_found");

        if (user.IsDeleted)
            return Result<CounterOfferDto>.Unauthorized("user_deleted");

        var counterOffer = await db.CounterOffers
            .Include(co => co.ListingCounterOfferItems)
            .FirstOrDefaultAsync(co => co.ID == counterOfferId, ct);

        if (counterOffer is null)
            return Result<CounterOfferDto>.NotFound("Nie znaleziono kontrofrty");

        var offerOwnerId = await db.Offers
            .AsNoTracking()
            .Where(o => o.ID == counterOffer.Offer_Id)
            .Select(o => (int?)o.User_ID)
            .FirstOrDefaultAsync(ct);

        if (offerOwnerId != user.ID)
            return Result<CounterOfferDto>.Unauthorized();

        if (counterOffer.CounterOfferStatus_Id != 1)
            return Result<CounterOfferDto>.BadRequest("Ofert nie jest pending");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        counterOffer.CounterOfferStatus_Id = statusId;

        if (statusId == 3 && counterOffer.TokensOffered > 0)
        {
            var sender = await db.Users.FirstOrDefaultAsync(u => u.ID == counterOffer.User_ID, ct);
            if (sender is not null && !sender.IsDeleted)
            {
                sender.Tokens += counterOffer.TokensOffered;
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);


        await db.SaveChangesAsync(ct);

        var dto = new CounterOfferDto
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

        return Result<CounterOfferDto>.Success(dto);
    }

    public async Task<Result<AcceptCounterOfferResponse>> AcceptCounterOfferAsync(
        string auth0UserId,
        int counterOfferId,
        CancellationToken ct = default)
    {
        var validationResult = await ValidateAcceptCounterOffer(auth0UserId, counterOfferId, ct);
        if (validationResult != null)
            return validationResult;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var counterOffer = await db.CounterOffers
                .Include(co => co.Offer)
                .Include(co => co.ListingCounterOfferItems)
                .FirstOrDefaultAsync(co => co.ID == counterOfferId, ct);
        
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

            db.Trades.Add(trade);

            await db.SaveChangesAsync(ct);
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

