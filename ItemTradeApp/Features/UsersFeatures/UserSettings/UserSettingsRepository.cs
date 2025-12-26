// Features/UsersFeature/UserSettings/UserSettingsRepository.cs
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.UsersFeature.UserSettings;
public interface IUserSettingsRepository
{
    Task<User?> GetUserByAuth0IdAsync(string auth0UserId, CancellationToken ct);

    Task UpdateUserAsync(User user, CancellationToken ct);
    Task<User?> GetUserWithProfileInfoByUserIdAsync(int id, CancellationToken ct);
}
public sealed class UserSettingsRepository(AppDbContext dbContext) : IUserSettingsRepository
{

    public async Task<User?> GetUserByAuth0IdAsync(string auth0UserId, CancellationToken ct)
    {
        return await dbContext.Users
            .SingleOrDefaultAsync(u => u.Auth0UserID == auth0UserId, ct);
    }

    public async Task UpdateUserAsync(User user, CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);
    }
    
    public async Task<User?> GetUserWithProfileInfoByUserIdAsync(int id, CancellationToken ct)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .Include(u => u.ProfileInfo)
            .SingleOrDefaultAsync(u => u.ID == id, ct);
        return user;
    }
}