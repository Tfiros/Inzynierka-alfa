using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.CounterOffers;

public interface ICounterOffersService
{
    Task<Result<OfferInformationDTO>> GetOfferInfo(int offerId, string currentUserId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetSentCounterOffers(int userId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetRecivedCounterOffers(int userId, CancellationToken ct = default);
}


public class CounterOffersService : ICounterOffersService
{
    private readonly AppDbContext db;

    public CounterOffersService(AppDbContext db)
    {
        this.db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Result<OfferInformationDTO>> GetOfferInfo(int offerId, string currentUserId,
        CancellationToken ct = default)
    {
        var offer = await db.Offers
            .AsNoTracking()
            .FirstOrDefaultAsync(offer => offer.ID == offerId, ct);

        if (offer is null)
        {
            return Result<OfferInformationDTO>.BadRequest("Offer not found");
        }

        if (offer.User_ID.ToString() == currentUserId)
        {
            return Result<OfferInformationDTO>.BadRequest("User can't counter his own offer");
        }

        if (offer.ExpDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return Result<OfferInformationDTO>.BadRequest("Offer expired");
        }

        if (offer.OfferStatus_ID != 1)
            return Result<OfferInformationDTO>.BadRequest("Offer is no longer Active");

        var items = await db.ListingItems
            .AsNoTracking()
            .Where(li => li.Offer_ID == offerId)
            .Include(li => li.Item)
            .ThenInclude(i => i.Game)
            .ThenInclude(g => g.Genre)
            .Select(li => new OfferListingItemDTO(
                li.Item_ID,
                li.Item.Name,
                li.Item.Game_ID,
                li.Item.Photo_URL,
                li.Quantity,
                li.Item.Game.Name,
                li.Item.Game.Genre_ID,
                li.Item.Game.Genre.Name
            ))
            .ToListAsync(ct);


        var dto = new OfferInformationDTO(
            OfferId: offer.ID,
            OwnerId: offer.User_ID,
            Title: offer.title,
            Description: offer.description,
            TokenCost: offer.TokenCost,
            ExpDate: offer.ExpDate,
            OfferStatusId: offer.OfferStatus_ID,
            CreationDate: offer.CreationDate,
            Items: items
        );

        return Result<OfferInformationDTO>.Success(dto);
    }

    //To test might be needed to be changed
    public async Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetSentCounterOffers(
        int userId,
        CancellationToken ct = default)
    {
        var counterOffers = await db.CounterOffers
            .AsNoTracking()
            .Where(counterOffer => counterOffer.User_ID == userId)
            .Select(counterOffer => new
            {
                counterOffer,
                Offer = db.Offers
                    .Where(offer => offer.ID == counterOffer.Offer_Id)
                    .Select(offer => new { offer.ID, offer.title, offer.User_ID })
                    .FirstOrDefault(),
                Status = db.CounterOfferStatuses
                    .Where(s => s.ID == counterOffer.CounterOfferStatus_Id)
                    .Select(s => new { s.ID, s.StatusName })
                    .FirstOrDefault(),
                Nick = db.ProfileInfos
                    .Where(p => p.User_ID == counterOffer.User_ID)
                    .Select(p => p.Nickname)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        if (counterOffers.Count == 0)
            return Result<IReadOnlyList<CounterOfferListItemDto>>
                .Success(Array.Empty<CounterOfferListItemDto>());

        var ids = counterOffers.Select(x => x.counterOffer.ID).ToArray();

        var items = await db.ListingCounterOfferItems
            .AsNoTracking()
            .Where(i => ids.Contains(i.CounterOffers_ID))
            .Include(i => i.Item)
            .ThenInclude(it => it.Game)
            .Select(i => new
            {
                CounterOfferId = i.CounterOffers_ID,
                Item = new CounterOfferItemsDto(
                    i.Item_ID,
                    i.Item.Name,
                    i.Item.Photo_URL,
                    i.Item.Game_ID,
                    i.Item.Game.Name,
                    i.Quantity
                )
            })
            .ToListAsync(ct);

        var itemsByOffer = new Dictionary<int, List<CounterOfferItemsDto>>();

        foreach (var item in items)
        {
            if (!itemsByOffer.TryGetValue(item.CounterOfferId, out var list))
            {
                list = new List<CounterOfferItemsDto>();
                itemsByOffer[item.CounterOfferId] = list;
            }

            list.Add(item.Item);
        }


        var dtos = new List<CounterOfferListItemDto>(counterOffers.Count);

        foreach (var x in counterOffers)
        {
            if (!itemsByOffer.TryGetValue(x.counterOffer.ID, out var itemsForThisOffer))
            {
                itemsForThisOffer = new List<CounterOfferItemsDto>();
            }

            dtos.Add(new CounterOfferListItemDto(
                CounterOfferId: x.counterOffer.ID,
                OfferId: x.counterOffer.Offer_Id,
                OfferTitle: x.Offer?.title ?? "",
                OfferOwnerUserId: x.Offer?.User_ID ?? 0,

                CounterOfferUserId: x.counterOffer.User_ID,
                CounterOfferUserNickname: x.Nick,

                CreationDate: x.counterOffer.CreationDate,
                TokensOffered: x.counterOffer.TokensOffered,
                StatusId: x.Status?.ID ?? x.counterOffer.CounterOfferStatus_Id,
                StatusName: x.Status?.StatusName ?? "",

                Items: itemsForThisOffer
            ));
        }


        return Result<IReadOnlyList<CounterOfferListItemDto>>.Success(dtos);
    }

    
    public async Task<Result<IReadOnlyList<CounterOfferListItemDto>>> GetRecivedCounterOffers(
        int userId,
        CancellationToken ct = default)
    {
        var counterOffers = await db.CounterOffers
            .AsNoTracking()
            .Where(counterOffer =>
                db.Offers.Any(o =>
                    o.ID == counterOffer.Offer_Id &&
                    o.User_ID == userId
                )
            )
            .Select(counterOffer => new
            {
                counterOffer,
                Offer = db.Offers
                    .Where(offer => offer.ID == counterOffer.Offer_Id)
                    .Select(offer => new { offer.ID, offer.title, offer.User_ID })
                    .FirstOrDefault(),
                Status = db.CounterOfferStatuses
                    .Where(s => s.ID == counterOffer.CounterOfferStatus_Id)
                    .Select(s => new { s.ID, s.StatusName })
                    .FirstOrDefault(),
                Nick = db.ProfileInfos
                    .Where(p => p.User_ID == counterOffer.User_ID)
                    .Select(p => p.Nickname)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        if (counterOffers.Count == 0)
            return Result<IReadOnlyList<CounterOfferListItemDto>>
                .Success(Array.Empty<CounterOfferListItemDto>());

        var ids = counterOffers.Select(x => x.counterOffer.ID).ToArray();

        var items = await db.ListingCounterOfferItems
            .AsNoTracking()
            .Where(i => ids.Contains(i.CounterOffers_ID))
            .Include(i => i.Item)
            .ThenInclude(it => it.Game)
            .Select(i => new
            {
                CounterOfferId = i.CounterOffers_ID,
                Item = new CounterOfferItemsDto(
                    i.Item_ID,
                    i.Item.Name,
                    i.Item.Photo_URL,
                    i.Item.Game_ID,
                    i.Item.Game.Name,
                    i.Quantity
                )
            })
            .ToListAsync(ct);

        var itemsByOffer = new Dictionary<int, List<CounterOfferItemsDto>>();

        foreach (var item in items)
        {
            if (!itemsByOffer.TryGetValue(item.CounterOfferId, out var list))
            {
                list = new List<CounterOfferItemsDto>();
                itemsByOffer[item.CounterOfferId] = list;
            }

            list.Add(item.Item);
        }

        var dtos = new List<CounterOfferListItemDto>(counterOffers.Count);

        foreach (var x in counterOffers)
        {
            if (!itemsByOffer.TryGetValue(x.counterOffer.ID, out var itemsForThisOffer))
            {
                itemsForThisOffer = new List<CounterOfferItemsDto>();
            }

            dtos.Add(new CounterOfferListItemDto(
                CounterOfferId: x.counterOffer.ID,
                OfferId: x.counterOffer.Offer_Id,
                OfferTitle: x.Offer?.title ?? "",
                OfferOwnerUserId: x.Offer?.User_ID ?? 0,

                CounterOfferUserId: x.counterOffer.User_ID,
                CounterOfferUserNickname: x.Nick,

                CreationDate: x.counterOffer.CreationDate,
                TokensOffered: x.counterOffer.TokensOffered,
                StatusId: x.Status?.ID ?? x.counterOffer.CounterOfferStatus_Id,
                StatusName: x.Status?.StatusName ?? "",

                Items: itemsForThisOffer
            ));
        }

        return Result<IReadOnlyList<CounterOfferListItemDto>>.Success(dtos);
    }
}
