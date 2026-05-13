using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Trades.Repositories;
public interface IUserRepository
{
    Task<User?> GetByAuth0UserIdAsync(string auth0UserId, CancellationToken ct);
    Task<User?> GetByIdAsync(int id, CancellationToken ct);


}
public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<User?> GetByAuth0UserIdAsync(string auth0UserId, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Auth0UserID == auth0UserId, ct);

    public async Task<User?> GetByIdAsync(int id, CancellationToken ct) =>
       await db.Users.FirstOrDefaultAsync(u => u.ID == id, ct);
}