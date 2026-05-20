using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Favourites.Repositories;

public interface IUserRepository
{
    Task<int?> GetUserIdByAuth0IdAsync(string auth0UserId, CancellationToken ct = default);
}

public class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public Task<int?> GetUserIdByAuth0IdAsync(string auth0UserId, CancellationToken ct = default)
        => dbContext.Users
            .AsNoTracking()
            .Where(u => u.Auth0UserID == auth0UserId)
            .Select(u => (int?)u.ID)
            .FirstOrDefaultAsync(ct);
}