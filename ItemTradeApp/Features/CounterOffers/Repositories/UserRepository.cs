using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.CounterOffers.Repositories;

public interface IUserRepository
{
    Task<User?> GetUserInfo(string auth0UserId, CancellationToken ct);
    Task<User?> GetUserEntityByIdAsync(int userId, CancellationToken ct);
}

public sealed class UserRepository(AppDbContext db):IUserRepository
{
    public async Task<User?> GetUserInfo(string auth0UserId, CancellationToken ct)
    {
        return await db.Users
            .FirstOrDefaultAsync(u => u.Auth0UserID == auth0UserId && !u.IsDeleted, ct);
    }

    public async Task<User?> GetUserEntityByIdAsync(int userId, CancellationToken ct)
    {
        return await db.Users
            .FirstOrDefaultAsync(u => u.ID == userId && !u.IsDeleted, ct);
    }
}