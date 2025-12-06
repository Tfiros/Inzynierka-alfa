using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.UsersFeature.UserInfo;

public interface IUserInfoRepository
{
    Task<User?> GetUserWithProfileInfoByUserIdAsync(int id, CancellationToken ct);
    Task<User?> GetUserWithProfileByAuth0IdAsync(string authZeroUserId, CancellationToken ct);
    Task UpdateUserWithProfileInfoAsync(ProfileInfo profile, CancellationToken ct);


}
public class UserInfoRepository(AppDbContext dbContext) : IUserInfoRepository
{
    public async Task<User?> GetUserWithProfileInfoByUserIdAsync(int id, CancellationToken ct)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(u => u.ProfileInfo)
            .SingleOrDefaultAsync(u => u.ID == id, ct);
        return user;
    }

    public async Task<User?> GetUserWithProfileByAuth0IdAsync(string authZeroUserId, CancellationToken ct)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Include(u => u.ProfileInfo)
            .SingleOrDefaultAsync(u => u.Auth0UserID == authZeroUserId, ct);
    }

    public async Task UpdateUserWithProfileInfoAsync(ProfileInfo profile, CancellationToken ct)
    {
        dbContext.ProfileInfos.Update(profile);
        await dbContext.SaveChangesAsync(ct);
    }
}