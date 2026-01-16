using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Trades.Repositories;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Trades;

public interface IUserContext
{
    Task<User?> GetRequiredUserAsync(string? auth0UserId, CancellationToken ct);
    Task<User?> GetRequiredMiddlemanAsync(string? auth0UserId, CancellationToken ct);
}

public sealed class UserContext(IUserRepository userRepo) : IUserContext
{
    public async Task<User?> GetRequiredUserAsync(string? auth0UserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            throw new Exception("Missing auth0 user id (sub claim).");

        var trimmed = TrimAuth0UserId(auth0UserId);
        var user = await userRepo.GetByAuth0UserIdAsync(trimmed!, ct);

        return user;
    }

    public Task<User?> GetRequiredMiddlemanAsync(string? auth0UserId, CancellationToken ct)
    {
        return GetRequiredUserAsync(auth0UserId, ct);
    }

    private static string? TrimAuth0UserId(string? auth0UserId)
        => string.IsNullOrWhiteSpace(auth0UserId)
            ? null
            : auth0UserId.StartsWith("auth0|", StringComparison.Ordinal)
                ? auth0UserId["auth0|".Length..]
                : auth0UserId;
}