using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Trades.Repositories;
public interface IUserRepository
{
    Task<User?> GetByAuth0UserIdAsync(string auth0UserId, CancellationToken ct);
    Task<User?> GetByIdAsync(int id, CancellationToken ct);
    Task<bool> TryEscrowTokensAsync(int fromUserId, int toUserId, int amount, CancellationToken ct);
    Task<bool> TryReleaseTokensAsync(int userId, int amount, CancellationToken ct);
    Task<bool> TryRefundTokensAsync(int escrowHolderId, int originalSenderId, int amount, CancellationToken ct);


}
public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<User?> GetByAuth0UserIdAsync(string auth0UserId, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Auth0UserID == auth0UserId, ct);

    public async Task<User?> GetByIdAsync(int id, CancellationToken ct) =>
       await db.Users.FirstOrDefaultAsync(u => u.ID == id, ct);

    public async Task<bool> TryEscrowTokensAsync(int fromUserId, int toUserId, int amount, CancellationToken ct)
    {
        var deducted = await db.Users.Where(u => u.ID == fromUserId && u.Tokens >= amount)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.Tokens, u => u.Tokens - amount), ct
            );

        if (deducted == 0)
        {
            return false;
        }

        await db.Users.Where(u => u.ID == toUserId)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.EscrowedTokens, u => u.EscrowedTokens + amount), ct
            );
        return true;
    }
    public async Task<bool> TryRefundTokensAsync(int escrowHolderId, int originalSenderId, int amount, CancellationToken ct)
    {
        var deducted = await db.Users.Where(u => u.ID == escrowHolderId && u.EscrowedTokens >= amount)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.EscrowedTokens, u => u.EscrowedTokens - amount), ct);
        if (deducted == 0)
        {
            return false;
        }

        await db.Users.Where(u => u.ID == originalSenderId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Tokens, u => u.Tokens + amount), ct);
        return true;
    }
    
    public async Task<bool> TryReleaseTokensAsync(int userId, int amount, CancellationToken ct)
    {
        var rows = await db.Users.Where(u => u.ID == userId && u.EscrowedTokens >= amount)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.EscrowedTokens, u => u.EscrowedTokens - amount)
                    .SetProperty(u => u.Tokens, u => u.Tokens + amount), ct
            );
        return rows > 0;
    }
    
}