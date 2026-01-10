using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Offers;

public sealed record UserState(int Id, bool IsDeleted, int Tokens);

public interface IUsersRepository
{
    Task<UserState?> GetStateByAuth0IdAsync(string auth0UserId, CancellationToken ct = default);
    Task<bool> TrySubtractTokenCostAsync(int userId, int tokenCost, CancellationToken ct = default);

}

public class UsersRepository(AppDbContext dbContext) : IUsersRepository
{
    public Task<UserState?> GetStateByAuth0IdAsync(string auth0UserId, CancellationToken ct = default)
        => dbContext.Users.AsNoTracking()
            .Where(u => u.Auth0UserID == auth0UserId)
            .Select(u => new UserState(u.ID, u.IsDeleted, u.Tokens))
            .SingleOrDefaultAsync(ct);

    public async Task<bool> TrySubtractTokenCostAsync(int userId, int tokenCost, CancellationToken ct = default)
    {
        var updated = await dbContext.Users
            .Where(u => u.ID == userId && !u.IsDeleted && u.Tokens >= tokenCost)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Tokens, u => u.Tokens - tokenCost), ct);
        return updated == 1;
    }

}