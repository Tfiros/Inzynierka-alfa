using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ItemTradeApp.Features.Users.UserInfo;

public interface IUserInfoOfferRepository
{
    Task<(List<OfferListingDTO> offers, int totalCount)> GetActiveForUserByIdPagedAsync(
        int id,int page, int pageSize, CancellationToken ct = default);
    Task<(List<OfferListingDTO> offers, int totalCount)> GetHistoryForUserByIdPagedAsync(
        int id,int page, int pageSize, CancellationToken ct = default);
}

public class UserInfoOfferRepository(AppDbContext dbContext) : IUserInfoOfferRepository
{
    public async Task<(List<OfferListingDTO> offers, int totalCount)> GetActiveForUserByIdPagedAsync(
        int id,
        int page, 
        int pageSize,
        CancellationToken ct = default) 
    {

        var localQuery = dbContext.Offers.AsNoTracking().AsQueryable();
        localQuery = localQuery.Where(o => o.User.ID == id && o.OfferStatus_ID == (int)OfferStatuses.Active);

        var totalCount = await localQuery.CountAsync(ct);

        var offers = await localQuery.Skip((page - 1) * pageSize)
            .Take(pageSize).SelectOfferListingDto().ToListAsync(ct);
        return (offers, totalCount);

    }
    public async Task<(List<OfferListingDTO> offers, int totalCount)> GetHistoryForUserByIdPagedAsync(
        int id,
        int page, 
        int pageSize,
        CancellationToken ct = default) 
    {

        var localQuery = dbContext.Offers.AsNoTracking().AsQueryable();
        localQuery = localQuery.Where(o => o.User.ID == id && o.OfferStatus_ID == (int)OfferStatuses.Completed || o.OfferStatus_ID == (int)OfferStatuses.Canceled);

        var totalCount = await localQuery.CountAsync(ct);

        var offers = await localQuery.Skip((page - 1) * pageSize)
            .Take(pageSize).SelectOfferListingDto().ToListAsync(ct);
        return (offers, totalCount);

    }

}