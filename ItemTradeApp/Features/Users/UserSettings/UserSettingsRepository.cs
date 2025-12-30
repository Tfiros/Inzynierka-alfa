using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Users.UserSettings;
public interface IUserSettingsRepository
{
    Task<User?> GetUserByAuth0IdAsync(string auth0UserId, CancellationToken ct);
    Task UpdateUserAsync(User user, CancellationToken ct);
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
}