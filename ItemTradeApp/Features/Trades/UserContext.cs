using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Trades.Repositories;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Trades;

public interface IUserContext
{
    Task<User?> GetRequiredUserAsync(string? auth0UserId, CancellationToken ct);
}

public sealed class UserContext(IUserRepository userRepo) : IUserContext
{
    public async Task<User?> GetRequiredUserAsync(string? auth0UserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            throw new Exception("Missing auth0 user id (sub claim).");

        var trimmed = Auth0IdHandler.Trim(auth0UserId);
        var user = await userRepo.GetByAuth0UserIdAsync(trimmed, ct);

        return user;
    }
}