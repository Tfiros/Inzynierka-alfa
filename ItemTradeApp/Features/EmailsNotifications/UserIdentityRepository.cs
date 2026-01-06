using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.EmailsNotifications;

public interface IUserIdentityRepository
{
    Task<int?> GetUserIdByAuth0IdAsync(string auth0UserId, CancellationToken ct);
    Task<User?> GetUserByIdAsync(int id, CancellationToken ct);
}

public sealed class UserIdentityRepository(AppDbContext db) : IUserIdentityRepository
{
    public async Task<int?> GetUserIdByAuth0IdAsync(string auth0UserId, CancellationToken ct)
    {
        return await db.Users
            .Where(u => u.Auth0UserID == auth0UserId)
            .Select(u => (int?)u.ID)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<User?> GetUserByIdAsync(int id, CancellationToken ct) =>
     await db.Users.Where(u => u.ID == id).FirstOrDefaultAsync(ct);

}