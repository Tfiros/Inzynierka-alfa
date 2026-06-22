using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Shared.TokenEscrow;

public interface ITokenEscrow
{
    Task<bool> TryLockOwnTokensAsync(int userId, int amount, CancellationToken ct);
    Task<bool> TryReleaseOwnEscrowAsync(int userId, int amount, CancellationToken ct);
    Task<bool> TryEscrowToOtherAsync(int fromUserId, int toUserId, int amount, CancellationToken ct);
    Task<bool> TryRefundEscrowToOtherAsync(int escrowHolderId, int originalSenderId, int amount, CancellationToken ct);
    Task<bool> TryTransferEscrowAsync(int fromUserId, int toUserId, int amount, CancellationToken ct);


}

public class TokenEscrow(AppDbContext dbContext) : ITokenEscrow
{
    public async Task<bool> TryLockOwnTokensAsync(int userId, int amount, CancellationToken ct)
        => await dbContext.Users.Where(u => u.ID == userId && !u.IsDeleted && u.Tokens >= amount)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Tokens, u => u.Tokens - amount)
                .SetProperty(u => u.EscrowedTokens, u => u.EscrowedTokens + amount), ct) == 1;
    
    
    public async Task<bool> TryReleaseOwnEscrowAsync(int userId, int amount, CancellationToken ct)
        => await dbContext.Users.Where(u => u.ID == userId && !u.IsDeleted && u.EscrowedTokens >= amount)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EscrowedTokens, u => u.EscrowedTokens - amount)
                .SetProperty(u => u.Tokens, u => u.Tokens + amount), ct) == 1;

    public async Task<bool> TryEscrowToOtherAsync(int fromUserId, int toUserId, int amount, CancellationToken ct)
        => await AtomicTokenEscrowOperation(async () =>
        {
            var deducted = await dbContext.Users
                .Where(u => u.ID == fromUserId && !u.IsDeleted && u.Tokens >= amount)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Tokens, u => u.Tokens - amount)
                    , ct) == 1;
            if (!deducted) return false;
            var added = await dbContext.Users
                .Where(u => u.ID == toUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.EscrowedTokens, u => u.EscrowedTokens + amount)
                    , ct) == 1;
            return added;
        }, ct);

    public async Task<bool> TryRefundEscrowToOtherAsync(int escrowHolderId, int originalSenderId, int amount,
        CancellationToken ct)
        => await AtomicTokenEscrowOperation(async () =>  {
            var deducted = await dbContext.Users.Where(u => u.ID == escrowHolderId && u.EscrowedTokens >= amount)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.EscrowedTokens, u => u.EscrowedTokens - amount), ct) == 1;
            if (!deducted) return false;
            var added = await dbContext.Users.Where(u => u.ID == originalSenderId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Tokens, u => u.Tokens + amount), ct) == 1;
            return added;
        }, ct);
   

    public async Task<bool> TryTransferEscrowAsync(int fromUserId, int toUserId, int amount, CancellationToken ct)
        => await AtomicTokenEscrowOperation(async () =>
        {
            var deducted = await dbContext.Users.Where(u => u.ID == fromUserId && u.EscrowedTokens >= amount)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.EscrowedTokens, u => u.EscrowedTokens - amount), ct) == 1;
            if (!deducted) return false;
            var added = await dbContext.Users.Where(u => u.ID == toUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.EscrowedTokens, u => u.EscrowedTokens + amount), ct) == 1;
            return added;
        }, ct);

    private async Task<bool> AtomicTokenEscrowOperation(Func<Task<bool>> tokenEscrowFunc, CancellationToken ct)
    {
        var tx = dbContext.Database.CurrentTransaction;
        if (tx is null)
        {
            throw new InvalidOperationException("Token Escrow Method must be run inside a transaction.");
        }

        await tx.CreateSavepointAsync("tokenEscrow", ct);
        try
        {
            var result = await tokenEscrowFunc();
            if (!result)
            {
                await tx.RollbackToSavepointAsync("tokenEscrow", ct);
            }
            return result;
        }
        catch
        {
            await tx.RollbackToSavepointAsync("tokenEscrow", ct);
            throw;
        }
    }
}