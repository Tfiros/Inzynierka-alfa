using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.CounterOffers.Repositories;

public interface IUserRepository
{
    Task<User?> GetUserInfo(string auth0UserId, CancellationToken ct);
    Task<User?> GetUserEntityByIdAsync(int userId, CancellationToken ct);
    Task<bool> TrySubtractTokenCostAsync(int userId, int amount, CancellationToken ct);
}

public sealed class UserRepository(AppDbContext db):IUserRepository
{
    public async Task<User?> GetUserInfo(string auth0UserId, CancellationToken ct)
    {
        return await db.Users
            .Include(u => u.ProfileInfo)
            .FirstOrDefaultAsync(u => u.Auth0UserID == auth0UserId && !u.IsDeleted, ct);
    }

    public async Task<User?> GetUserEntityByIdAsync(int userId, CancellationToken ct)
    {
        return await db.Users
            .FirstOrDefaultAsync(u => u.ID == userId && !u.IsDeleted, ct);
    }

    public async Task<bool> TrySubtractTokenCostAsync(int userId, int amount, CancellationToken ct)
        => await db.Users.Where(u => u.ID == userId && !u.IsDeleted && u.Tokens >= amount)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Tokens,u => u.Tokens - amount), ct) == 1;
}