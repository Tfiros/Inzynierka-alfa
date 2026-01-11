using ItemTradeApp.Features.Shared;
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
            .Take(pageSize).SelectOfferListingDto().ToListAsync(ct);
        return (offers, totalCount);

    }

}