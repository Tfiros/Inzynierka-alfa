using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ItemTradeApp.Features.Users.UserInfo;

public interface IUserInfoOfferRepository
{
    Task<(List<OfferListingDTO> offers, int totalCount)> GetForUserByIdPagedAsync(
        int id,int page, int pageSize, CancellationToken ct = default);
}

public class UserInfoOfferRepository(AppDbContext dbContext) : IUserInfoOfferRepository
{
    public async Task<(List<OfferListingDTO> offers, int totalCount)> GetForUserByIdPagedAsync(
        int id,
        int page, 
        int pageSize,
        CancellationToken ct = default) 
    {

        var localQuery = dbContext.Offers.AsNoTracking().AsQueryable();
        localQuery = localQuery.Where(o => o.User.ID == id);

        var totalCount = await localQuery.CountAsync(ct);

        var offers = await localQuery.Skip((page - 1) * pageSize)
            .Take(pageSize).Select(o => new OfferListingDTO
            (new OfferCoreDTO(o.ID,o.Title,o.Description,o.ExpDate,o.CreationDate,o.TokenCost,o.OfferStatus.ID),
              new OfferUserDTO
                    (
                        o.User_ID,
                        o.User.ProfileInfo!.Nickname,
                        o.User.ProfileInfo!.ImageUrl
                    ),
              o.ListingItems.Where(li=>!li.IsWanted).OrderByDescending(li => li.Item.EstimatedTokenValue).Take(3).Select(li => 
                    new OfferListingItemDTO
                        (
                            li.Item.ID,
                            li.Item.Name,
                            li.Item.Game.ID,
                            li.Item.Photo_URL,
                            li.Quantity,
                            li.Item.Game.Name,
                            li.Item.Game.Genre.ID,
                            li.Item.Game.Genre.Name,
                            li.Item.ItemRarity.ID,
                            li.Item.ItemRarity.RarityName
                        )).ToList(),
              o.ListingItems.Where(li=>li.IsWanted).OrderByDescending(li => li.Item.EstimatedTokenValue).Take(3).Select(li => 
                  new OfferListingItemDTO
                  (
                      li.Item.ID,
                      li.Item.Name,
                      li.Item.Game.ID,
                      li.Item.Photo_URL,
                      li.Quantity,
                      li.Item.Game.Name,
                      li.Item.Game.Genre.ID,
                      li.Item.Game.Genre.Name,
                      li.Item.ItemRarity.ID,
                      li.Item.ItemRarity.RarityName
                  )).ToList(),
              o.ListingItems.Count(li => !li.IsWanted),
              o.ListingItems.Count(li => li.IsWanted)
            )).ToListAsync(ct);
        return (offers, totalCount);

    }

}