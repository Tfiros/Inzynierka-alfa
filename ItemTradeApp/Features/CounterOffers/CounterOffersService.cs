using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.CounterOffers;

public interface ICounterOffersService
{
    Task<Result<OfferInformationDTO>> GetOfferInfo(int offerId, string currentUserId, CancellationToken ct = default);
}

public class CounterOffersService:ICounterOffersService
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
}