using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.UsersFeature.UserInfo;

public interface IUserInfoRepository
{
    Task<User> GetUserAsync(int id, CancellationToken ct);
    
}
public class UserInfoRepository(AppDbContext dbContext) : IUserInfoRepository
{
    public async Task<User> GetUserAsync(int id, CancellationToken ct)
    {
        var user = await dbContext.Users
            .Include(u => u.ProfileInfo)
            .SingleOrDefaultAsync(u => u.ID == id, ct);
        return user;
    }
}